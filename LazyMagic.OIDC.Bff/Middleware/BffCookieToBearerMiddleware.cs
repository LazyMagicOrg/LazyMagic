using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LazyMagic.OIDC.Bff;

/// <summary>
/// Hot-path bridge (§8.10 "Authenticated API call"). If the request carries the BFF
/// session cookie and NO Authorization header, decrypt the cookie → access token and
/// attach <c>Authorization: Bearer</c> + <c>lz-authname</c> so the host's existing
/// multi-scheme JWT middleware authenticates the proxied call. When the access token
/// is within the skew window, refresh first (session-store lock + token-client refresh
/// + re-issue cookie). On ANY failure, pass through unauthenticated (never 500).
///
/// MUST run BEFORE the host's own auth middleware (wired via <see cref="BffStartupFilter"/>).
/// </summary>
public sealed class BffCookieToBearerMiddleware
{
    private const int RefreshLockSeconds = 10;
    private const int MaxReReadAttempts = 3;

    private readonly RequestDelegate _next;
    private readonly BffOptions _options;
    private readonly IBffCookieCodec _codec;
    private readonly IBffSessionStore _store;
    private readonly IBffTokenClient _tokenClient;
    private readonly ILogger<BffCookieToBearerMiddleware> _logger;

    public BffCookieToBearerMiddleware(
        RequestDelegate next,
        IOptions<BffOptions> options,
        IBffCookieCodec codec,
        IBffSessionStore store,
        IBffTokenClient tokenClient,
        ILogger<BffCookieToBearerMiddleware> logger)
    {
        _next = next;
        _options = options.Value;
        _codec = codec;
        _store = store;
        _tokenClient = tokenClient;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await TryAttachAsync(context).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Defensive: never let the bridge break a request. Pass through unauthenticated.
            _logger.LogDebug(ex, "BFF cookie->bearer bridge failed; passing through unauthenticated.");
        }

