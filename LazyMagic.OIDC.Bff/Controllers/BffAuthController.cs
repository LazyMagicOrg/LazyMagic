using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LazyMagic.OIDC.Bff;

/// <summary>
/// BFF auth endpoints (§8.3). Auto-discovered as an MVC ApplicationPart when the host
/// references this package; otherwise registered explicitly in <c>AddLazyMagicBff</c>.
/// </summary>
// One route, two pools: the {bffSeg} segment ("bff"=tenantauth, "cbff"=consumerauth) selects the
// BFF instance from the registry per request. Endpoints: /{bff|cbff}/login|callback|user|logout|logout-callback.
[ApiController]
[Route("{bffSeg:regex(^(bff|cbff)$)}")]
public sealed class BffAuthController : ControllerBase
{
    private readonly BffRegistry _registry;
    private readonly ILogger<BffAuthController> _logger;

    public BffAuthController(BffRegistry registry, ILogger<BffAuthController> logger)
    {
        _registry = registry;
        _logger = logger;
    }

    // Resolve the BFF instance for THIS request from the route segment. The same-named per-request
    // accessors below mean the action bodies don't change vs the single-pool version.
    private BffInstance Inst => _registry.ResolveByKey(
        RouteData.Values.TryGetValue("bffSeg", out var v) ? v?.ToString() : null);
    private BffOptions _options => Inst.Options;
    private IBffTokenClient _tokenClient => Inst.Tokens;
    private IBffSessionStore _store => Inst.Store;
    private IBffCookieCodec _cookieCodec => Inst.Cookie;
    private IBffTransactionCodec _txnCodec => Inst.Txn;

    // Per-instance cookie names + the IdP-registered sign-out path. Derived from the instance's
    // options so the two pools never collide; for tenantauth these equal the historical
    // __bff_txn / __bff_logout / __bff_logout_host / /bff/logout-callback values.
    private string TxnCookieName => _options.CookieName + "_txn";
    private string LogoutReturnCookieName => _options.CookieName + "_logout";
    private string LogoutOriginCookieName => _options.CookieName + "_logout_host";
    private string LogoutCallbackPath => _options.RoutePrefix.TrimEnd('/') + "/logout-callback";

    /// <summary>GET /bff/login?returnUrl=… → 302 to the IdP authorize endpoint.</summary>
    [HttpGet("login")]
    public async Task<IActionResult> Login([FromQuery] string? returnUrl, CancellationToken ct)
    {
        var safeReturn = SanitizeReturnUrl(returnUrl);
        // Pin the redirect_uri to the APEX /bff/callback — the only one registered with the IdP.
        // The originating (possibly subtenant) host is stashed in the transaction and fanned back
        // after the apex callback completes the exchange (see the subtenant fan-out note below).
        var redirectUri = BuildCallbackUrl(_options.CallbackPath);

        var authorize = await _tokenClient.BuildAuthorizeUrlAsync(redirectUri, ct).ConfigureAwait(false);

        var txn = new BffTransaction
        {
            CodeVerifier = authorize.CodeVerifier,
            State = authorize.State,
            Nonce = authorize.Nonce,
            ReturnUrl = safeReturn,
            OriginHost = ViewerHost,
            IssuedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        };
        Response.Cookies.Append(TxnCookieName, _txnCodec.Protect(txn), BffCookieBuilder.Transaction(_options));

        return Redirect(authorize.AuthorizeUrl);
    }

    /// <summary>GET /bff/callback?code=&amp;state= → exchange + validate + set session cookie + 302 returnUrl.</summary>
    [HttpGet("callback")]
    public async Task<IActionResult> Callback([FromQuery] string? code, [FromQuery] string? state, [FromQuery] string? error, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(error))
        {
            _logger.LogInformation("BFF callback returned IdP error {Error}.", error);
            return BadRequest(new { error });
        }

        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
            return BadRequest(new { error = "missing_code_or_state" });

