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
    /// <param name="logger">Logger instance for diagnostic output</param>
    /// <returns>Loaded OIDC configuration or null if failed</returns>
    public static async Task<OidcConfig?> LoadOidcConfigurationAsync(
        string baseAddress,
        string configUrl = "config",
        string defaultAuthConfig = "ConsumerAuth",
        ILogger? logger = null)
    {
        OidcConfig? oidcConfig = null;

        try
        {
            // First, try to fetch with cache-breaking (assumes online)
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var freshConfigUrl = $"{baseAddress}{configUrl}?v={timestamp}";

            logger?.LogInformation("[LoadOidcConfigurationAsync][{Timestamp}] Attempting to fetch: {FreshConfigUrl}", DateTime.UtcNow.ToString("HH:mm:ss.fff"), freshConfigUrl);

            try
            {
                using var httpClient = new HttpClient();
                // Set timeout for quick offline detection
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

                logger?.LogInformation("[LoadOidcConfigurationAsync][{Timestamp}] Starting HTTP request", DateTime.UtcNow.ToString("HH:mm:ss.fff"));
                var response = await httpClient.GetAsync(freshConfigUrl, cts.Token);
                logger?.LogInformation("[LoadOidcConfigurationAsync][{Timestamp}] Response status: {StatusCode}", DateTime.UtcNow.ToString("HH:mm:ss.fff"), response.StatusCode);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    logger?.LogInformation("[LoadOidcConfigurationAsync][{Timestamp}] Response content (first 200 chars): {ResponseContent}", DateTime.UtcNow.ToString("HH:mm:ss.fff"), json.Substring(0, Math.Min(200, json.Length)));
                    logger?.LogInformation("[LoadOidcConfigurationAsync][{Timestamp}] Response content type: {ContentType}", DateTime.UtcNow.ToString("HH:mm:ss.fff"), response.Content.Headers.ContentType);

                    oidcConfig = JsonConvert.DeserializeObject<OidcConfig>(json);
                    if (oidcConfig != null)
                    {
                        oidcConfig.SelectedAuthConfig = defaultAuthConfig;
                    }
                    logger?.LogInformation("[LoadOidcConfigurationAsync][{Timestamp}] Loaded fresh OIDC config with cache-breaking", DateTime.UtcNow.ToString("HH:mm:ss.fff"));
                }
                else
                {
                    logger?.LogWarning("[LoadOidcConfigurationAsync][{Timestamp}] Failed with status: {StatusCode}", DateTime.UtcNow.ToString("HH:mm:ss.fff"), response.StatusCode);
                    var errorContent = await response.Content.ReadAsStringAsync();
                    logger?.LogWarning("[LoadOidcConfigurationAsync][{Timestamp}] Error response content (first 200 chars): {ErrorContent}", DateTime.UtcNow.ToString("HH:mm:ss.fff"), errorContent.Substring(0, Math.Min(200, errorContent.Length)));
                }
            }
            catch (Exception ex) when (ex is TaskCanceledException || ex is HttpRequestException)
            {
                // Likely offline or network issue - fall back to cached version
                logger?.LogWarning("[LoadOidcConfigurationAsync][{Timestamp}] Failed to fetch fresh config, trying cached version", DateTime.UtcNow.ToString("HH:mm:ss.fff"));

                try
                {
                    using var fallbackClient = new HttpClient();
                    // Try without cache-breaking to hit service worker cache
                    var cachedConfigUrl = $"{baseAddress}{configUrl}";
                    logger?.LogInformation("[LoadOidcConfigurationAsync][{Timestamp}] Trying cached URL: {CachedConfigUrl}", DateTime.UtcNow.ToString("HH:mm:ss.fff"), cachedConfigUrl);
                    var cachedResponse = await fallbackClient.GetAsync(cachedConfigUrl);

                    if (cachedResponse.IsSuccessStatusCode)
                    {
                        var json = await cachedResponse.Content.ReadAsStringAsync();
                        logger?.LogInformation("[LoadOidcConfigurationAsync][{Timestamp}] Cached response content (first 200 chars): {CachedContent}", DateTime.UtcNow.ToString("HH:mm:ss.fff"), json.Substring(0, Math.Min(200, json.Length)));
                        oidcConfig = JsonConvert.DeserializeObject<OidcConfig>(json);
                        if (oidcConfig != null)
                        {
                            oidcConfig.SelectedAuthConfig = defaultAuthConfig;
                        }
                        logger?.LogInformation("[LoadOidcConfigurationAsync][{Timestamp}] Loaded cached OIDC config", DateTime.UtcNow.ToString("HH:mm:ss.fff"));
                    }
                    else
                    {
                        logger?.LogWarning("[LoadOidcConfigurationAsync][{Timestamp}] Cached request failed with status: {CachedStatusCode}", DateTime.UtcNow.ToString("HH:mm:ss.fff"), cachedResponse.StatusCode);
                        var errorContent = await cachedResponse.Content.ReadAsStringAsync();
                        logger?.LogWarning("[LoadOidcConfigurationAsync][{Timestamp}] Cached error content (first 200 chars): {CachedErrorContent}", DateTime.UtcNow.ToString("HH:mm:ss.fff"), errorContent.Substring(0, Math.Min(200, errorContent.Length)));
                    }
                }
                catch (Exception fallbackEx)
                {
                    logger?.LogError(fallbackEx, "[LoadOidcConfigurationAsync][{Timestamp}] Failed to load cached config: {ErrorMessage}", DateTime.UtcNow.ToString("HH:mm:ss.fff"), fallbackEx.Message);
                }
            }
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "[LoadOidcConfigurationAsync][{Timestamp}] Failed to load OIDC config: {ErrorMessage}", DateTime.UtcNow.ToString("HH:mm:ss.fff"), ex.Message);
        }

        if (oidcConfig == null)
        {
            logger?.LogWarning("[LoadOidcConfigurationAsync][{Timestamp}] No configuration loaded - authentication will not be available", DateTime.UtcNow.ToString("HH:mm:ss.fff"));
        }
        else
        {
            logger?.LogInformation("[LoadOidcConfigurationAsync][{Timestamp}] Successfully loaded configuration with {AuthConfigCount} auth config(s)", DateTime.UtcNow.ToString("HH:mm:ss.fff"), oidcConfig.AuthConfigs.Count);
        }

        return oidcConfig;
    }

    /// <summary>
    /// Gets OIDC configuration options based on loaded configuration
    /// </summary>
    /// <param name="oidcConfig">The loaded OIDC configuration</param>
    /// <param name="defaultAuthConfig">The auth config name to use</param>
    /// <param name="baseAddress">Base address for redirect URIs</param>
    /// <param name="logger">Logger instance for diagnostic output</param>
    /// <returns>OIDC options configuration or null if not available</returns>
    public static OidcOptionsConfiguration? GetOidcOptions(
        OidcConfig? oidcConfig,
        string defaultAuthConfig,
        string baseAddress,
        ILogger? logger = null)
    {
        if (oidcConfig != null && oidcConfig.AuthConfigs.TryGetValue(defaultAuthConfig, out var authConfig))
        {
            var options = OidcOptionsConfiguration.FromAuthConfig(authConfig, baseAddress);
            logger?.LogInformation("[GetOidcOptions][{Timestamp}] Configured OIDC with authority: {Authority}", DateTime.UtcNow.ToString("HH:mm:ss.fff"), options.Authority);
            logger?.LogInformation("[GetOidcOptions][{Timestamp}] Client ID: {ClientId}", DateTime.UtcNow.ToString("HH:mm:ss.fff"), options.ClientId);
            logger?.LogInformation("[GetOidcOptions][{Timestamp}] Redirect URI: {RedirectUri}", DateTime.UtcNow.ToString("HH:mm:ss.fff"), options.RedirectUri);
            return options;
        }

        logger?.LogWarning("[GetOidcOptions][{Timestamp}] No OIDC configuration available for auth config: {DefaultAuthConfig}", DateTime.UtcNow.ToString("HH:mm:ss.fff"), defaultAuthConfig);
        return null;
    }
}