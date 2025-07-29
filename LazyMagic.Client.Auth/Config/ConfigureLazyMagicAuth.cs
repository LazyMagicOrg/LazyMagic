namespace LazyMagic.Client.Auth;

public static class ConfigureLazyMagicClientAuth
{
    public static IServiceCollection AddLazyMagicClientAuth(this IServiceCollection services)
    {
        // TryAdd only succeeds if the service is not already registered
        // It is used here to allow the calling programs to register their own
        // implementations of these classes prior to calling this method.
        services.TryAddTransient<IAuthProcess, AuthProcess>();
        services.TryAddTransient<ILoginFormat, LoginFormat>();
        services.TryAddTransient<IEmailFormat, EmailFormat>();
        services.TryAddTransient<IPhoneFormat, PhoneFormat>();
        services.TryAddTransient<ICodeFormat, CodeFormat>();
        services.TryAddTransient<IPasswordFormat, PasswordFormat>();
        return services;
    }
}
