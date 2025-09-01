namespace LazyMagic.OIDC.Base;

public static class ConfigureLazyMagicOIDCBase
{
    public static IServiceCollection AddLazyMagicOIDCBase(this IServiceCollection services)
    {
        services.TryAddScoped<DynamicOidcConfigurationService>();
        services.TryAddTransient<IAuthenticationHandler, BearerTokenHandler>();
        Console.WriteLine("Added LazyMagic.OIDC.Base services");
        return services;
    }
}
