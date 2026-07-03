namespace LazyMagic.OIDC.WASM.Bff;

/// <summary>
/// DI wiring for the ADDITIVE, opt-in client-side BFF auth mode. This is the BFF-mode
/// counterpart to <c>AddLazyMagicOIDCWASM()</c> — call it INSTEAD of the SPA-token wiring when
/// the app is configured for BFF. It registers:
///   - <see cref="BffAuthenticationStateProvider"/> as the <see cref="AuthenticationStateProvider"/>
///     (so <c>&lt;AuthorizeView&gt;</c> / <c>&lt;CascadingAuthenticationState&gt;</c> work),
///   - <c>AddAuthorizationCore()</c> (so <c>[Authorize]</c> / role policies resolve),
///   - the thin <see cref="BffOIDCService"/> as <see cref="IOIDCService"/> (the UI abstraction),
///   - the <see cref="BffCredentialsHandler"/> as <see cref="IAuthenticationHandler"/> (cookie +
///     <c>X-CSRF: 1</c>) so the existing API-client wiring picks it up unchanged, and
///   - a same-origin, cookie-credentialed <see cref="HttpClient"/> used by the provider/service
///     to reach the <c>/bff/*</c> endpoints.
///
/// Nothing here touches the SPA-token providers in LazyMagic.OIDC.WASM / .Base / .MAUI.
/// </summary>
public static class ConfigureLazyMagicOIDCWASMBff
{
    /// <summary>Default re-poll interval for the BFF session (MultiTenantAuth.md §8.14).</summary>
    public static readonly TimeSpan DefaultPollInterval = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Register the client-side BFF auth services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="bffBaseAddress">
    /// Same-origin base address the <c>/bff/*</c> endpoints are served from (typically the WASM
    /// host's base address). The BFF endpoints are reached relative to this.
    /// </param>
    /// <param name="pollInterval">
    /// Re-poll interval for <c>/bff/user</c>. Defaults to 5 minutes. Pass
    /// <see cref="TimeSpan.Zero"/> (or negative) to disable background re-polling.
    /// </param>
    /// <param name="postLogoutRedirectPath">
    /// Optional fixed app-relative path to land on AFTER logout (the BFF server fans it back to the
    /// originating host). E.g. employee-gated apps pass <c>/explore/home/</c> so logout returns to the
    /// public landing instead of bouncing back through the gated app to the login. Null = the page the
    /// user logged out from.
    /// </param>
    public static IServiceCollection AddLazyMagicOIDCWASMBff(
        this IServiceCollection services,
        string bffBaseAddress,
        string routePrefix = "/bff",
        TimeSpan? pollInterval = null,
        string? postLogoutRedirectPath = null)
    {
        if (string.IsNullOrWhiteSpace(bffBaseAddress))
            throw new ArgumentException("A same-origin BFF base address is required.", nameof(bffBaseAddress));

        var interval = pollInterval ?? DefaultPollInterval;
        var baseUri = new Uri(bffBaseAddress, UriKind.Absolute);

        // Multi-pool marker the credentials handler stamps on /AppApi calls so the apphost
        // cookie→Bearer bridge picks THIS pool's cookie. Null for the default tenantauth (/bff)
        // instance so StoreApp/AdminApp stay wire-identical; "cbff" for ConsumerApp.
        var poolMarker = routePrefix.Trim('/').Equals("bff", StringComparison.OrdinalIgnoreCase)
            ? null
            : routePrefix.Trim('/');

        // Authorization core so AuthorizeView / [Authorize] / role policies work.
        services.AddAuthorizationCore();

        // The cookie-credentialed handler. Registered as IAuthenticationHandler so the existing
        // LazyMagic API-client registration (IAuthenticationHandler.CreateHandler()) uses it in
        // BFF mode in place of the bearer handler.
        services.TryAddTransient<BffCredentialsHandler>(sp => new BffCredentialsHandler(poolMarker));
        services.TryAddTransient<IAuthenticationHandler>(sp => new BffCredentialsHandler(poolMarker));

        // Same-origin HttpClient the provider/service use to reach {prefix}/*.
        services.TryAddScoped<BffAuthenticationStateProvider>(sp =>
        {
            var http = new HttpClient(new BffCredentialsHandler(poolMarker) { InnerHandler = new HttpClientHandler() })
            {
                BaseAddress = baseUri
            };
            var logger = sp.GetRequiredService<ILogger<BffAuthenticationStateProvider>>();
            return new BffAuthenticationStateProvider(http, logger, interval, routePrefix);
        });

        // Surface the BFF provider as THE AuthenticationStateProvider.
        services.AddScoped<AuthenticationStateProvider>(
            sp => sp.GetRequiredService<BffAuthenticationStateProvider>());

        // The UI binds to IOIDCService. Build it with its own same-origin, cookie-credentialed
        // HttpClient so LogoutAsync can POST /bff/logout with the X-CSRF header (the destructive
        // logout is CSRF-protected and no longer reachable by a bare GET navigation).
        services.TryAddScoped<IOIDCService>(sp =>
        {
            var http = new HttpClient(new BffCredentialsHandler(poolMarker) { InnerHandler = new HttpClientHandler() })
            {
                BaseAddress = baseUri
            };
            var authStateProvider = sp.GetRequiredService<AuthenticationStateProvider>();
            var navigation = sp.GetRequiredService<NavigationManager>();
            var logger = sp.GetRequiredService<ILogger<BffOIDCService>>();
            return new BffOIDCService(authStateProvider, navigation, http, logger, routePrefix, postLogoutRedirectPath);
        });

        // The shared LoginDisplay also injects IRememberMeService + IProfileManagementService.
        // In BFF mode the browser holds no tokens (nothing to persist/clear client-side) and
        // password/profile management is a server/IdP concern — register minimal BFF impls so
        // the UI resolves these dependencies and renders. (The SPA impls depend on the
        // client-side dynamic-config / token-storage stack, which BFF mode does not wire.)
        services.TryAddScoped<IRememberMeService, BffRememberMeService>();
        services.TryAddScoped<IProfileManagementService, BffProfileManagementService>();

        return services;
    }
}
