namespace LazyMagic.MudBlazor;

public static class ConfigureLazyMagicMudBlazorAuth
{
    public static IServiceCollection AddLazyMagicMudBlazorAuth(this IServiceCollection services)
    {
        // LazyMagic uses TryAdd so we can just call it without checking if individual services are
        // already registered.
        services.AddLazyMagicBlazor();

        // MudBlazor uses TryAdd so we can just call it without checking if individual services are 
        // already registered.
        services.AddMudServices();

        return services;
    }
}
