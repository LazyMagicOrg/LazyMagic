using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace LazyMagic.OIDC.Bff;

/// <summary>
/// Hot-path bridge (§8.10 "Authenticated API call"). If the request carries a BFF session
/// cookie and NO Authorization header, decrypt the cookie → access token and attach
/// <c>Authorization: Bearer</c> + <c>lz-authname</c> so the host's multi-scheme JWT middleware
/// authenticates the proxied call. Within the skew window, refresh first.
///
/// MULTI-POOL: the request selects an instance via the <c>lz-bff-pool</c> marker header
/// (set by the WASM client); absent ⇒ the default (tenantauth) instance. That instance's
/// cookie/codec/store/token-client/authname are used. (Both pools' parent-domain cookies can be
/// present at once, so the marker — not "whichever cookie exists" — picks the pool.)
///
/// MUST run BEFORE the host's own auth middleware (wired via <see cref="BffStartupFilter"/>).
/// </summary>
public sealed class BffCookieToBearerMiddleware
{
    private const int RefreshLockSeconds = 10;
    private const int MaxReReadAttempts = 3;

    private readonly RequestDelegate _next;
    private readonly BffRegistry _registry;
    private readonly ILogger<BffCookieToBearerMiddleware> _logger;

    public BffCookieToBearerMiddleware(
        RequestDelegate next,
        BffRegistry registry,
        ILogger<BffCookieToBearerMiddleware> logger)
    {
        _next = next;
        _registry = registry;
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

        // SECURITY: lz-authname selects WHICH pool scheme validates the Bearer. It must only ever be
        // set by Attach (below), never by the caller. Strip any inbound value unconditionally.
        req.Headers.Remove(BffConstants.AuthNameHeader);

        // Select the BFF instance for this request from the marker (default = tenantauth). The
        // marker only chooses which of the caller's OWN cookies to use, so it's not a trust vector;
        // strip it so it doesn't leak downstream.
        string? marker = req.Headers.TryGetValue(BffConstants.PoolMarkerHeader, out var mv) ? mv.ToString() : null;
        req.Headers.Remove(BffConstants.PoolMarkerHeader);
        var inst = _registry.ResolveByKey(marker);

        // Respect an explicit Authorization header (e.g. the /bff/ws-token holder).
        if (req.Headers.ContainsKey(BffConstants.AuthorizationHeader))
            return;

        if (!req.Cookies.TryGetValue(inst.Options.CookieName, out var cookieValue) || string.IsNullOrEmpty(cookieValue))
            return;

        var payload = inst.Cookie.Unprotect(cookieValue);
        if (payload is null || string.IsNullOrEmpty(payload.AccessToken))
            return;

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var remaining = payload.Exp - now;

        if (remaining > inst.Options.AccessTokenSkewSeconds)
        {
            Attach(context, payload.AccessToken, inst);
            return;
        }

        // Within skew (or expired): attempt a refresh. Pass through unauthenticated on failure.
        var refreshed = await TryRefreshAsync(context, payload, inst).ConfigureAwait(false);
        if (refreshed is not null)
            Attach(context, refreshed.AccessToken, inst);
        // else: do not attach; downstream sees an anonymous request.
    }

    private void Attach(HttpContext context, string accessToken, BffInstance inst)
    {
        // We MUST stamp the matching lz-authname whenever we attach a Bearer (the host's
        // multi-scheme JWT middleware selects the pool scheme from it). If we cannot resolve
        // an authname, refuse to attach rather than emit a Bearer the host can't route.
        var authName = ResolveAuthName(inst.Options);
        if (string.IsNullOrEmpty(authName))
            return;

        context.Request.Headers[BffConstants.AuthorizationHeader] = $"Bearer {accessToken}";
        context.Request.Headers[BffConstants.AuthNameHeader] = authName;
    }

