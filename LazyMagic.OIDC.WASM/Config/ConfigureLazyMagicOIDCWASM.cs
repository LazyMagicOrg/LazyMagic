using System.Runtime.CompilerServices;

namespace LazyMagic.OIDC.WASM;

public static class ConfigureLazyMagicOIDCWASM
{
    public static IServiceCollection AddLazyMagicOIDCWASM(this IServiceCollection services)
    {
        services.TryAddScoped<IOIDCService, BlazorOIDCService>();
        services.TryAddSingleton<IRememberMeService, BlazorRememberMeService>();
        services.TryAddTransient<UserProfileViewModel>();

        services.TryAddSingleton<IPostConfigureOptions<RemoteAuthenticationOptions<OidcProviderOptions>>,
                                      DynamicOidcPostConfigureOptions>();

        // Register the lazy OIDC configuration as a singleton
        services.TryAddSingleton<IOidcConfig>(provider =>
        {
            var lzHost = provider.GetRequiredService<ILzHost>();
            var logger = provider.GetService<ILogger<LazyOidcConfig>>();
            return new LazyOidcConfig(lzHost, logger);
        });

        // Register a configuration service that will hold the dynamic config once loaded
        services.TryAddSingleton<DynamicOidcConfigHolder>();

        // Register a service to provide configuration values from dynamic config
        services.TryAddSingleton<IDynamicConfigurationProvider>(provider =>
        {
            var oidcConfig = provider.GetRequiredService<IOidcConfig>();
            var logger = provider.GetRequiredService<ILogger<DynamicConfigurationProvider>>();
            return new DynamicConfigurationProvider(oidcConfig, logger);
        });

        // Register profile management service
        services.TryAddSingleton<IProfileManagementService, BlazorProfileManagementService>();

        services.AddLazyMagicOIDCBase(); // Base OIDC services

        return services;
    }

    public static async Task LoadConfiguration(WebAssemblyHost host)
    {
        Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] LoadConfiguration started");

        // Load and apply OIDC configuration now that _appConfig is available
        // This MUST complete before any authentication attempts
        var oidcConfig = host.Services.GetRequiredService<IOidcConfig>();
        var configHolder = host.Services.GetRequiredService<DynamicOidcConfigHolder>();
        // Load configuration directly (not in Task.Run) to ensure it completes before app starts
        try
        {
            if (oidcConfig is LazyOidcConfig lazyConfig)
            {
                Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] Getting ILzHost");
                var lzHost = host.Services.GetRequiredService<ILzHost>();
                
                Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] Starting LoadAuthConfigsAsync");
                var authConfigs = await lazyConfig.LoadAuthConfigsAsync();
                Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] Completed LoadAuthConfigsAsync");
                
                Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] Starting GetSelectedAuthConfigAsync");
                var selectedAuth = await lazyConfig.GetSelectedAuthConfigAsync();
                Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] Completed GetSelectedAuthConfigAsync");

                Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] Looking for selectedAuth '{selectedAuth}' in available configs: {string.Join(", ", authConfigs.Keys)}");
                
                if (authConfigs.TryGetValue(selectedAuth, out var authConfig))
                {
                    Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] Found matching config, setting configuration in holder");
                    // Store the configuration in the holder
                    // Get current URL efficiently for redirect URI calculation
                    var currentUrl = await GetCurrentBaseUrlAsync(host.Services, lzHost.AppUrl);
                    configHolder.SetConfigurationFromAuthConfig(authConfig, currentUrl);

                    // Force OIDC post-configuration to happen NOW instead of waiting for first authentication
                    try
                    {
                        Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] Getting IOptionsSnapshot");
                        var oidcOptions = host.Services.GetRequiredService<IOptionsSnapshot<RemoteAuthenticationOptions<OidcProviderOptions>>>();
                        Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] Triggering post-configuration");
                        var options = oidcOptions.Value; // This will trigger post-configuration
                        Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] Post-configuration completed");
                        
                        // Initialize RememberMe service and handle token cleanup
                        Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] Starting RememberMe initialization");
                        var rememberMeService = host.Services.GetRequiredService<IRememberMeService>();
                        await rememberMeService.InitializeAuthenticationAsync();
                        Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] RememberMe initialization completed");
                    }
                    catch
                    {
                        // Post-configuration error - authentication may not work properly
                    }
                }
                else
                {
                    Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] ❌ Could not find selectedAuth '{selectedAuth}' in available configs");
                    
                    // Try case-insensitive lookup
                    var matchingKey = authConfigs.Keys.FirstOrDefault(k => k.Equals(selectedAuth, StringComparison.OrdinalIgnoreCase));
                    if (matchingKey != null)
                    {
                        Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] Found case-insensitive match: '{matchingKey}'");
                        
                        // Get current URL efficiently for redirect URI calculation
                        var currentUrl = await GetCurrentBaseUrlAsync(host.Services, lzHost.AppUrl);
                        configHolder.SetConfigurationFromAuthConfig(authConfigs[matchingKey], currentUrl);
                    }
                    else
                    {
                        Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] No case-insensitive match found either");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] Config load error: {ex.Message}");
            // Config load error - authentication will not work
        }
        
        Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] LoadConfiguration completed");
    }

    // Cached base URL to avoid repeated JavaScript calls  
    private static string? _cachedBaseUrl = null;
    
    /// <summary>
    /// Efficiently gets the current base URL including subpath, with caching
    /// </summary>
    private static async Task<string> GetCurrentBaseUrlAsync(IServiceProvider services, string fallbackUrl)
    {
        // Return cached URL if available
        if (!string.IsNullOrEmpty(_cachedBaseUrl))
        {
            Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] Using cached base URL: {_cachedBaseUrl}");
            return _cachedBaseUrl;
        }
        
        var jsRuntime = services.GetService<IJSRuntime>();
        if (jsRuntime != null)
        {
            try
            {
                Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] Getting base URL from browser...");
                
                // Use a simpler, faster JavaScript call
                var origin = await jsRuntime.InvokeAsync<string>("eval", "window.location.origin");
                var pathname = await jsRuntime.InvokeAsync<string>("eval", "window.location.pathname");
                
                // For OIDC redirect URIs, we want the base app URL, not the current page path
                // Skip AuthPage paths since those are temporary OAuth navigation paths
                string basePath = "";
                if (!pathname.Contains("/AuthPage") && !pathname.Contains("/authentication"))
                {
                    // Only use the path if it's not an auth-related path
                    basePath = pathname.EndsWith("/") ? pathname.TrimEnd('/') : 
                               pathname.Contains('/') ? pathname.Substring(0, pathname.LastIndexOf('/')) : "";
                }
                
                _cachedBaseUrl = origin + basePath;
                Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] Resolved and cached base URL: {_cachedBaseUrl}");
                return _cachedBaseUrl;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] Error getting dynamic URL: {ex.Message}");
            }
        }
        
        Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] Using fallback URL: {fallbackUrl}");
        _cachedBaseUrl = fallbackUrl;
        return fallbackUrl;
    }

}
