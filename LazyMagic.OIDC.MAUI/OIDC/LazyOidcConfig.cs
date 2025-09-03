namespace LazyMagic.OIDC.MAUI;

/// <summary>
/// Lazy-loading OIDC configuration service that loads real config when first accessed
/// </summary>
public class LazyOidcConfig : IOidcConfig
{
    private OidcConfig? _loadedConfig;
    private bool _isLoading;
    private readonly SemaphoreSlim _loadSemaphore = new(1, 1);

    public Dictionary<string, JObject> AuthConfigs 
    { 
        get
        {
            if (_loadedConfig != null)
                return _loadedConfig.AuthConfigs;
            
            // Config not loaded yet - return empty dictionary to avoid deadlock
            // Configuration should be loaded asynchronously before accessing properties
            Console.WriteLine("[LazyOidcConfig] ⚠️ Config accessed before loading completed, returning empty dictionary");
            return new Dictionary<string, JObject>();
        }
    }
    
    public string SelectedAuthConfig 
    { 
        get 
        {
            if (_loadedConfig != null)
                return _loadedConfig.SelectedAuthConfig ?? "ConsumerAuth";
            
            // Return default without loading to avoid deadlock
            Console.WriteLine("[LazyOidcConfig] ⚠️ Config accessed before loading completed, returning default: ConsumerAuth");
            return "ConsumerAuth";
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
    
    public JObject? GetCurrentAuthConfig()
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
    /// This method should be used to pre-load configuration before accessing properties
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
            Console.WriteLine($"[LazyOidcConfig] Failed to load config async: {ex.Message}");
            return CreateMockConfig().AuthConfigs;
        }
    }

    /// <summary>
    /// Gets the selected auth config name asynchronously after loading
    /// This method should be used to pre-load configuration before accessing properties
    /// </summary>
    public async Task<string> GetSelectedAuthConfigAsync()
    {
        try
        {
            var config = await GetConfigAsync();
            return config.SelectedAuthConfig ?? "ConsumerAuth";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[LazyOidcConfig] Failed to load config async: {ex.Message}");
            return "ConsumerAuth";
        }
    }

    private async Task<OidcConfig> GetConfigAsync()
    {
        if (_loadedConfig != null)
            return _loadedConfig;

        await _loadSemaphore.WaitAsync();
        try
        {
            if (_loadedConfig != null)
                return _loadedConfig;

            if (_isLoading)
            {
                // Another thread is loading, wait and return mock config for now
                return CreateMockConfig();
            }

            _isLoading = true;
            
            try
            {
                Console.WriteLine("[LazyOidcConfig] Loading OIDC configuration...");
                
                var baseUrl = "https://uptown.lazymagicdev.click/";
                var config = await DynamicOidcConfigurationService.LoadOidcConfigurationAsync(
                    baseUrl,
                    "config",
                    "ConsumerAuth");
                
                if (config != null)
                {
                    _loadedConfig = config;
                    Console.WriteLine("[LazyOidcConfig] ✅ Loaded real OIDC configuration");
                    return _loadedConfig;
                }
                else
                {
                    Console.WriteLine("[LazyOidcConfig] ⚠️ Failed to load real config, using mock");
                    _loadedConfig = CreateMockConfig();
                    return _loadedConfig;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LazyOidcConfig] ❌ Error loading config: {ex.Message}");
                _loadedConfig = CreateMockConfig();
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

    private static OidcConfig CreateMockConfig()
    {
        var mockConfig = new OidcConfig();
        mockConfig.AuthConfigs["ConsumerAuth"] = JObject.FromObject(new
        {
            authority = "https://mock-cognito.amazonaws.com",
            clientId = "mock-client-id",
            responseType = "code"
        });
        mockConfig.SelectedAuthConfig = "ConsumerAuth";
        return mockConfig;
    }
}