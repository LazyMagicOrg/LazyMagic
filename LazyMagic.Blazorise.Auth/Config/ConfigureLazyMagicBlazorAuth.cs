using Microsoft.Extensions.DependencyInjection;

namespace LazyMagic.Blazorise;
public static class ConfigureLazyMagicBlazoriseAuth
{
    public static IServiceCollection AddLazyMagicBlazoriseAuth(this IServiceCollection services)
    {
        // Blazorise doesn't use TryAdd* so we check if BlazoriseOptions is already registered
        if (!services.IsServiceRegistered<BlazoriseOptions>())
        {
            services
                .AddBlazorise(options => { options.Immediate = true; })
                .AddBootstrap5Providers();
        }
        return services;
    }
    public static bool IsServiceRegistered<TService>(this IServiceCollection services)
    {
        return services.Any(serviceDescriptor => serviceDescriptor.ServiceType == typeof(TService));
    }
}