        // Recover and immediately clear the transaction cookie.
        if (!Request.Cookies.TryGetValue(TxnCookieName, out var txnCookie) || string.IsNullOrEmpty(txnCookie))
            return BadRequest(new { error = "missing_transaction" });
        Response.Cookies.Append(TxnCookieName, string.Empty, BffCookieBuilder.Delete(_options));

        var txn = _txnCodec.Unprotect(txnCookie);
        if (txn is null)
            return BadRequest(new { error = "invalid_transaction" });

        // CSRF/replay: returned state MUST equal the stashed state.
        if (!FixedTimeEquals(state, txn.State))
            return BadRequest(new { error = "state_mismatch" });

        // Must EXACTLY match the redirect_uri sent at /bff/login (apex), independent of which host
        // this callback landed on. BuildCallbackUrl is deterministic from the parent CookieDomain.
        var redirectUri = BuildCallbackUrl(_options.CallbackPath);

        BffTokenResult tokens;
        try
        {
            tokens = await _tokenClient.ExchangeCodeAsync(code, txn.CodeVerifier, redirectUri, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "BFF code exchange failed.");
            return BadRequest(new { error = "code_exchange_failed" });
        }

        if (string.IsNullOrEmpty(tokens.IdToken))
            return BadRequest(new { error = "missing_id_token" });

        ValidatedIdToken validated;
        try
        {
            validated = await _tokenClient.ValidateIdTokenAsync(tokens.IdToken, txn.Nonce, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "BFF id_token validation failed.");
            return BadRequest(new { error = "invalid_id_token" });
        }

        // Create the session: sid = CSPRNG(32B); store refresh token + revocation in DynamoDB.
        var sid = Base64Url(RandomNumberGenerator.GetBytes(32));
        var ttlEpoch = DateTimeOffset.UtcNow.AddHours(_options.SessionTtlHours).ToUnixTimeSeconds();

        await _store.CreateAsync(new BffSessionRecord
        {
            Sid = sid,
            RefreshToken = tokens.RefreshToken ?? string.Empty,
            Revoked = false,
            Sub = validated.Sub,
            Tenant = ResolveTenant(),
            Pool = _options.Provider.ToString().ToLowerInvariant(),
            Ttl = ttlEpoch,
        }, ct).ConfigureAwait(false);

        var minimalClaims = BffClaimsMapper.BuildMinimalClaims(validated.Principal, _options.Provider);
        var payload = new BffCookiePayload
        {
            Sid = sid,
            AccessToken = tokens.AccessToken,
            Exp = tokens.ExpiresAtEpoch,
            Claims = minimalClaims,
        };

        Response.Cookies.Append(_options.CookieName, _cookieCodec.Protect(payload), BffCookieBuilder.Session(_options, DateTimeOffset.FromUnixTimeSeconds(ttlEpoch)));

