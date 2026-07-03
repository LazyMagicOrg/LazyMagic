namespace LazyMagic.OIDC.WASM.Bff;

/// <summary>
/// Client-side BFF (Backend-For-Frontend) <see cref="AuthenticationStateProvider"/>.
///
/// In BFF mode the SPA holds NO tokens. The authoritative session lives in an
/// <c>HttpOnly</c> cookie (<c>__bff</c>) that JavaScript cannot read. To learn whether
/// the user is authenticated we call the same-origin endpoint <c>GET /bff/user</c> with
/// browser credentials included so the cookie rides along:
///   - <c>200</c> + an envelope <c>{ isAuthenticated, claims:{ sub, name, email, roles:[...] } }</c>
///     → unwrap the inner <c>claims</c> object and build an authenticated <see cref="ClaimsPrincipal"/>
///     (authenticationType "bff"). The server normalizes roles for BOTH providers under the
///     plural <c>roles</c> key (Cognito <c>cognito:groups</c> / Keycloak <c>realm_access.roles</c>),
///     which is in <see cref="RoleClaimTypes"/>, so <c>&lt;AuthorizeView&gt;</c> and
///     <c>[Authorize(Roles=...)]</c> resolve directly.
///   - <c>401</c> → anonymous principal.
///
/// A configurable re-poll timer (default 5 min, per MultiTenantAuth.md §8.14) periodically
/// re-fetches <c>/bff/user</c> and raises <see cref="NotifyAuthenticationStateChanged"/> so a
/// server-side session kill (logout elsewhere / revocation) flips the UI without a reload.
/// </summary>
public sealed class BffAuthenticationStateProvider : AuthenticationStateProvider, IDisposable
{
    // HOST-ABSOLUTE (leading slash): {prefix}/user lives at the host root, but the WASM may be
    // mounted under a sub-path (e.g. <base href="/store/">) and the HttpClient's BaseAddress
    // is that sub-path. A relative "bff/user" would resolve to "/store/bff/user" (the app's
    // own HTML), so the provider would never see the real session and always read anonymous.
    // {prefix} = /bff (tenantauth) or /cbff (consumerauth), supplied by AddLazyMagicOIDCWASMBff.
    private readonly string _userEndpoint;

    // Claim types that, when present in the returned claims, are treated as ROLE claims.
    // The server already maps roles into these; we tag the ClaimsIdentity's RoleClaimType
    // so IsInRole / [Authorize(Roles=...)] resolve them.
    private static readonly string[] RoleClaimTypes = { "role", "roles", ClaimTypes.Role, "cognito:groups", "groups" };

    // The claim used as the identity Name (so user?.Identity?.Name is populated).
    private static readonly string[] NameClaimTypes =
    {
        "name", "cognito:username", "preferred_username", ClaimTypes.Name, "email", "sub"
    };

    private const string AuthenticationType = "bff";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly ILogger<BffAuthenticationStateProvider> _logger;
    private readonly TimeSpan _pollInterval;
    private readonly Timer? _pollTimer;

    private static readonly AuthenticationState Anonymous =
        new(new ClaimsPrincipal(new ClaimsIdentity()));

    public BffAuthenticationStateProvider(
        HttpClient http,
        ILogger<BffAuthenticationStateProvider> logger,
        TimeSpan pollInterval,
        string routePrefix = "/bff")
    {
        _http = http;
        _logger = logger;
        _pollInterval = pollInterval;
        _userEndpoint = "/" + routePrefix.Trim('/') + "/user";

        // Push the auth state to subscribers EARLY and repeatedly through the first ~20s, then
        // settle to _pollInterval. Components subscribe only AFTER the (slow) WASM boot, so a
        // single early notify would race their subscriptions; re-notify every couple seconds
        // until the boot window closes, then settle. (BaseApp.BlazorUI 1.0.1's LoginDisplay
        // now also does its own initial GetAuthenticationStateAsync — fixed 2026-07-03 — but
        // this window still covers older packages and any other late subscriber.) With polling
        // disabled we still push during the window, then stop.
        _pollTimer = new Timer(OnPollTick, null, InitialNotifyDelay, EarlyPollPeriod);
    }

