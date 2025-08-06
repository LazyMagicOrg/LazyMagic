namespace ViewModels;

public static class ConfigViewModels
{
    public static IServiceCollection AddViewModels(this IServiceCollection services)
    {
        // ISessionsViewModel serves as the global app state. It manages multiple 
        // sessions, each represented by a SessionViewModel. This is useful for 
        // POS terminal apps where multiple sessions can be active at once. PWAa 
        // and mobile apps are generally single session apps so there will be only
        // a single SessionViewModel for those app types.
        services.AddSingleton<ISessionsViewModel, SessionsViewModel>();

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