        await _next(context).ConfigureAwait(false);
    }

    private async Task TryAttachAsync(HttpContext context)
    {
        var req = context.Request;

        // SECURITY: lz-authname selects WHICH pool scheme validates the Bearer. It must only
        // ever be set by Attach (below), never by the caller. Strip any inbound value
        // unconditionally — before the Authorization early-return — so a client cannot steer
        // the validation scheme on a self-supplied Authorization or empty-ClientId path.
        req.Headers.Remove(BffConstants.AuthNameHeader);

        // Respect an explicit Authorization header (e.g. the /bff/ws-token holder).
        if (req.Headers.ContainsKey(BffConstants.AuthorizationHeader))
            return;

        if (!req.Cookies.TryGetValue(_options.CookieName, out var cookieValue) || string.IsNullOrEmpty(cookieValue))
            return;

        var payload = _codec.Unprotect(cookieValue);
        if (payload is null || string.IsNullOrEmpty(payload.AccessToken))
            return;

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var remaining = payload.Exp - now;

        if (remaining > _options.AccessTokenSkewSeconds)
        {
            Attach(context, payload.AccessToken);
            return;
        }

        // Within skew (or expired): attempt a refresh. Pass through unauthenticated on failure.
        var refreshed = await TryRefreshAsync(context, payload).ConfigureAwait(false);
        if (refreshed is not null)
            Attach(context, refreshed.AccessToken);
        // else: do not attach; downstream sees an anonymous request.
    }

    private void Attach(HttpContext context, string accessToken)
    {
        // We MUST stamp the matching lz-authname whenever we attach a Bearer (the host's
        // multi-scheme JWT middleware selects the pool scheme from it). If we cannot resolve
        // an authname, refuse to attach rather than emit a Bearer the host can't route — and
        // never leave a (now-stripped) caller value in play.
        var authName = ResolveAuthName();
        if (string.IsNullOrEmpty(authName))
            return;

        context.Request.Headers[BffConstants.AuthorizationHeader] = $"Bearer {accessToken}";
        context.Request.Headers[BffConstants.AuthNameHeader] = authName;
    }

    private string? ResolveAuthName()
    {
        // SECURITY TODO: derive the authname from the validated token issuer/pool rather than a
        // static option, once per-pool issuer mapping is wired in AppHost. For now use the
        // configured LZ_BFF_AUTHNAME (BffOptions.AuthName) if present, else the provider name.
        if (!string.IsNullOrWhiteSpace(_options.AuthName))
            return _options.AuthName;
        var provider = _options.Provider.ToString().ToLowerInvariant();
        return string.IsNullOrWhiteSpace(provider) ? null : provider;
    }

    private async Task<BffCookiePayload?> TryRefreshAsync(HttpContext context, BffCookiePayload payload)
    {
        if (string.IsNullOrEmpty(payload.Sid))
            return null;

        // 1) Try to win the refresh lock. The fencing token is non-null only for the winner.
        var lockToken = await _store.TryAcquireRefreshLockAsync(payload.Sid, RefreshLockSeconds, context.RequestAborted)
            .ConfigureAwait(false);

        if (lockToken is null)
        {
            // LOCK-LOSER: another request is rotating. We must NOT re-refresh the refresh token
            // (that double-rotates the family and invalidates the session). Instead, wait for the
            // winner to publish its freshly minted access token, then ADOPT it (re-issue our cookie).
            for (var i = 0; i < MaxReReadAttempts; i++)
            {
                await Task.Delay(50, context.RequestAborted).ConfigureAwait(false);

                var rec = await _store.GetAsync(payload.Sid, context.RequestAborted).ConfigureAwait(false);
                if (rec is null || rec.Revoked)
                    return null;

                var lockCleared = !rec.LockUntil.HasValue
                    || rec.LockUntil.Value < DateTimeOffset.UtcNow.ToUnixTimeSeconds();

                // Adopt the winner's published access token if it is newer than our cookie's.
                if (!string.IsNullOrEmpty(rec.AccessToken)
                    && rec.AccessTokenExp.HasValue
                    && rec.AccessTokenExp.Value > payload.Exp)
                {
                    return ReissueFromStoredAccessToken(context, payload, rec.AccessToken!, rec.AccessTokenExp.Value, rec.Ttl);
                }

                // Lock cleared but no usable published token (e.g. winner failed). Stop re-refreshing
                // here — let the request pass through unauthenticated and the client retry.
                if (lockCleared)
                    return null;
            }
            return null;
        }

        // 2) WINNER: we hold the lock. Load the row, ensure not revoked, then refresh + publish.
        var record = await _store.GetAsync(payload.Sid, context.RequestAborted).ConfigureAwait(false);
        if (record is null || record.Revoked || string.IsNullOrEmpty(record.RefreshToken))
            return null;

        return await DoRefreshAndReissueAsync(context, payload, record.RefreshToken, lockToken, record.Ttl)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Re-issue the session cookie from an access token (either freshly refreshed by us, or
    /// published by the lock winner). Preserves the absolute session expiry (<paramref name="ttlEpoch"/>)
    /// so a silent refresh never drops/extends the absolute logoff window.
    /// </summary>
    private BffCookiePayload ReissueFromStoredAccessToken(
        HttpContext context, BffCookiePayload payload, string accessToken, long accessTokenExp, long ttlEpoch)
    {
        var newPayload = new BffCookiePayload
        {
            Sid = payload.Sid,
            AccessToken = accessToken,
            Exp = accessTokenExp,
            Claims = payload.Claims,
        };

        if (!context.Response.HasStarted)
        {
            var cookie = _codec.Protect(newPayload);
            var expires = ttlEpoch > 0 ? DateTimeOffset.FromUnixTimeSeconds(ttlEpoch) : (DateTimeOffset?)null;
            context.Response.Cookies.Append(_options.CookieName, cookie, BffCookieBuilder.Session(_options, expires));
        }

        return newPayload;
    }

    private async Task<BffCookiePayload?> DoRefreshAndReissueAsync(
        HttpContext context, BffCookiePayload payload, string refreshToken, string lockToken, long ttlEpoch)
    {
        if (string.IsNullOrEmpty(refreshToken))
            return null;

        BffTokenResult tokens;
        try
        {
            tokens = await _tokenClient.RefreshAsync(refreshToken, context.RequestAborted).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "BFF refresh failed for sid.");
            return null;
        }

        if (string.IsNullOrEmpty(tokens.AccessToken))
            return null;

        // Persist the rotated refresh token AND publish the fresh access token (+ expiry) so
        // concurrent siblings adopt it instead of re-rotating — all under the fencing condition.
        var newRt = string.IsNullOrEmpty(tokens.RefreshToken) ? refreshToken : tokens.RefreshToken;
        try
        {
            await _store.UpdateRefreshAsync(payload.Sid, newRt, tokens.AccessToken, tokens.ExpiresAtEpoch, lockToken, context.RequestAborted)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Persist failed (lost/expired fencing lock, or transient store error). The provider
            // already rotated the rt at this point, so the stored rt is now stale — do NOT hand back
            // a cookie whose session row is inconsistent. Pass through unauthenticated; the client
            // retries and a subsequent refresh re-reads the source-of-truth row.
            _logger.LogDebug(ex, "BFF refresh-token persist failed (fencing condition or store error); passing through unauthenticated.");
            return null;
        }

        return ReissueFromStoredAccessToken(context, payload, tokens.AccessToken, tokens.ExpiresAtEpoch, ttlEpoch);
    }
}
