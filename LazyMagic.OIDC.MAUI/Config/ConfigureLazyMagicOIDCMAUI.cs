using LazyMagic.OIDC.Base.Services;
using Microsoft.Maui.Hosting;

namespace LazyMagic.OIDC.MAUI;

public static class ConfigureLazyMagicOIDCMAUI
{
    public static IServiceCollection AddLazyMagicOIDCMAUI(this IServiceCollection services)
    {
        // Add authorization services that AuthorizeView components need
        services.AddAuthorizationCore();
        
        // We use TryAddScoped to avoid overwriting existing registrations made to customize the UI
        services.TryAddScoped<IAuthenticationUI,DefaultAuthenticationUI>();
        // Register token storage service
        services.TryAddScoped<ITokenStorageService, TokenStorageService>();

        // Register WebView authentication provider
        services.TryAddTransient<IWebViewAuthenticationProvider, MauiWebViewAuthenticationProvider>();

        // Register token refresh service for automatic token renewal
        services.TryAddScoped<ITokenRefreshService, Services.MauiTokenRefreshService>();

        // Register OIDC service (MAUI implementation)
        services.TryAddScoped<IOIDCService, MauiOIDCService>();

        // Register RememberMe service (MAUI implementation)
        services.TryAddScoped<IRememberMeService, MauiRememberMeService>();

        // Register a service that will load OIDC configuration when first accessed
        // NOTE: Must be Singleton because it loads configuration asynchronously at startup
        // and caches it for the lifetime of the application. If this were Scoped, each
        // new scope would create a new instance without the loaded configuration.
        services.TryAddSingleton<IOidcConfig>(provider =>
        {
            var lzHost = provider.GetRequiredService<ILzHost>();
            var logger = provider.GetRequiredService<ILogger<LazyOidcConfig>>();
            return new LazyOidcConfig(lzHost, logger);
        });

        // Register dynamic configuration provider (MAUI implementation)  
        services.TryAddScoped<IDynamicConfigurationProvider, DynamicConfigurationProvider>();

        services.TryAddScoped<IProfileManagementService, MauiProfileManagementService>();

        services.TryAddScoped<AuthenticationStateProvider, LazyMagic.OIDC.MAUI.MauiAuthenticationStateProvider>();

        services.AddLazyMagicOIDCBase(); // Base OIDC services

        return services;
    }

    public static async Task LoadConfiguration(MauiApp app)
    {
        try
        {
            Console.WriteLine("[MauiProgram] Pre-loading OIDC configuration...");
            var oidcConfig = app.Services.GetRequiredService<IOidcConfig>();

            if (oidcConfig is LazyOidcConfig lazyConfig)
            {
                var authConfigs = await lazyConfig.LoadAuthConfigsAsync();
                var selectedAuth = await lazyConfig.GetSelectedAuthConfigAsync();

                Console.WriteLine($"[MauiProgram] OIDC config pre-loaded with {authConfigs.Count} configs, selected: {selectedAuth}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MauiProgram] ❌ Failed to pre-load OIDC config: {ex.Message}");
        }
    }
}
