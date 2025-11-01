namespace LazyMagic.Auth.WASM;

public static class ConfigureAuthApp
{
    public static IServiceCollection AddAuthApp(this IServiceCollection services)
    {
        // Register LazyMagic core services
        services.TryAddScoped<ILzJsUtilities, LzJsUtilities>();

        // Note: ISubtenantService and SubtenantService are in LazyMagic.Client.Base
        // and should be registered there or by LazyMagic client services

        // Register LzHost (will be configured after app config is loaded)
        services.TryAddScoped<ILzHost>(sp =>
        {
            return new LzHost(
                appPath: "/auth/",
                appUrl: "",  // Will be set from config
                androidAppUrl: "",
                remoteApiUrl: "",
                localApiUrl: "",
                assetsUrl: "",
                authConfigName: "",
                isMAUI: false,
                isAndroid: false,
                isLocal: false,
                useLocalhostApi: false);
        });

        // Register auth orchestrator
        services.AddScoped<AuthOrchestrator>();

        // Register token exchange service
        services.AddScoped<TokenExchangeService>();

        // Register NavigationManager wrapper
        services.AddScoped<NavigationManager>(sp =>
            sp.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>());

        return services;
    }
}
