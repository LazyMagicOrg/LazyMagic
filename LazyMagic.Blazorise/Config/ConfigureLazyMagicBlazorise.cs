using LazyMagic.Blazor;

namespace LazyMagic.Blazorise;
public static class ConfigureLazyMagicBlazorise
{
    public static IServiceCollection AddLazyMagicBlazorise(this IServiceCollection services)
    {
        services.AddLazyMagicBlazor();

        // Blazorise doesn't use TryAdd* so we check if BlazoriseOptions is already registered
        var blazorRegistered = services.Any(ServiceDescriptor => ServiceDescriptor.ServiceType == typeof(BlazoriseOptions));
        if(!blazorRegistered)
        {
            services
                .AddBlazorise(options => { options.Immediate = true; })
                .AddBootstrap5Providers();
        }

        return services;
    }
}