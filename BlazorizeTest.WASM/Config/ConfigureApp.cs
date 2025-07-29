namespace BlazorTest;
public static class ConfigureApp
{
    public static IServiceCollection AddApp(this IServiceCollection services)
    {
        // Note that all LazyMagic AddLazyMagic* methods use TryAdd* methods
        // so you can register your own implementations of individual services
        services.AddLazyMagicAuthCognito(); // Viewmodels used by auth with Cognito

        services.AddBlazorUI(); // The BlazorUI folder contains the BlazorUI namespace

        services.AddViewModels(); // The BlazorTest.ViewModels project contains the ViewModels namespace
        return services;
    }

}
