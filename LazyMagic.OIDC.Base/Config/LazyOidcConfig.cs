using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace LazyMagic.OIDC.Base;

/// <summary>
/// Lazy-loading OIDC configuration service
/// Provides thread-safe, async configuration loading with fallback support
/// </summary>
public class LazyOidcConfig : IOidcConfig
{
    protected readonly ILzHost _lzHost;
    protected readonly ILogger? _logger;
    protected readonly string _configUrl;
    protected readonly string _defaultAuthConfig;
    protected OidcConfig? _loadedConfig;
    protected bool _isLoading;
    protected readonly SemaphoreSlim _loadSemaphore = new(1, 1);

    public LazyOidcConfig(
        ILzHost lzHost,
        ILogger? logger = null)
    {
        _lzHost = lzHost;
        _logger = logger;
        _configUrl = "config";
        _defaultAuthConfig = _lzHost.AuthConfigName ?? "ConsumerAuth";
    }

    public virtual Dictionary<string, JObject> AuthConfigs
    {
        get
        {
            if (_loadedConfig != null)
                return _loadedConfig.AuthConfigs;

            // Return empty dictionary to avoid deadlock - configuration should be loaded asynchronously
            LogMessage($"Config accessed before loading completed, returning empty dictionary");
            return new Dictionary<string, JObject>();
        }
    }

    public virtual string SelectedAuthConfig
    {
        get
        {
            if (_loadedConfig != null)
                return _loadedConfig.SelectedAuthConfig ?? _defaultAuthConfig;

            // Return the default without loading to avoid deadlock
            LogMessage($"Config accessed before loading completed, returning default: {_defaultAuthConfig}");
            return _defaultAuthConfig;
        }
        set
        {
            if (_loadedConfig != null)
            {
                _loadedConfig.SelectedAuthConfig = value;
            }
        }
    }

    public bool IsConfigured => _loadedConfig != null;

    public virtual JObject? GetCurrentAuthConfig()
    {
        var authConfigs = AuthConfigs;
        var selected = SelectedAuthConfig;

        if (authConfigs.TryGetValue(selected, out var authConfig))
        {
            return authConfig;
        }
        return null;
    }

    /// <summary>
    /// Loads configuration asynchronously and returns the auth configs
    /// This method should be called to pre-load configuration before accessing properties
    /// </summary>
    public async Task<Dictionary<string, JObject>> LoadAuthConfigsAsync()
    {
        // If AuthConfigName is empty, skip loading and return empty config
        if (string.IsNullOrEmpty(_lzHost.AuthConfigName))
        {
            LogMessage("AuthConfigName is empty, returning empty configuration");
            return new Dictionary<string, JObject>();
        }
        
        try
        {
            var config = await GetConfigAsync();
            return config.AuthConfigs;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load OIDC configuration asynchronously");
            LogMessage($"Failed to load config async: {ex.Message}");
            return CreateFallbackConfig().AuthConfigs;
        }
    }

    /// <summary>
    /// Gets the selected auth config name asynchronously after loading
    /// This method should be called to pre-load configuration before accessing properties
    /// </summary>
    public async Task<string> GetSelectedAuthConfigAsync()
    {
        // If AuthConfigName is empty, return empty string
        if (string.IsNullOrEmpty(_lzHost.AuthConfigName))
        {
            LogMessage("AuthConfigName is empty, returning empty string");
            return string.Empty;
        }
        
        try
        {
            var config = await GetConfigAsync();
            return config.SelectedAuthConfig ?? _defaultAuthConfig;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load OIDC configuration asynchronously");
            LogMessage($"Failed to load config async: {ex.Message}");
            return _defaultAuthConfig;
        }
    }

    /// <summary>
    /// Attempts to load configuration with a timeout
    /// Returns true if loading succeeds or was already loaded, false if timeout or error
    /// </summary>
    public async Task<bool> TryLoadConfigAsync(int timeoutMs = 1000)
    {
        if (_loadedConfig != null)
            return true;

        try
        {
            using var cts = new CancellationTokenSource(timeoutMs);
            var config = await GetConfigAsync();
            return config != null && config.AuthConfigs.Any();
        }
        catch (OperationCanceledException)
        {
            _logger?.LogWarning("Configuration loading timed out after {TimeoutMs}ms", timeoutMs);
            return false;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error loading configuration with timeout");
            return false;
        }
    }

