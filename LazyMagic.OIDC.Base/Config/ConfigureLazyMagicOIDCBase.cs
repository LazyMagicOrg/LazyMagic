namespace LazyMagic.OIDC.Base;

public static class ConfigureLazyMagicOIDCBase
{
    public static IServiceCollection AddLazyMagicOIDCBase(this IServiceCollection services)
    {
        services.TryAddScoped<DynamicOidcConfigurationService>();
        services.TryAddScoped<IAuthenticationHandler, BearerTokenHandler>();
        return services;
    }
}
