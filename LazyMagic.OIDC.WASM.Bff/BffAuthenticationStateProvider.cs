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
    private const string UserEndpoint = "bff/user";

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
        TimeSpan pollInterval)
    {
        _http = http;
        _logger = logger;
        _pollInterval = pollInterval;

        if (_pollInterval > TimeSpan.Zero)
        {
            // Fire-and-forget background re-poll. Disabled when interval <= 0.
            _pollTimer = new Timer(OnPollTick, null, _pollInterval, _pollInterval);
        }
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, UserEndpoint);
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
    public void NotifyStateChanged()
    {
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    private void OnPollTick(object? state)
    {
        try
        {
            // Re-evaluate the session and push the new state to the UI. If the server
            // killed the session, this flips authenticated → anonymous.
            NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[BFF] Poll tick failed");
        }
    }

    private ClaimsPrincipal BuildPrincipal(string json)
    {
        var claims = new List<Claim>();

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

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

        if (claims.Count == 0)
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