    private static readonly TimeSpan InitialNotifyDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan EarlyPollPeriod = TimeSpan.FromSeconds(2);
    private const int MaxEarlyTicks = 10; // ~1s + 10×2s ≈ 21s of early re-notify
    private int _earlyTicks;

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, _userEndpoint);
            // Send the HttpOnly BFF session cookie on this same-origin request.
            request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);

            using var response = await _http.SendAsync(request).ConfigureAwait(false);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                _logger.LogDebug("[BFF] /bff/user returned 401; user is anonymous");
                return Anonymous;
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[BFF] /bff/user returned {Status}; treating as anonymous", response.StatusCode);
                return Anonymous;
            }

            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(json))
                return Anonymous;

            var principal = BuildPrincipal(json);
            return new AuthenticationState(principal);
        }
        catch (Exception ex)
        {
            // Network/transient errors → anonymous (fail closed). The next poll re-checks.
            _logger.LogWarning(ex, "[BFF] Error fetching /bff/user; treating as anonymous");
            return Anonymous;
        }
    }

    /// <summary>
    /// Force an immediate re-check of the BFF session and notify subscribers. Useful right
    /// after a navigation returns from /bff/login (the cookie should now exist).
    /// </summary>
    public void NotifyStateChanged() => _ = PublishResolvedStateAsync();

    /// <summary>
    /// Re-check the session, then publish it as a COMPLETED task.
    ///
    /// CRITICAL: publishing the PENDING task returned by <see cref="GetAuthenticationStateAsync"/>
    /// (as the old code did) makes <c>&lt;AuthorizeView&gt;</c> fall back to its authorizing/empty
    /// state until the <c>/bff/user</c> fetch completes — so on EVERY poll the authed-only UI (e.g.
    /// the Pets nav link) blinks out and back. Resolving first and handing AuthorizeView a completed
    /// task flips it straight to the new state with no intermediate "authorizing" render. We still
    /// re-notify on every early tick so a late subscriber (e.g. the bar LoginDisplay) catches up.
    /// </summary>
    private async Task PublishResolvedStateAsync()
    {
        try
        {
            var newState = await GetAuthenticationStateAsync().ConfigureAwait(false);
            NotifyAuthenticationStateChanged(Task.FromResult(newState));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[BFF] Auth state publish failed");
        }
    }

    private void OnPollTick(object? state)
    {
        // Settle the timer to the long interval once the boot re-notify window closes (synchronous,
        // independent of the async re-check below).
        if (++_earlyTicks == MaxEarlyTicks)
        {
            var settled = _pollInterval > TimeSpan.Zero ? _pollInterval : Timeout.InfiniteTimeSpan;
            try { _pollTimer?.Change(settled, settled); } catch { /* timer disposed */ }
        }

        // Re-evaluate the session and push the result as a COMPLETED task (see PublishResolvedStateAsync).
        // If the server killed the session, this flips authenticated → anonymous.
        _ = PublishResolvedStateAsync();
    }

    private ClaimsPrincipal BuildPrincipal(string json)
    {
        var claims = new List<Claim>();

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Authentication is decided by the envelope's `isAuthenticated` flag — NOT by claim
        // count. /bff/user only returns 200 for an authenticated session (401 otherwise), and a
        // valid user may carry zero profile claims/roles (e.g. belongs to no groups). Relying on
        // claim count alone made such users render as anonymous in the SPA.
        var isAuthenticated = root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty("isAuthenticated", out var iaEl)
            && (iaEl.ValueKind == JsonValueKind.True
                || (iaEl.ValueKind == JsonValueKind.String
                    && bool.TryParse(iaEl.GetString(), out var parsedIa) && parsedIa));

        // /bff/user returns an envelope: { "isAuthenticated": true, "claims": { sub, name, email, roles:[...] } }.
        // Descend into the inner "claims" object before enumerating; fall back to the root for tolerance
        // (so a flat claims object — older/alternate shapes — still maps). Enumerating the envelope root
        // would flatten the nested claims into ONE opaque "claims" string and lose roles + name entirely.
        var claimsElement = root;
        if (root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty("claims", out var inner)
            && inner.ValueKind == JsonValueKind.Object)
        {
            claimsElement = inner;
        }

        if (claimsElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in claimsElement.EnumerateObject())
                AddClaimsForProperty(claims, prop.Name, prop.Value);
        }

        // Anonymous ONLY when the envelope says so (and, for tolerance of older shapes that
        // lacked the flag, when there are also no claims). An authenticated session with zero
        // claims still yields an identity WITH an AuthenticationType (→ IsAuthenticated == true).
        if (!isAuthenticated && claims.Count == 0)
            return new ClaimsPrincipal(new ClaimsIdentity());

        var nameClaimType = NameClaimTypes.FirstOrDefault(
            nc => claims.Any(c => string.Equals(c.Type, nc, StringComparison.OrdinalIgnoreCase)))
            ?? "name";

        // Pick the role claim type actually present so IsInRole resolves.
        var roleClaimType = RoleClaimTypes.FirstOrDefault(
            rc => claims.Any(c => string.Equals(c.Type, rc, StringComparison.OrdinalIgnoreCase)))
            ?? ClaimTypes.Role;

        var identity = new ClaimsIdentity(claims, AuthenticationType, nameClaimType, roleClaimType);
        return new ClaimsPrincipal(identity);
    }

    private static void AddClaimsForProperty(List<Claim> claims, string name, JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Array:
                // e.g. roles: ["StoreAdmin","StoreUser"] → one claim per element.
                foreach (var item in value.EnumerateArray())
                {
                    var v = ScalarToString(item);
                    if (v != null)
                        claims.Add(new Claim(name, v));
                }
                break;
            case JsonValueKind.Object:
                // Flatten nested objects to JSON text so nothing is silently dropped.
                claims.Add(new Claim(name, value.GetRawText()));
                break;
            case JsonValueKind.Null or JsonValueKind.Undefined:
                break;
            default:
                var s = ScalarToString(value);
                if (s != null)
                    claims.Add(new Claim(name, s));
                break;
        }
    }

    private static string? ScalarToString(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number => element.GetRawText(),
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        JsonValueKind.Null or JsonValueKind.Undefined => null,
        _ => element.GetRawText()
    };

    public void Dispose()
    {
        _pollTimer?.Dispose();
    }
}
