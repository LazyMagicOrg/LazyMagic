namespace LazyMagic.Client.Auth;

public static class ConfigureLazyMagicAuth
{
    public static IServiceCollection AddLazyMagicAuth(this IServiceCollection services)
    {
        // TryAdd only succeeds if the service is not already registered
        // It is used here to allow the calling programs to register their own
        // implementations of these classes.
        // Note: LzHost must be registered in the WASM Program.cs file so the current 
        // base url can be captured. MAUI programs are not loaded from a URL so they 
        // read their API params from a configuration file specific to the client build,
        // see the RunConfig class.
        services.TryAddTransient<IAuthProcess, AuthProcess>();
        services.TryAddTransient<ILoginFormat, LoginFormat>();
        services.TryAddTransient<IEmailFormat, EmailFormat>();
        services.TryAddTransient<IPhoneFormat, PhoneFormat>();
        services.TryAddTransient<ICodeFormat, CodeFormat>();
        services.TryAddTransient<IPasswordFormat, PasswordFormat>();
        services.TryAddSingleton<ILzHost, LzHost>();
        return services;
    }
}
