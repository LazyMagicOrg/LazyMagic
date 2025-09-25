namespace WASMApp;

public static class ConfigApp
{

    public static IServiceCollection AddApp(this IServiceCollection services)
    {

        services.AddBlazorUI(); // The BlazorUI folder contains the BlazorUI namespace

        services.AddViewModels(); // The BlazorTest.ViewModels project contains the ViewModels namespace

        return services;
    }
}
