namespace LazyMagic.OIDC.WASM;

/// <summary>
/// Holds the dynamically loaded OIDC configuration and provides a way to apply it
/// This allows us to load configuration after startup and apply it when needed
/// </summary>
public class DynamicOidcConfigHolder
{
    private OidcOptionsConfiguration? _configuration;
    private readonly object _lock = new();

    public bool IsConfigured 
    { 
        get 
        { 
            lock (_lock)
            {
                return _configuration != null;
            }
        }
    }

    public void SetConfiguration(OidcOptionsConfiguration configuration)
    {
        lock (_lock)
        {
            _configuration = configuration;
        }
    }

    public void SetConfigurationFromAuthConfig(JObject authConfig, string baseAddress)
    {
        var configuration = OidcOptionsConfiguration.FromAuthConfig(authConfig, baseAddress);
        SetConfiguration(configuration);
    }

    public void ApplyConfiguration(RemoteAuthenticationOptions<OidcProviderOptions> options)
    {
        lock (_lock)
        {
            if (_configuration == null)
            {
                return;
            }
            
            options.ProviderOptions.Authority = _configuration.Authority;
            options.ProviderOptions.ClientId = _configuration.ClientId;
            options.ProviderOptions.ResponseType = _configuration.ResponseType;
            options.ProviderOptions.MetadataUrl = _configuration.MetadataUrl;
            options.ProviderOptions.RedirectUri = _configuration.RedirectUri;
            options.ProviderOptions.PostLogoutRedirectUri = _configuration.PostLogoutRedirectUri;
            
            options.ProviderOptions.DefaultScopes.Clear();
            foreach (var scope in _configuration.DefaultScopes)
            {
                options.ProviderOptions.DefaultScopes.Add(scope);
            }
            
            options.UserOptions.NameClaim = _configuration.NameClaim;
            options.UserOptions.RoleClaim = _configuration.RoleClaim;
        }
    }

    public OidcOptionsConfiguration? GetConfiguration()
    {
        lock (_lock)
        {
            return _configuration;
        }
    }
}