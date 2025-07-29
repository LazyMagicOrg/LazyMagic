
using LazyMagic.Shared;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LazyMagic.Blazor;

public static class ConfigureLazyMagicBlazorAuth
{
    public static IServiceCollection AddLazyMagicBlazorAuth(this IServiceCollection services)
    {
        services.AddLazyMagicBlazor();
        return services;
    }
}
