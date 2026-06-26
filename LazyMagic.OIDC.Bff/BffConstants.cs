namespace LazyMagic.OIDC.Bff;

/// <summary>Shared header/cookie/scheme names for the BFF.</summary>
public static class BffConstants
{
    /// <summary>Custom non-safelisted CSRF header required on state-changing endpoints (§8.12).</summary>
    public const string CsrfHeader = "X-CSRF";

    /// <summary>Expected CSRF header value.</summary>
    public const string CsrfHeaderValue = "1";

    /// <summary>Header the host's multi-scheme JWT middleware reads to pick the Cognito pool scheme.</summary>
    public const string AuthNameHeader = "lz-authname";

    /// <summary>Standard bearer authorization header.</summary>
    public const string AuthorizationHeader = "Authorization";

    /// <summary>The short-lived login transaction cookie name.</summary>
    public const string TransactionCookieName = "__bff_txn";

    /// <summary>Short-lived cookie carrying the app's post-logout destination across the IdP
    /// sign-out round-trip (the IdP only redirects back to the registered logout-callback URL).</summary>
    public const string LogoutReturnCookieName = "__bff_logout";

    /// <summary>Cookie auth scheme name registered for the BFF.</summary>
    public const string CookieScheme = "BffCookie";

    /// <summary>HostingStartup / package assembly name (for ASPNETCORE_HOSTINGSTARTUPASSEMBLIES).</summary>
    public const string AssemblyName = "LazyMagic.OIDC.Bff";
}
