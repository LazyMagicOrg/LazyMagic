namespace LazyMagic.Client.Auth;

public static class ConfigureLazyMagicAuthCognito
{
    public static IServiceCollection AddLazyMagicAuthCognito(this IServiceCollection services)
    {
        // TryAdd only succeeds if the service is not already registered
        // It is used here to allow the calling programs to register their own
        // implementations of these classes and to avoid multiple registrations.
        services.TryAddTransient<IAuthProvider, AuthProviderCognito>();
        services.AddSingleton<ILzHttpClient, LzHttpClientCognito>();
        services.AddLazyMagicClientAuth();
        return services;
    }

}
