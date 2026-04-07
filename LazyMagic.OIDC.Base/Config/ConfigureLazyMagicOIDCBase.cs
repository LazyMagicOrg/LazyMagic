namespace LazyMagic.OIDC.Base;

public static class ConfigureLazyMagicOIDCBase
{
    public static IServiceCollection AddLazyMagicOIDCBase(this IServiceCollection services)
    {
        services.TryAddScoped<DynamicOidcConfigurationService>();
        services.TryAddScoped<IAuthenticationHandler, BearerTokenHandler>();
        services.TryAddSingleton<IOpenIdDiscoveryService, OpenIdDiscoveryService>();
        Console.WriteLine("Added LazyMagic.OIDC.Base services");
        return services;
    }
}
