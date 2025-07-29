namespace BlazorUI;

public static class ConfigBlazorUI
{
    public static IServiceCollection AddBlazorUI(this IServiceCollection services)
    {
        // Add core Blazor components and services
        services.AddBootstrap5Providers();
        services.AddLazyMagicBlazor();
        services.AddBlazorise(options => { options.Immediate = true; });

        // Note that all LazyMagic AddLazyMagic* methods use TryAdd* methods
        // so you can register your own implementations of individual services 
        // before calling these AddLazyMagic* methods.
        services.AddLazyMagicBlazorise();// Blazorise Components 
        services.AddLazyMagicBlazoriseAuth(); // Blazorise Components used by auth 

        return services;
    }
}