    protected virtual async Task<OidcConfig> GetConfigAsync()
    {
        if (_loadedConfig != null)
            return _loadedConfig;

        // If AuthConfigName is empty, return fallback config without trying to load
        if (string.IsNullOrEmpty(_lzHost.AuthConfigName))
        {
            LogMessage("AuthConfigName is empty, returning fallback configuration");
            return CreateFallbackConfig();
        }

        await _loadSemaphore.WaitAsync();
        try
        {
            if (_loadedConfig != null)
                return _loadedConfig;

            if (_isLoading)
            {
                // Another thread is loading, return fallback for now
                LogMessage("Another thread is loading, returning fallback");
                return CreateFallbackConfig();
            }

            _isLoading = true;

            try
            {
                var assetsUrl = _lzHost.AssetsUrl;
                _logger?.LogInformation("Loading OIDC configuration from {AssetsUrl}/{ConfigUrl}", assetsUrl, _configUrl);
                LogMessage($"Loading OIDC configuration from {assetsUrl}/{_configUrl}...");

                var config = await DynamicOidcConfigurationService.LoadOidcConfigurationAsync(
                    assetsUrl,
                    _configUrl,
                    _defaultAuthConfig);

                if (config != null)
                {
                    _loadedConfig = config;
                    _logger?.LogInformation("Successfully loaded OIDC configuration with {Count} auth configs", config.AuthConfigs.Count);
                    LogMessage($"✅ Loaded OIDC configuration with {config.AuthConfigs.Count} auth configs");
                    
                    // Log the configuration values
                    _logger?.LogInformation("Configuration details:");
                    _logger?.LogInformation("  Selected AuthConfig: {SelectedAuth}", config.SelectedAuthConfig);
                    
                    foreach (var authConfigEntry in config.AuthConfigs)
                    {
                        _logger?.LogInformation("  AuthConfig '{Name}':", authConfigEntry.Key);
                        
                        // Log key properties from the JObject
                        var authConfig = authConfigEntry.Value;
                        if (authConfig["authority"] != null)
                            _logger?.LogInformation("    - Authority: {Authority}", authConfig["authority"]);
                        if (authConfig["clientId"] != null)
                            _logger?.LogInformation("    - ClientId: {ClientId}", authConfig["clientId"]);
                        if (authConfig["responseType"] != null)
                            _logger?.LogInformation("    - ResponseType: {ResponseType}", authConfig["responseType"]);
                        if (authConfig["defaultScopes"] != null)
                            _logger?.LogInformation("    - DefaultScopes: {Scopes}", string.Join(", ", authConfig["defaultScopes"]?.Select(s => s.ToString()) ?? Array.Empty<string>()));
                        
                        // Log any additional properties
                        var standardProps = new[] { "authority", "clientId", "responseType", "defaultScopes" };
                        var additionalProps = authConfig.Properties()
                            .Where(p => !standardProps.Contains(p.Name))
                            .Select(p => p.Name);
                        
                        if (additionalProps.Any())
                            _logger?.LogInformation("    - Additional properties: {Props}", string.Join(", ", additionalProps));
                    }
                    
                    return _loadedConfig;
                }
                else
                {
                    _logger?.LogWarning("Failed to load OIDC configuration, using fallback");
                    LogMessage("⚠️ Failed to load config, using fallback");
                    _loadedConfig = CreateFallbackConfig();
                    return _loadedConfig;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error loading OIDC configuration");
                LogMessage($"❌ Error loading config: {ex.Message}");
                _loadedConfig = CreateFallbackConfig();
                return _loadedConfig;
            }
            finally
            {
                _isLoading = false;
            }
        }
        finally
        {
            _loadSemaphore.Release();
        }
    }

    protected virtual OidcConfig CreateFallbackConfig()
    {
        var fallbackConfig = new OidcConfig();

        // Create fallback configuration that won't cause network errors
        fallbackConfig.AuthConfigs[_defaultAuthConfig] = JObject.FromObject(new
        {
            authority = "",
            clientId = "",
            responseType = "code",
            defaultScopes = new[] { "openid", "profile", "email" }
        });

        fallbackConfig.SelectedAuthConfig = _defaultAuthConfig;

        _logger?.LogWarning("Using fallback OIDC configuration for {AuthConfig}", _defaultAuthConfig);
        LogMessage($"Created fallback config for auth: {_defaultAuthConfig}");
        return fallbackConfig;
    }

    protected virtual void LogMessage(string message)
    {
        Console.WriteLine($"[{GetType().Name}] {message}");
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _loadSemaphore?.Dispose();
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}