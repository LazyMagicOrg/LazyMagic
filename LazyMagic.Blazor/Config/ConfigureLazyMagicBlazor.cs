namespace LazyMagic.Blazor;

public static class ConfigureLazyMagicBlazor
{
    public static IServiceCollection AddLazyMagicBlazor(this IServiceCollection services)
    {
        services.TryAddSingleton<IConnectivityService, ConnectivityService>();
        services.TryAddSingleton<IInternetConnectivitySvc>(sp => sp.GetRequiredService<IConnectivityService>());
        services.TryAddSingleton<IOSAccess, BlazorOSAccess>();
        services.TryAddSingleton<ILzMessages, LzMessages>();
        services.TryAddSingleton<ILzClientConfig, LzClientConfig>();
        services.TryAddScoped<ClipboardService>();
        services.TryAddScoped<IResizeListener, ResizeListener>();
        services.TryAddSingleton<BrowserFingerprintService>();
        services.TryAddSingleton<ILzJsUtilities, LzJsUtilities>();
        services
            .AddMediaQueryService()
            .AddResizeListener();
            
        return services;
    }
}