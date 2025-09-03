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
            return new DynamicConfigurationProvider(oidcConfig);
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
                    //configHolder.SetConfigurationFromAuthConfig(authConfig, builder.HostEnvironment.BaseAddress);
                    configHolder.SetConfigurationFromAuthConfig(authConfig, lzHost.AppUrl);

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
                        configHolder.SetConfigurationFromAuthConfig(authConfigs[matchingKey], lzHost.AppUrl);
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

}
