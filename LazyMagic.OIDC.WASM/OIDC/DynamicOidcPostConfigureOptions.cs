namespace LazyMagic.OIDC.WASM;

/// <summary>
/// Post-configures OIDC options using the dynamically loaded configuration
/// This runs when OIDC services are first accessed, allowing us to apply dynamic config
/// </summary>
public class DynamicOidcPostConfigureOptions : IPostConfigureOptions<RemoteAuthenticationOptions<OidcProviderOptions>>
{
    private readonly DynamicOidcConfigHolder _configHolder;
    private readonly ILogger<DynamicOidcPostConfigureOptions> _logger;
    private bool _hasConfigured = false;
    private readonly object _configLock = new();

    public DynamicOidcPostConfigureOptions(
        DynamicOidcConfigHolder configHolder,
        ILogger<DynamicOidcPostConfigureOptions> logger)
    {
        _configHolder = configHolder;
        _logger = logger;
    }

    public void PostConfigure(string? name, RemoteAuthenticationOptions<OidcProviderOptions> options)
    {
        _logger.LogInformation("PostConfigure called with name: {Name}", name ?? "null");
        
        lock (_configLock)
        {
            if (_hasConfigured)
            {
                _logger.LogInformation("Configuration already applied, skipping");
                return;
            }

            try
            {
                _logger.LogInformation("ConfigHolder.IsConfigured: {IsConfigured}", _configHolder.IsConfigured);
                
                if (_configHolder.IsConfigured)
                {
                    var config = _configHolder.GetConfiguration();
                    _logger.LogInformation("Applying configuration - Authority: {Authority}, ClientId: {ClientId}", 
                        config?.Authority, config?.ClientId);
                    
                    // Apply the dynamic configuration
                    _configHolder.ApplyConfiguration(options);
                    
                    // Log the final applied configuration
                    _logger.LogInformation("Final OIDC options - Authority: {Authority}, ClientId: {ClientId}, RedirectUri: {RedirectUri}", 
                        options.ProviderOptions.Authority, 
                        options.ProviderOptions.ClientId,
                        options.ProviderOptions.RedirectUri);
                    
                    _logger.LogInformation("Successfully applied dynamic OIDC configuration");
                }
                else
                {
                    // Check if authentication is intentionally disabled
                    if (_configHolder.IsAuthenticationDisabled)
                    {
                        _logger.LogInformation("Authentication is disabled - using null authority to prevent OIDC initialization");
                        // Set authority to null to prevent OIDC from trying to fetch metadata
                        options.ProviderOptions.Authority = null;
                        options.ProviderOptions.MetadataUrl = null;
                    }
                    else
                    {
                        // Configuration not loaded yet - this means authentication happened too early
                        _logger.LogWarning("Dynamic OIDC configuration not available during post-configure");
                        _logger.LogInformation("Current options - Authority: {Authority}, ClientId: {ClientId}", 
                            options.ProviderOptions.Authority, options.ProviderOptions.ClientId);
                    }
                }

                _hasConfigured = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error applying dynamic OIDC configuration");
            }
        }
    }
}