    private static string? ResolveAuthName(BffOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.AuthName))
            return options.AuthName;
        var provider = options.Provider.ToString().ToLowerInvariant();
        return string.IsNullOrWhiteSpace(provider) ? null : provider;
    }

    private async Task<BffCookiePayload?> TryRefreshAsync(HttpContext context, BffCookiePayload payload, BffInstance inst)
    {
        if (string.IsNullOrEmpty(payload.Sid))
            return null;

        // 1) Try to win the refresh lock. The fencing token is non-null only for the winner.
        var lockToken = await inst.Store.TryAcquireRefreshLockAsync(payload.Sid, RefreshLockSeconds, context.RequestAborted)
            .ConfigureAwait(false);

        if (lockToken is null)
        {
            // LOCK-LOSER: another request is rotating. We must NOT re-refresh the refresh token
            // (that double-rotates the family and invalidates the session). Instead, wait for the
            // winner to publish its freshly minted access token, then ADOPT it (re-issue our cookie).
            for (var i = 0; i < MaxReReadAttempts; i++)
            {
                await Task.Delay(50, context.RequestAborted).ConfigureAwait(false);

                var rec = await inst.Store.GetAsync(payload.Sid, context.RequestAborted).ConfigureAwait(false);
                if (rec is null || rec.Revoked)
                    return null;

                var lockCleared = !rec.LockUntil.HasValue
                    || rec.LockUntil.Value < DateTimeOffset.UtcNow.ToUnixTimeSeconds();

                // Adopt the winner's published access token if it is newer than our cookie's.
                if (!string.IsNullOrEmpty(rec.AccessToken)
                    && rec.AccessTokenExp.HasValue
                    && rec.AccessTokenExp.Value > payload.Exp)
                {
                    return ReissueFromStoredAccessToken(context, payload, rec.AccessToken!, rec.AccessTokenExp.Value, rec.Ttl, inst);
                }

                // Lock cleared but no usable published token (e.g. winner failed). Stop re-refreshing
                // here — let the request pass through unauthenticated and the client retry.
                if (lockCleared)
                    return null;
            }
            return null;
        }

        // 2) WINNER: we hold the lock. Load the row, ensure not revoked, then refresh + publish.
        var record = await inst.Store.GetAsync(payload.Sid, context.RequestAborted).ConfigureAwait(false);
        if (record is null || record.Revoked || string.IsNullOrEmpty(record.RefreshToken))
            return null;

        return await DoRefreshAndReissueAsync(context, payload, record.RefreshToken, lockToken, record.Ttl, inst)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Re-issue the session cookie from an access token (either freshly refreshed by us, or
    /// published by the lock winner). Preserves the absolute session expiry (<paramref name="ttlEpoch"/>)
    /// so a silent refresh never drops/extends the absolute logoff window.
    /// </summary>
    private BffCookiePayload ReissueFromStoredAccessToken(
        HttpContext context, BffCookiePayload payload, string accessToken, long accessTokenExp, long ttlEpoch, BffInstance inst)
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
            var cookie = inst.Cookie.Protect(newPayload);
            var expires = ttlEpoch > 0 ? DateTimeOffset.FromUnixTimeSeconds(ttlEpoch) : (DateTimeOffset?)null;
            context.Response.Cookies.Append(inst.Options.CookieName, cookie, BffCookieBuilder.Session(inst.Options, expires));
        }

        return newPayload;
    }

    private async Task<BffCookiePayload?> DoRefreshAndReissueAsync(
        HttpContext context, BffCookiePayload payload, string refreshToken, string lockToken, long ttlEpoch, BffInstance inst)
    {
        if (string.IsNullOrEmpty(refreshToken))
            return null;

        BffTokenResult tokens;
        try
        {
            tokens = await inst.Tokens.RefreshAsync(refreshToken, context.RequestAborted).ConfigureAwait(false);
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
            await inst.Store.UpdateRefreshAsync(payload.Sid, newRt, tokens.AccessToken, tokens.ExpiresAtEpoch, lockToken, context.RequestAborted)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Persist failed (lost/expired fencing lock, or transient store error). The provider
            // already rotated the rt, so the stored rt is now stale — do NOT hand back a cookie whose
            // session row is inconsistent. Pass through unauthenticated; the client retries.
            _logger.LogDebug(ex, "BFF refresh-token persist failed (fencing condition or store error); passing through unauthenticated.");
            return null;
        }

        return ReissueFromStoredAccessToken(context, payload, tokens.AccessToken, tokens.ExpiresAtEpoch, ttlEpoch, inst);
    }
}