        // Re-sanitize at the sink: the value was sanitized at /bff/login time, but never
        // hand a stashed value straight to Redirect — close the open-redirect path defensively.
        // Then fan back to the originating (subtenant) host so the user lands where they started,
        // not on the apex where the callback ran. The session cookie carries Domain=.{root}, so it
        // is already valid on the subtenant host after this hop.
        return Redirect(BuildFanBackUrl(txn.OriginHost, SanitizeReturnUrl(txn.ReturnUrl)));
    }

    /// <summary>GET /bff/user → 200 {claims} from the session cookie, or 401.</summary>
    [HttpGet("user")]
    public IActionResult GetUser()
    {
        if (!Request.Cookies.TryGetValue(_options.CookieName, out var cookie) || string.IsNullOrEmpty(cookie))
            return Unauthorized();

        var payload = _cookieCodec.Unprotect(cookie);
        if (payload is null)
            return Unauthorized();

        return Ok(new { isAuthenticated = true, claims = payload.Claims });
    }

    /// <summary>
    /// GET|POST /bff/logout → delete session, clear cookie, return the provider logout URL.
    /// The destructive path (session delete + cookie clear) is gated by the non-safelisted
    /// CSRF header on BOTH verbs so a top-level cross-site GET navigation can never force a
    /// logout (§8.12). The credentialed SPA flow POSTs with <c>X-CSRF: 1</c> and reads the
    /// returned <c>logoutUrl</c>, then navigates (forceLoad) to it. Without the header, the
    /// endpoint returns 403 and does NOT touch server state.
    /// </summary>
    [HttpGet("logout")]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromQuery] string? returnUrl, CancellationToken ct)
    {
        // CSRF: the destructive logout is state-changing and MUST carry the non-safelisted
        // header regardless of verb. A bare cross-site GET cannot attach it, so it is rejected.
        if (!HasCsrfHeader())
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "missing_csrf_header" });

        string? idTokenHint = null;
        if (Request.Cookies.TryGetValue(_options.CookieName, out var cookie) && !string.IsNullOrEmpty(cookie))
        {
            var payload = _cookieCodec.Unprotect(cookie);
            if (payload is not null && !string.IsNullOrEmpty(payload.Sid))
            {
                try
                {
                    await _store.DeleteAsync(payload.Sid, ct).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "BFF session delete during logout failed (continuing).");
                }
            }
        }

        // Clear the session cookie (Max-Age=0).
        Response.Cookies.Append(_options.CookieName, string.Empty, BffCookieBuilder.Delete(_options));

        // The IdP (Cognito/Keycloak) only redirects back to a REGISTERED sign-out URL — the apex
        // /bff/logout-callback — NOT arbitrary app paths like /store/ (passing the raw returnUrl as
        // logout_uri makes Cognito bounce to its login page). Stash the app's post-logout
        // destination in a short-lived cookie so the callback can fan back to it.
        // SUBTENANT FAN-OUT: BuildCallbackUrl pins logout_uri to the apex logout-callback (the only
        // one registered with the IdP). We also stash the originating host so the apex callback can
        // fan the user back to the subtenant — the mirror of the login /bff/callback flow.
        var finalDest = SanitizeReturnUrl(returnUrl);
        Response.Cookies.Append(LogoutReturnCookieName, Uri.EscapeDataString(finalDest),
            BffCookieBuilder.Transaction(_options));
        Response.Cookies.Append(LogoutOriginCookieName, Uri.EscapeDataString(ViewerHost),
            BffCookieBuilder.Transaction(_options));

        var postLogoutRedirect = BuildCallbackUrl(LogoutCallbackPath);
        string logoutUrl;
        try
        {
            logoutUrl = await _tokenClient.BuildLogoutUrlAsync(idTokenHint, postLogoutRedirect, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "BFF logout URL build failed; returning the post-logout destination.");
            logoutUrl = BuildAbsoluteUrl(finalDest);
        }

        // Return the provider logout URL for the SPA to navigate to (credentialed POST flow).
        return Ok(new { logoutUrl });
    }

    /// <summary>
    /// GET /bff/logout-callback — the IdP's REGISTERED sign-out URL. The IdP redirects here after
    /// clearing its own session; we fan the user back to the app's post-logout destination stashed
    /// at /bff/logout. A plain top-level GET that mutates no server state (the session was already
    /// deleted at /bff/logout), so it needs no CSRF guard.
    /// </summary>
    [HttpGet("logout-callback")]
    public IActionResult LogoutCallback()
    {
        var dest = "/";
        if (Request.Cookies.TryGetValue(LogoutReturnCookieName, out var c) && !string.IsNullOrEmpty(c))
        {
            try { dest = Uri.UnescapeDataString(c); } catch { dest = "/"; }
            Response.Cookies.Append(LogoutReturnCookieName, string.Empty, BffCookieBuilder.Delete(_options));
        }

        string? originHost = null;
        if (Request.Cookies.TryGetValue(LogoutOriginCookieName, out var oh) && !string.IsNullOrEmpty(oh))
        {
            try { originHost = Uri.UnescapeDataString(oh); } catch { originHost = null; }
            Response.Cookies.Append(LogoutOriginCookieName, string.Empty, BffCookieBuilder.Delete(_options));
        }

        // Re-sanitize at the sink — the cookie could be tampered — to close the open-redirect path,
        // then fan back to the originating (subtenant) host (guarded to the system root domain).
        return Redirect(BuildFanBackUrl(originHost, SanitizeReturnUrl(dest)));
    }

    /// <summary>GET /bff/ws-token → mint a short-lived token for the AppSync WebSocket.</summary>
    [HttpGet("ws-token")]
    public IActionResult WsToken()
    {
        // SECURITY TODO: mint short-lived token for AppSync WS. The WS subprotocol header
        // cannot ride the cookie, so this endpoint must return a narrowly-scoped, short-TTL
        // token derived from the current session (validate cookie, re-mint/downscope, audit).
        return StatusCode(StatusCodes.Status501NotImplemented, new { error = "not_implemented" });
    }

    // ---- helpers ----

    private bool HasCsrfHeader() =>
        Request.Headers.TryGetValue(BffConstants.CsrfHeader, out var v) &&
        v.ToString().Trim() == BffConstants.CsrfHeaderValue;

    private string? ResolveTenant() =>
        Request.Headers.TryGetValue("lz-tenantid", out var t) ? t.ToString() : null;

    /// <summary>
    /// The apex host derived from the parent <see cref="BffOptions.CookieDomain"/> (".root" → "root"),
    /// or <c>null</c> when no parent domain is configured. When null the BFF keeps the legacy
    /// per-request-host callback behavior (single-host deploys, localhost dev).
    /// </summary>
    private string? ApexHost
    {
        get
        {
            var d = _options.CookieDomain;
            return string.IsNullOrWhiteSpace(d) ? null : d.TrimStart('.');
        }
    }

    /// <summary>
    /// The public viewer host of the current request. Behind CloudFront's AllViewerExceptHostHeader
    /// policy the origin sees the ALB host in <see cref="HttpRequest.Host"/>, so prefer the
    /// <c>lz-tenantid</c> header (CFRequest injects the original viewer Host).
    /// </summary>
    private string ViewerHost
    {
        get
        {
            var host = ResolveTenant();
            return string.IsNullOrWhiteSpace(host) ? Request.Host.Value : host!;
        }
    }

    /// <summary>
    /// Build the OIDC callback/redirect URL. When a parent <see cref="BffOptions.CookieDomain"/> is
    /// configured the URL is pinned to the APEX host — the only <c>/bff/callback</c> +
    /// <c>/bff/logout-callback</c> registered with the IdP — so a login/logout initiated on any
    /// subtenant host still presents a registered redirect_uri (no <c>redirect_mismatch</c>). The
    /// originating host is carried in the transaction/logout cookie and fanned back afterwards.
    /// Without a parent domain, falls back to the per-request-host URL (<see cref="BuildAbsoluteUrl"/>).
    /// </summary>
    private string BuildCallbackUrl(string path)
    {
        var apex = ApexHost;
        if (apex is null)
            return BuildAbsoluteUrl(path);
        var p = path.StartsWith('/') ? path : "/" + path;
        return $"https://{apex}{p}";
    }

    /// <summary>
    /// Resolve the absolute post-flow redirect. When the originating host is a member of the system
    /// root domain, fan back to it (<c>https://{originHost}{safePath}</c>); otherwise stay relative on
    /// the callback host. <paramref name="safePath"/> MUST already be an open-redirect-guarded
    /// same-site relative path (i.e. the output of <see cref="SanitizeReturnUrl"/>).
    /// </summary>
    private string BuildFanBackUrl(string? originHost, string safePath)
    {
        var apex = ApexHost;
        if (apex is null || string.IsNullOrWhiteSpace(originHost) || !IsHostWithinRoot(originHost!, apex))
            return safePath;
        return $"https://{originHost}{safePath}";
    }

    /// <summary>
    /// True when <paramref name="host"/> equals the apex or is a subdomain of it. Also rejects hosts
    /// containing characters outside the DNS label set (letters/digits/'.'/'-') — including ':' (port),
    /// '/' and '@' — to close header-injection / open-redirect tricks on the absolute fan-back hop.
    /// </summary>
    private static bool IsHostWithinRoot(string host, string apex)
    {
        foreach (var ch in host)
            if (!(char.IsAsciiLetterOrDigit(ch) || ch == '.' || ch == '-'))
                return false;
        return host.Equals(apex, StringComparison.OrdinalIgnoreCase)
            || host.EndsWith("." + apex, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Build an absolute URL on the current host for a server-relative path.</summary>
    private string BuildAbsoluteUrl(string pathOrUrl)
    {
        // Already an http(s) absolute URL? Return as-is. Do NOT rely on
        // Uri.TryCreate(Absolute) alone: on LINUX "/bff/callback" parses as the
        // file URI file:///bff/callback (a valid absolute *file* path), which is
        // how the IdP redirect_uri became "file:///bff/callback".
        if (Uri.TryCreate(pathOrUrl, UriKind.Absolute, out var abs)
            && (abs.Scheme == Uri.UriSchemeHttp || abs.Scheme == Uri.UriSchemeHttps))
            return abs.ToString();

        var path = pathOrUrl.StartsWith('/') ? pathOrUrl : "/" + pathOrUrl;

        // Behind CloudFront's AllViewerExceptHostHeader policy the origin sees the
        // ALB host in Request.Host, not the public viewer host — so prefer the
        // lz-tenantid header (CFRequest injects the original viewer Host). Always
        // https: the IdP callback must be the public https URL.
        var host = ResolveTenant();
        if (string.IsNullOrWhiteSpace(host)) host = Request.Host.Value;
        return $"https://{host}{path}";
    }

    /// <summary>
    /// Only allow same-site relative return URLs (open-redirect guard). Prefer the
    /// framework's <see cref="ControllerBase.Url"/>.IsLocalUrl (which rejects backslashes,
    /// "//", "/\", control chars, and absolute URLs) when the controller context is
    /// available; otherwise fall back to the hardened static check.
    /// </summary>
    private string SanitizeReturnUrl(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
            return "/";

        // Reject control chars on BOTH paths (parity with SanitizeReturnUrlCore) — defense in depth
        // against CR/LF response-splitting, even though the header writer would also reject it.
        if (returnUrl.Any(char.IsControl))
            return "/";

        // IsLocalUrl is null only outside an MVC request context (e.g. unit tests); guard for it.
        if (Url is not null)
            return Url.IsLocalUrl(returnUrl) ? returnUrl : "/";

        return SanitizeReturnUrlCore(returnUrl);
    }

    /// <summary>
    /// Hardened static open-redirect guard for paths without a controller URL helper.
    /// Rejects: null/empty, ANY backslash (a single leading '\' is the real bypass —
    /// browsers normalize '/\' in a Location to '//'), control characters, absolute URIs,
    /// and protocol-relative '//' prefixes. Otherwise ensures a single leading '/'.
    /// </summary>
    private static string SanitizeReturnUrlCore(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
            return "/";
        // Reject backslashes (the '\evil.com' -> '/\evil.com' bypass) and control chars.
        if (returnUrl.IndexOf('\\') >= 0 || returnUrl.Any(char.IsControl))
            return "/";
        // Reject absolute/protocol-relative URLs to prevent open redirects.
        if (returnUrl.StartsWith("//", StringComparison.Ordinal))
            return "/";
        if (Uri.TryCreate(returnUrl, UriKind.Absolute, out _))
            return "/";
        return returnUrl.StartsWith('/') ? returnUrl : "/" + returnUrl;
    }

    private static string Base64Url(byte[] bytes) => Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode(bytes);

    private static bool FixedTimeEquals(string a, string b)
    {
        var ba = System.Text.Encoding.UTF8.GetBytes(a);
        var bb = System.Text.Encoding.UTF8.GetBytes(b);
        if (ba.Length != bb.Length) return false;
        return CryptographicOperations.FixedTimeEquals(ba, bb);
    }
}
