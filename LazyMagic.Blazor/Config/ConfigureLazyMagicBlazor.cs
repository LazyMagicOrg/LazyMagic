namespace LazyMagic.Blazor;

public static class ConfigureLazyMagicBlazor
{
    public static IServiceCollection AddLazyMagicBlazor(this IServiceCollection services)
    {
        // TryAdd only succeeds if the service is not already registered
        // It is used here to allow the calling programs to register their own
        // implementations of these classes and to avoid multiple registrations.
        services.TryAddSingleton<IConnectivityService, ConnectivityService>();
        services.TryAddSingleton<IOSAccess, BlazorOSAccess>();
        services.TryAddSingleton<ILzMessages, LzMessages>();
        services.TryAddSingleton<ILzClientConfig, LzClientConfig>();
        services.TryAddScoped<ClipboardService>();
        services.TryAddScoped<IResizeListener, ResizeListener>();
        services.TryAddSingleton<BrowserFingerprintService>();
        services.TryAddSingleton<ILzJsUtilities, LzJsUtilities>();

        if(!services.IsServiceRegistered<IMediaQueryService>())  services.AddMediaQueryService();
        if(!services.IsServiceRegistered<IResizeListener>()) services.AddResizeListener();
            
        return services;
    }
    public static bool IsServiceRegistered<TService>(this IServiceCollection services)
    {
        return services.Any(serviceDescriptor => serviceDescriptor.ServiceType == typeof(TService));
    }
}