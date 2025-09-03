namespace BlazorUI;

public static class ConfigBlazorUI
{
    public static IServiceCollection AddBlazorUI(this IServiceCollection services)
    {
        // Note that all LazyMagic AddLazyMagic* methods use TryAdd* methods
        // so you can register your own implementations of individual services 
        // before calling these AddLazyMagic* methods.
        services.AddLazyMagicBlazorise();// Blazorise Components 
        return services;
    }
}
