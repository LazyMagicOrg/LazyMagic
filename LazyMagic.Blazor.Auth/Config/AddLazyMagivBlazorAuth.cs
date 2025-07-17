
namespace LazyMagic.Blazor;

public static class AddLazyMagivBlazorAuth
{
    public static IServiceCollection AddLazyMagicBlazorAuth(this IServiceCollection services)
    {
        services
            .AddLazyMagicBlazor();
        return services;
    }
}
