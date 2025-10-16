namespace LazyMagic.Blazor;

public static class ConfigureLazyMagicBlazor
{
    public static IServiceCollection AddLazyMagicBlazor(this IServiceCollection services)
    {
        // TryAdd only succeeds if the service is not already registered
        // It is used here to allow the calling programs to register their own
        // implementations of these classes and to avoid multiple registrations.
        services.TryAddScoped<BrowserFingerprintService>();
        services.TryAddScoped<ClipboardService>();
        services.TryAddScoped<IConnectivityService, ConnectivityService>();
        services.TryAddScoped<ILzClientConfig, LzClientConfig>();
        services.TryAddScoped<ILzJsUtilities, LzJsUtilities>();
        services.TryAddScoped<ILzMessages, LzMessages>();
        services.TryAddScoped<IOSAccess, BlazorOSAccess>();
        services.TryAddScoped<IResizeListener, ResizeListener>();

        if(!services.IsServiceRegistered<IMediaQueryService>())  services.AddMediaQueryService();
        if(!services.IsServiceRegistered<IResizeListener>()) services.AddResizeListener();
            
        return services;
    }
    public static bool IsServiceRegistered<TService>(this IServiceCollection services)
    {
        return services.Any(serviceDescriptor => serviceDescriptor.ServiceType == typeof(TService));
    }
}