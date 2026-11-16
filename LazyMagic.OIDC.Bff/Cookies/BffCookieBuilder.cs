using Microsoft.AspNetCore.Http;

namespace LazyMagic.OIDC.Bff;

/// <summary>
/// Builds <see cref="CookieOptions"/> with the BFF security attributes (§8.12):
/// <c>HttpOnly; Secure; SameSite=Lax; Domain={CookieDomain}; Path=/</c>.
/// </summary>
public static class BffCookieBuilder
{
    /// <summary>Options for the session cookie.</summary>
    public static CookieOptions Session(BffOptions options, DateTimeOffset? expires = null) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Lax,
        Domain = string.IsNullOrWhiteSpace(options.CookieDomain) ? null : options.CookieDomain,
        Path = "/",
        Expires = expires,
        IsEssential = true,
    };

    /// <summary>
    /// HOW LONG A SIGN-IN MAY TAKE, from /bff/login to the identity provider's redirect back to /bff/callback: the
    /// transaction cookie's life and the time limit its encryption enforces. 15 minutes since 2026-11-16 (was 5): a
    /// first sign-in that sets a new password or enrols MFA took 8 minutes on a deployed pool and was refused.
    /// </summary>
    public static readonly TimeSpan TransactionLifetime = TimeSpan.FromMinutes(15);

    /// <summary>
    /// How long the sign-in RESTART cookie lives: where a sign-in started, kept past the transaction so a callback that
    /// finds the transaction gone can send the user back to that page to sign in again, rather than to an error.
    /// </summary>
    public static readonly TimeSpan RestartLifetime = TimeSpan.FromHours(1);

    /// <summary>Options for the short-lived login transaction cookie (<see cref="TransactionLifetime"/>).</summary>
    public static CookieOptions Transaction(BffOptions options) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Lax,
        Domain = string.IsNullOrWhiteSpace(options.CookieDomain) ? null : options.CookieDomain,
        Path = "/",
        Expires = DateTimeOffset.UtcNow.Add(TransactionLifetime),
        IsEssential = true,
    };

    /// <summary>Options for the sign-in restart cookie (<see cref="RestartLifetime"/>).</summary>
    public static CookieOptions Restart(BffOptions options) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Lax,
        Domain = string.IsNullOrWhiteSpace(options.CookieDomain) ? null : options.CookieDomain,
        Path = "/",
        Expires = DateTimeOffset.UtcNow.Add(RestartLifetime),
        IsEssential = true,
    };

    /// <summary>Options used to delete a cookie (Max-Age=0) — must match Domain/Path of the original.</summary>
    public static CookieOptions Delete(BffOptions options) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Lax,
        Domain = string.IsNullOrWhiteSpace(options.CookieDomain) ? null : options.CookieDomain,
        Path = "/",
        Expires = DateTimeOffset.UnixEpoch,
        IsEssential = true,
    };
}
