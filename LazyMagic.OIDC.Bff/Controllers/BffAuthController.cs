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
[ApiController]
[Route("bff")]
public sealed class BffAuthController : ControllerBase
{
    private readonly BffOptions _options;
    private readonly IBffTokenClient _tokenClient;
    private readonly IBffSessionStore _store;
    private readonly IBffCookieCodec _cookieCodec;
    private readonly IBffTransactionCodec _txnCodec;
    private readonly ILogger<BffAuthController> _logger;

    public BffAuthController(
        IOptions<BffOptions> options,
        IBffTokenClient tokenClient,
        IBffSessionStore store,
        IBffCookieCodec cookieCodec,
        IBffTransactionCodec txnCodec,
        ILogger<BffAuthController> logger)
    {
        _options = options.Value;
        _tokenClient = tokenClient;
        _store = store;
        _cookieCodec = cookieCodec;
        _txnCodec = txnCodec;
        _logger = logger;
    }

    /// <summary>GET /bff/login?returnUrl=… → 302 to the IdP authorize endpoint.</summary>
    [HttpGet("login")]
    public async Task<IActionResult> Login([FromQuery] string? returnUrl, CancellationToken ct)
    {
        var safeReturn = SanitizeReturnUrl(returnUrl);
        var redirectUri = BuildAbsoluteUrl(_options.CallbackPath);

        var authorize = await _tokenClient.BuildAuthorizeUrlAsync(redirectUri, ct).ConfigureAwait(false);

        var txn = new BffTransaction
        {
            CodeVerifier = authorize.CodeVerifier,
            State = authorize.State,
            Nonce = authorize.Nonce,
            ReturnUrl = safeReturn,
            IssuedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        };
        Response.Cookies.Append(BffConstants.TransactionCookieName, _txnCodec.Protect(txn), BffCookieBuilder.Transaction(_options));

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
        if (!Request.Cookies.TryGetValue(BffConstants.TransactionCookieName, out var txnCookie) || string.IsNullOrEmpty(txnCookie))
            return BadRequest(new { error = "missing_transaction" });
        Response.Cookies.Append(BffConstants.TransactionCookieName, string.Empty, BffCookieBuilder.Delete(_options));

        var txn = _txnCodec.Unprotect(txnCookie);
        if (txn is null)
            return BadRequest(new { error = "invalid_transaction" });

        // CSRF/replay: returned state MUST equal the stashed state.
        if (!FixedTimeEquals(state, txn.State))
            return BadRequest(new { error = "state_mismatch" });

        var redirectUri = BuildAbsoluteUrl(_options.CallbackPath);

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
        return Redirect(SanitizeReturnUrl(txn.ReturnUrl));
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

        var postLogoutRedirect = BuildAbsoluteUrl(SanitizeReturnUrl(returnUrl));
        string logoutUrl;
        try
        {
            logoutUrl = await _tokenClient.BuildLogoutUrlAsync(idTokenHint, postLogoutRedirect, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "BFF logout URL build failed; returning returnUrl.");
            logoutUrl = postLogoutRedirect;
        }

        // Return the provider logout URL for the SPA to navigate to (credentialed POST flow).
        return Ok(new { logoutUrl });
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
