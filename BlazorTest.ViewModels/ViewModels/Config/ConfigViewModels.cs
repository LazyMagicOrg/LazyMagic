namespace ViewModels;

public static class ConfigViewModels
{
    public static IServiceCollection AddViewModels(this IServiceCollection services)
    {
        services.TryAddScoped<ISessionViewModel, SessionViewModel>();

        // The LazyMagic.Client.FactoryGenerator generates Factory classes
        // for ViewModels using the [Factory] attribute. To avoid using 
        // runtime reflection, we also generate the ViewModelsRegisterFactories class
        // which contains a static method ViewModelsRegister that registers
        // each generated ViewModel factory.
        ViewModelsRegisterFactories.ViewModelsRegister(services);

        services.AddLazyMagicClientViewModels(); 

        return services;
    }
}
