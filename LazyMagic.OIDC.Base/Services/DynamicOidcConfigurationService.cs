namespace LazyMagic.OIDC.Base;

/// <summary>
/// Platform-agnostic service for loading and configuring dynamic OIDC authentication
/// </summary>
public class DynamicOidcConfigurationService
{
    /// <summary>
    /// Loads OIDC configuration from a JSON endpoint
    /// </summary>
    /// <param name="baseAddress">Base address for the configuration endpoint</param>
    /// <param name="configUrl">Configuration endpoint path (defaults to "config")</param>
    /// <param name="defaultAuthConfig">Default auth config to select (defaults to "ConsumerAuth")</param>
    /// <returns>Loaded OIDC configuration or null if failed</returns>
    public static async Task<OidcConfig?> LoadOidcConfigurationAsync(
        string baseAddress,
        string configUrl = "config",
        string defaultAuthConfig = "ConsumerAuth")
    {
        OidcConfig? oidcConfig = null;

        try
        {
            // First, try to fetch with cache-breaking (assumes online)
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var freshConfigUrl = $"{baseAddress}{configUrl}?v={timestamp}";

            Console.WriteLine($"[DynamicOidcConfig] Attempting to fetch: {freshConfigUrl}");

            try
            {
                using var httpClient = new HttpClient();
                // Set timeout for quick offline detection
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

                Console.WriteLine("[DynamicOidcConfig] Starting HTTP request...");
                var response = await httpClient.GetAsync(freshConfigUrl, cts.Token);
                Console.WriteLine($"[DynamicOidcConfig] Response status: {response.StatusCode}");

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"[DynamicOidcConfig] Response content (first 200 chars): {json.Substring(0, Math.Min(200, json.Length))}");
                    Console.WriteLine($"[DynamicOidcConfig] Response content type: {response.Content.Headers.ContentType}");

                    oidcConfig = JsonConvert.DeserializeObject<OidcConfig>(json);
                    if (oidcConfig != null)
                    {
                        oidcConfig.SelectedAuthConfig = defaultAuthConfig;
                    }
                    Console.WriteLine("[DynamicOidcConfig] Loaded fresh OIDC config with cache-breaking");
                }
                else
                {
                    Console.WriteLine($"[DynamicOidcConfig] Failed with status: {response.StatusCode}");
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"[DynamicOidcConfig] Error response content (first 200 chars): {errorContent.Substring(0, Math.Min(200, errorContent.Length))}");
                }
            }
            catch (Exception ex) when (ex is TaskCanceledException || ex is HttpRequestException)
            {
                // Likely offline or network issue - fall back to cached version
                Console.WriteLine("[DynamicOidcConfig] Failed to fetch fresh config, trying cached version");

                try
                {
                    using var fallbackClient = new HttpClient();
                    // Try without cache-breaking to hit service worker cache
                    var cachedConfigUrl = $"{baseAddress}{configUrl}";
                    Console.WriteLine($"[DynamicOidcConfig] Trying cached URL: {cachedConfigUrl}");
                    var cachedResponse = await fallbackClient.GetAsync(cachedConfigUrl);

                    if (cachedResponse.IsSuccessStatusCode)
                    {
                        var json = await cachedResponse.Content.ReadAsStringAsync();
                        Console.WriteLine($"[DynamicOidcConfig] Cached response content (first 200 chars): {json.Substring(0, Math.Min(200, json.Length))}");
                        oidcConfig = JsonConvert.DeserializeObject<OidcConfig>(json);
                        if (oidcConfig != null)
                        {
                            oidcConfig.SelectedAuthConfig = defaultAuthConfig;
                        }
                        Console.WriteLine("[DynamicOidcConfig] Loaded cached OIDC config");
                    }
                    else
                    {
                        Console.WriteLine($"[DynamicOidcConfig] Cached request failed with status: {cachedResponse.StatusCode}");
                        var errorContent = await cachedResponse.Content.ReadAsStringAsync();
                        Console.WriteLine($"[DynamicOidcConfig] Cached error content (first 200 chars): {errorContent.Substring(0, Math.Min(200, errorContent.Length))}");
                    }
                }
                catch (Exception fallbackEx)
                {
                    Console.WriteLine($"[DynamicOidcConfig] Failed to load cached config: {fallbackEx.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DynamicOidcConfig] Failed to load OIDC config: {ex.Message}");
        }

        if (oidcConfig == null)
        {
            Console.WriteLine("[DynamicOidcConfig] No configuration loaded - authentication will not be available");
        }
        else
        {
            Console.WriteLine($"[DynamicOidcConfig] Successfully loaded configuration with {oidcConfig.AuthConfigs.Count} auth config(s)");
        }

        return oidcConfig;
    }

    /// <summary>
    /// Gets OIDC configuration options based on loaded configuration
    /// </summary>
    /// <param name="oidcConfig">The loaded OIDC configuration</param>
    /// <param name="defaultAuthConfig">The auth config name to use</param>
    /// <param name="baseAddress">Base address for redirect URIs</param>
    /// <returns>OIDC options configuration or null if not available</returns>
    public static OidcOptionsConfiguration? GetOidcOptions(
        OidcConfig? oidcConfig,
        string defaultAuthConfig,
        string baseAddress)
    {
        if (oidcConfig != null && oidcConfig.AuthConfigs.TryGetValue(defaultAuthConfig, out var authConfig))
        {
            var options = OidcOptionsConfiguration.FromAuthConfig(authConfig, baseAddress);
            Console.WriteLine($"[DynamicOidcConfig] Configured OIDC with authority: {options.Authority}");
            Console.WriteLine($"[DynamicOidcConfig] Client ID: {options.ClientId}");
            Console.WriteLine($"[DynamicOidcConfig] Redirect URI: {options.RedirectUri}");
            return options;
        }

        Console.WriteLine($"[DynamicOidcConfig] No OIDC configuration available for auth config: {defaultAuthConfig}");
        return null;
    }
}