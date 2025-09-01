namespace LazyMagic.OIDC.WASM;

/// <summary>
/// Lazy-loading OIDC configuration service for Blazor WebAssembly
/// Defers configuration loading until first access
/// </summary>
public class BlazorLazyOidcConfig : IOidcConfig
{
    private readonly ILzHost _lzHost;
    private readonly string _configUrl;
    private readonly string _defaultAuthConfig;
    private readonly ILogger<BlazorLazyOidcConfig>? _logger;
    private OidcConfig? _loadedConfig;
    private bool _isLoading;
    private readonly SemaphoreSlim _loadSemaphore = new(1, 1);

    public BlazorLazyOidcConfig(
        ILzHost lzHost,
        string configUrl = "config",
        string defaultAuthConfig = "ConsumerAuth",
        ILogger<BlazorLazyOidcConfig>? logger = null)
    {
        _lzHost = lzHost;
        _configUrl = configUrl;
        _defaultAuthConfig = defaultAuthConfig;
        _logger = logger;
    }

    public Dictionary<string, JObject> AuthConfigs
    {
        get
        {
            Console.WriteLine("[BlazorLazyOidcConfig] 🔍 AuthConfigs property accessed");
            
            if (_loadedConfig != null)
            {
                Console.WriteLine($"[BlazorLazyOidcConfig] ✅ Returning cached config with {_loadedConfig.AuthConfigs.Count} configs");
                return _loadedConfig.AuthConfigs;
            }

            Console.WriteLine("[BlazorLazyOidcConfig] ⏳ Config not loaded yet, attempting to load...");

            // Config not loaded yet - attempt to load it now
            try
            {
                var config = GetConfigAsync().GetAwaiter().GetResult();
                return config.AuthConfigs;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BlazorLazyOidcConfig] Failed to load config: {ex.Message}");
                return new Dictionary<string, JObject>();
            }
        }
    }

    public string SelectedAuthConfig
    {
        get
        {
            Console.WriteLine("[BlazorLazyOidcConfig] 🔍 SelectedAuthConfig property accessed");
            
            if (_loadedConfig != null)
            {
                var selected = _loadedConfig.SelectedAuthConfig ?? _defaultAuthConfig;
                Console.WriteLine($"[BlazorLazyOidcConfig] ✅ Returning cached selected config: {selected}");
                return selected;
            }

            // Config not loaded yet - attempt to load it now
            try
            {
                var config = GetConfigAsync().GetAwaiter().GetResult();
                return config.SelectedAuthConfig ?? _defaultAuthConfig;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BlazorLazyOidcConfig] Failed to load config: {ex.Message}");
                return _defaultAuthConfig;
            }
        }
        set
        {
            if (_loadedConfig != null)
            {
                _loadedConfig.SelectedAuthConfig = value;
            }
            // If config isn't loaded yet, we could store this for later use
            // For now, we'll ignore it until config is loaded
        }
    }

    public bool IsConfigured => _loadedConfig != null;

    public JObject? GetCurrentAuthConfig()
    {
        // For backward compatibility, but this will no longer load synchronously
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
    /// This method is intended to be used by the DeferredOidcConfigurationService
    /// </summary>
    public async Task<Dictionary<string, JObject>> LoadAuthConfigsAsync()
    {
        try
        {
            var config = await GetConfigAsync();
            return config.AuthConfigs;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load OIDC configuration asynchronously");
            Console.WriteLine($"[BlazorLazyOidcConfig] Failed to load config async: {ex.Message}");
            return CreateFallbackConfig().AuthConfigs;
        }
    }

    /// <summary>
    /// Gets the selected auth config name asynchronously after loading
    /// This method is intended to be used by the DeferredOidcConfigurationService
    /// </summary>
    public async Task<string> GetSelectedAuthConfigAsync()
    {
        try
        {
            var config = await GetConfigAsync();
            return config.SelectedAuthConfig ?? _defaultAuthConfig;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load OIDC configuration asynchronously");
            Console.WriteLine($"[BlazorLazyOidcConfig] Failed to load config async: {ex.Message}");
            return _defaultAuthConfig;
        }
    }


    /// <summary>
    /// Loads the OIDC configuration asynchronously
    /// This method is called lazily when the configuration is first accessed
    /// </summary>
    private async Task<OidcConfig> GetConfigAsync()
    {
        Console.WriteLine("[BlazorLazyOidcConfig] 📥 GetConfigAsync called");
        
        if (_loadedConfig != null)
        {
            Console.WriteLine("[BlazorLazyOidcConfig] ✅ Config already loaded, returning cached version");
            return _loadedConfig;
        }

        Console.WriteLine("[BlazorLazyOidcConfig] 🔒 Acquiring semaphore for config loading...");
        await _loadSemaphore.WaitAsync();
        try
        {
            if (_loadedConfig != null)
            {
                Console.WriteLine("[BlazorLazyOidcConfig] ✅ Config loaded by another thread, returning cached version");
                return _loadedConfig;
            }

            if (_isLoading)
            {
                Console.WriteLine("[BlazorLazyOidcConfig] ⏳ Another thread is loading, returning fallback");
                // Another thread is loading, return fallback for now
                return CreateFallbackConfig();
            }


            Console.WriteLine("[BlazorLazyOidcConfig] 🚀 Starting actual config loading...");
            _isLoading = true;

            try
            {
                var assetsUrl = _lzHost.AssetsUrl;
                _logger?.LogInformation("Loading OIDC configuration from {AssetsUrl}/{ConfigUrl}", assetsUrl, _configUrl);
                Console.WriteLine($"[BlazorLazyOidcConfig] Loading OIDC configuration from {assetsUrl}/{_configUrl}...");
                Console.WriteLine($"[BlazorLazyOidcConfig] Full URL will be: {assetsUrl.TrimEnd('/')}/{_configUrl}");

                var config = await DynamicOidcConfigurationService.LoadOidcConfigurationAsync(
                    assetsUrl,
                    _configUrl,
                    _defaultAuthConfig);

                if (config != null)
                {
                    _loadedConfig = config;
                    _logger?.LogInformation("Successfully loaded OIDC configuration");
                    Console.WriteLine("[BlazorLazyOidcConfig] ✅ Loaded OIDC configuration");
                    return _loadedConfig;
                }
                else
                {
                    _logger?.LogWarning("Failed to load OIDC configuration, using fallback");
                    Console.WriteLine("[BlazorLazyOidcConfig] ⚠️ Failed to load config, using fallback");
                    _loadedConfig = CreateFallbackConfig();
                    return _loadedConfig;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error loading OIDC configuration");
                Console.WriteLine($"[BlazorLazyOidcConfig] ❌ Error loading config: {ex.Message}");
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
            return config != null && config != CreateFallbackConfig();
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

    /// <summary>
    /// Creates a fallback configuration when the real config cannot be loaded
    /// This allows the app to start even if the config service is unavailable
    /// </summary>
    private OidcConfig CreateFallbackConfig()
    {
        var fallbackConfig = new OidcConfig();
        
        // Create empty fallback configuration that won't cause network errors
        // The actual configuration will come from appsettings.json
        fallbackConfig.AuthConfigs[_defaultAuthConfig] = JObject.FromObject(new
        {
            authority = "",
            clientId = "",
            responseType = "code",
            defaultScopes = new[] { "openid", "profile", "email" }
        });
        
        fallbackConfig.SelectedAuthConfig = _defaultAuthConfig;
        
        _logger?.LogWarning("Using empty fallback OIDC configuration");
        return fallbackConfig;
    }
}