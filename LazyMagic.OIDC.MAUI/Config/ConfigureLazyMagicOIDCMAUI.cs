namespace LazyMagic.OIDC.MAUI;

public static class ConfigureLazyMagicOIDCMAUI
{
    public static IServiceCollection AddLazyMagicOIDCMAUI(this IServiceCollection services)
    {
        // We use TryAddSingleton to avoid overwriting existing registrations made to customize the UI
        services.TryAddSingleton<IAuthenticationUI,DefaultAuthenticationUI>();
        // Register token storage service
        services.TryAddSingleton<ITokenStorageService, TokenStorageService>();

        // Register WebView authentication provider
        services.TryAddTransient<IWebViewAuthenticationProvider, MauiWebViewAuthenticationProvider>();

        // Register OIDC service (MAUI implementation)
        services.TryAddSingleton<IOIDCService, MauiOIDCService>();

        // Register RememberMe service (MAUI implementation)
        services.TryAddSingleton<IRememberMeService, MauiRememberMeService>();

        // Register a service that will load OIDC configuration when first accessed
        services.TryAddSingleton<IOidcConfig>(provider => new LazyOidcConfig());

        // Register dynamic configuration provider (MAUI implementation)  
        services.TryAddSingleton<IDynamicConfigurationProvider, DynamicConfigurationProvider>();

        services.TryAddSingleton<AuthenticationStateProvider, LazyMagic.OIDC.MAUI.MauiAuthenticationStateProvider>();

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
