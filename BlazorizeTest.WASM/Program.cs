using Blazorise;
using Blazorise.Bootstrap5;
using Microsoft.JSInterop;
using System.Diagnostics;
using Newtonsoft.Json.Linq;

namespace BlazorizeTest.WASM;

public class Program
{
    private static JObject? _appConfig;
    public static async Task Main(string[] args)
    {
        var builder = WebAssemblyHostBuilder.CreateDefault(args);
        builder.RootComponents.Add<App>("#app");
        builder.RootComponents.Add<HeadOutlet>("head::after");

        // We use the launchSettings.json profile ASPNETCORE_ENVIRONMENT environment variable
        // to determine the host addresses for the API host and Tenancy host.
        //
        // Examples:
        // Production: "ASPNETCORE_ENVIRONMENT": "Production" 
        //  The API and Tenancy host are the same and are the base address of the cloudfront distribution
        //  the app is loaded from.
        //
        // Debug against LocalHost API:
        //  "ASPNETCORE_ENVIRONMENT": "Localhost"
        //  useLocalhostApi will be true else false


        var hostEnvironment = builder.HostEnvironment;
        var apiUrl = string.Empty;
        var assetsUrl = string.Empty;
        var isLocal = false; // Is the code being served from a local development host?
        var useLocalhostApi = false;    
        switch (hostEnvironment.Environment)
        {
            case "Production":
                Console.WriteLine("Production environment");
                break;
            default:
                Console.WriteLine("Development environment");
                isLocal = true;
                var envVar = hostEnvironment.Environment;
                if (envVar.Contains("Localhost"))
                    useLocalhostApi = true;
                break;
        }


        builder.Services
        .AddSingleton<ILzMessages, LzMessages>()
        .AddSingleton<ILzClientConfig, LzClientConfig>()
        .AddSingleton(sp => new HttpClient { BaseAddress = new Uri((string)_appConfig!["assetsUrl"]!) })
        .AddSingleton<BlazorInternetConnectivity>()
        .AddSingleton<IBlazorInternetConnectivity>(sp => sp.GetRequiredService<BlazorInternetConnectivity>())
        .AddSingleton<IInternetConnectivitySvc>(sp => sp.GetRequiredService<BlazorInternetConnectivity>())
        .AddSingleton<ILzHost>(sp => new LzHost(
            androidAppUrl: (string)_appConfig!["androidAppUrl"]!, // android app url 
            remoteApiUrl: (string)_appConfig!["remoteApiUrl"]!,  // api url
            localApiUrl: (string)_appConfig!["localApiUrl"]!, // local api url
            assetsUrl: (string)_appConfig!["assetsUrl"]!, // tenancy assets url
            isMAUI: false, // sets isWASM to true
            isAndroid: false,
            isLocal: isLocal,
            useLocalhostApi: useLocalhostApi))
        .AddSingleton<IOSAccess, BlazorOSAccess>()
        .AddLazyMagicAuthCognito()
        .AddSingleton<ISessionsViewModel, SessionsViewModel>()
        .AddBlazorise(options => { options.Immediate = true; })
        .AddBootstrap5Providers();
        RegisterFactories.Register(builder.Services);

        var host = builder.Build();
        // Wait for the page to fully load to finish up the Blazor app configuration
        var jsRuntime = host.Services.GetRequiredService<IJSRuntime>();

        await WaitForPageLoad(jsRuntime);

        // Now we can retrieve the app config information loaded with the page
        _appConfig = await GetAppConfigAsync(jsRuntime);

        if(_appConfig == null)
                    {
            Console.WriteLine("Error loading app config. Exiting.");
            return;
        }   

        await host.RunAsync();

    }
    private static async Task LoadStaticAssets(IJSRuntime jsRuntime)
    {
        await jsRuntime.InvokeVoidAsync("loadStaticAssets");
    }

    private static async Task<JObject> GetAppConfigAsync(IJSRuntime jsRuntime)
    {
        try
        {
            // Use IJSRuntime to evaluate JavaScript and get the JSON string
            string jsonString = await jsRuntime.InvokeAsync<string>(
                "eval",
                "JSON.stringify(window.appConfig)"
            );

            // Parse the JSON string to a JObject
            return JObject.Parse(jsonString);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching app config: {ex.Message}");
            return null;
        }
    }

    private static async Task WaitForPageLoad(IJSRuntime jsRuntime)
    {
        const int maxWaitTimeMs = 10000; // Maximum wait time of 10 seconds
        const int checkIntervalMs = 100; // Check every 100ms

        var totalWaitTime = 0;
        while (totalWaitTime < maxWaitTimeMs)
        {
            var isLoaded = await jsRuntime.InvokeAsync<bool>("checkIfLoaded");
            if (isLoaded)
            {
                Console.WriteLine("Page fully loaded.");
                return;
            }

            await Task.Delay(checkIntervalMs);
            totalWaitTime += checkIntervalMs;
        }

        Console.WriteLine("Warning: Page load timeout reached.");
    }
}
