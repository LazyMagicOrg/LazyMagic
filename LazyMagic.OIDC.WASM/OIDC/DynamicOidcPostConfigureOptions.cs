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
        lock (_configLock)
        {
            if (_hasConfigured)
            {
                return;
            }

            try
            {
                if (_configHolder.IsConfigured)
                {
                    // Apply the dynamic configuration
                    _configHolder.ApplyConfiguration(options);
                    _logger.LogInformation("Successfully applied dynamic OIDC configuration");
                }
                else
                {
                    // Configuration not loaded yet - this means authentication happened too early
                    _logger.LogWarning("Dynamic OIDC configuration not available during post-configure");
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