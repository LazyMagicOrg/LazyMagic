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

    /// <summary>Options for the short-lived (5-min) login transaction cookie.</summary>
    public static CookieOptions Transaction(BffOptions options) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Lax,
        Domain = string.IsNullOrWhiteSpace(options.CookieDomain) ? null : options.CookieDomain,
        Path = "/",
        Expires = DateTimeOffset.UtcNow.AddMinutes(5),
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
