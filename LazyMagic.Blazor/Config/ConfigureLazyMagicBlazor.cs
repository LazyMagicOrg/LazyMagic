namespace LazyMagic.Blazor;

public static class ConfigureLazyMagicBlazor
{
    public static IServiceCollection AddLazyMagicBlazor(this IServiceCollection services)
    {
        // TryAdd only succeeds if the service is not already registered
        // It is used here to allow the calling programs to register their own
        // implementations of these classes and to avoid multiple registrations.
        services.TryAddSingleton<BrowserFingerprintService>();
        services.TryAddSingleton<ClipboardService>();
        services.TryAddSingleton<IConnectivityService, ConnectivityService>();
        services.TryAddSingleton<ILzClientConfig, LzClientConfig>();
        services.TryAddSingleton<ILzJsUtilities, LzJsUtilities>();
        services.TryAddSingleton<ILzMessages, LzMessages>();
        services.TryAddSingleton<IOSAccess, BlazorOSAccess>();
        services.TryAddSingleton<IResizeListener, ResizeListener>();

        if(!services.IsServiceRegistered<IMediaQueryService>())  services.AddMediaQueryService();
        if(!services.IsServiceRegistered<IResizeListener>()) services.AddResizeListener();
            
        return services;
    }
    public static bool IsServiceRegistered<TService>(this IServiceCollection services)
    {
        return services.Any(serviceDescriptor => serviceDescriptor.ServiceType == typeof(TService));
    }
}