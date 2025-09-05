namespace LazyMagic.Client.ViewModels;

public static class ConfigureLazyMagicClientViewModels
{
    public static IServiceCollection AddLazyMagicClientViewModels(this IServiceCollection services)
    {
        //LazyMagicClientViewModelsRegisterFactories.LazyMagicClientViewModelsRegister(services); // Run generated registration code
        services.AddSingleton<ITenantConfigViewModel, TenantConfigViewModel>();
        return services;
    }
}
