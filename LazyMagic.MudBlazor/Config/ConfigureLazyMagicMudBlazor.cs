using LazyMagic.Blazor;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace LazyMagic.MudBlazor;

public static class ConfigureLazyMagicMudBlazor
{
    public static IServiceCollection AddLazyMagicMudBlazor(this IServiceCollection services)
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
