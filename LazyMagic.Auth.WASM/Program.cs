namespace LazyMagic.Auth.WASM;

public class Program
{
    private static JObject? _appConfig;
    private static LzHost? LzHost;

    private static void LogMessage(string methodName, string message)
    {
        Console.WriteLine($"[{methodName}][{DateTime.UtcNow:HH:mm:ss.fff}] {message}");
    }

    public static async Task Main(string[] args)
    {
        var builder = WebAssemblyHostBuilder.CreateDefault(args);
        builder.RootComponents.Add<Main>("#main");
        builder.RootComponents.Add<HeadOutlet>("head::after");

        var hostEnvironment = builder.HostEnvironment;
        var isLocal = false;
        var useLocalhostApi = false;

        switch (hostEnvironment.Environment)
        {
            case "Production":
                LogMessage("Main", "Loaded from CloudFront");
                builder.Logging.SetMinimumLevel(LogLevel.Warning);
                break;
            default:
                LogMessage("Main", "Development environment");
                builder.Logging.SetMinimumLevel(LogLevel.Information);
                isLocal = true;
                var envVar = hostEnvironment.Environment;
                if (envVar.Contains("Localhost"))
                    useLocalhostApi = true;
                break;
        }

        // Configure logging
        builder.Logging.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Debug);
        builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);

        // Register auth app services
        builder.Services.AddAuthApp();

        // Add LazyMagic OIDC authentication
        builder.Services.AddLazyMagicOIDCWASM();
        builder.AddLazyMagicOIDCWASMBuilder();

        var host = builder.Build();

        // Wait for the page to fully load
        var jsRuntime = host.Services.GetRequiredService<IJSRuntime>();
        await WaitForPageLoad(jsRuntime);

        // Get app config
        _appConfig = await GetAppConfigAsync(jsRuntime);
        if (_appConfig == null)
        {
            LogMessage("Main", "Error loading app config. Exiting.");
            return;
        }

        // Initialize LzHost
        var lzHost = host.Services.GetRequiredService<ILzHost>();
        if (lzHost is LzHost lzHostImpl)
        {
            LzHost = lzHostImpl;
        }

        // Load OIDC configuration
        await ConfigureLazyMagicOIDCWASM.LoadConfiguration(host);

        await host.RunAsync();
    }

    private static async Task<JObject?> GetAppConfigAsync(IJSRuntime jsRuntime)
    {
        try
        {
            string jsonString = await jsRuntime.InvokeAsync<string>(
                "eval",
                "JSON.stringify(window.appConfig)"
            );
            return JObject.Parse(jsonString);
        }
        catch (Exception ex)
        {
            LogMessage("GetAppConfigAsync", $"Error fetching app config: {ex.Message}");
            return null;
        }
    }

    private static async Task WaitForPageLoad(IJSRuntime jsRuntime)
    {
        const int maxWaitTimeMs = 10000;
        const int checkIntervalMs = 100;

        var totalWaitTime = 0;
        while (totalWaitTime < maxWaitTimeMs)
        {
            try
            {
                var isLoaded = await jsRuntime.InvokeAsync<bool>("checkIfLoaded");
                if (isLoaded)
                {
                    LogMessage("WaitForPageLoad", "Page fully loaded.");
                    return;
                }
            }
            catch
            {
                // JS function not ready yet
            }

            await Task.Delay(checkIntervalMs);
            totalWaitTime += checkIntervalMs;
        }

        LogMessage("WaitForPageLoad", "Warning: Page load timeout reached.");
    }
}
