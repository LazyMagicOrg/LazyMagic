namespace LazyMagic.OIDC.Base;

/// <summary>
/// Provides configuration values from dynamically loaded config
/// Acts as a bridge between the dynamic config and IConfiguration consumers
/// Uses the OpenID discovery document to obtain the end_session_endpoint
/// </summary>
public class DynamicConfigurationProvider : IDynamicConfigurationProvider
{
    private readonly IOidcConfig _oidcConfig;
    private readonly ILzHost _lzHost;
    private readonly ILogger<DynamicConfigurationProvider> _logger;
    private readonly IOpenIdDiscoveryService _discoveryService;
    private string? _cachedEndSessionEndpoint;
    private bool _discoveryInitialized;

    public DynamicConfigurationProvider(
        IOidcConfig oidcConfig,
        ILzHost lzHost,
        ILogger<DynamicConfigurationProvider> logger,
        IOpenIdDiscoveryService discoveryService)
    {
        _oidcConfig = oidcConfig;
        _lzHost = lzHost;
        _logger = logger;
        _discoveryService = discoveryService;
    }

    /// <summary>
    /// Gets the metadata URL (.well-known/openid-configuration) from the dynamic configuration
    /// </summary>
    public string? GetMetadataUrl()
    {
        try
        {
            if (_oidcConfig.AuthConfigs.TryGetValue(_oidcConfig.SelectedAuthConfig, out var authConfig))
            {
                // Try MetadataUrl first (new config format)
                var metadataUrl = authConfig["MetadataUrl"]?.ToString();
                if (!string.IsNullOrEmpty(metadataUrl))
                {
                    return metadataUrl;
                }

                // Try metadataUrl (lowercase, generic format)
                metadataUrl = authConfig["metadataUrl"]?.ToString();
                if (!string.IsNullOrEmpty(metadataUrl))
                {
                    return metadataUrl;
                }

                // Fallback: construct from AWS Cognito fields (old format)
                var userPoolId = authConfig["userPoolId"]?.ToString();
                var awsRegion = authConfig["awsRegion"]?.ToString();
                
                if (!string.IsNullOrEmpty(userPoolId) && !string.IsNullOrEmpty(awsRegion))
                {
                    return $"https://cognito-idp.{awsRegion}.amazonaws.com/{userPoolId}/.well-known/openid-configuration";
                }

                // Try to construct from authority
                var authority = authConfig["authority"]?.ToString();
                if (!string.IsNullOrEmpty(authority))
                {
                    return $"{authority.TrimEnd('/')}/.well-known/openid-configuration";
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GetMetadataUrl] Error getting MetadataUrl");
        }

        return null;
    }

    /// <summary>
    /// Ensures the discovery document is loaded and cached.
    /// Call this during initialization to pre-fetch the discovery document.
    /// </summary>
    public async Task InitializeDiscoveryAsync()
    {
        if (_discoveryInitialized)
            return;

        var metadataUrl = GetMetadataUrl();
        if (string.IsNullOrEmpty(metadataUrl))
        {
            _logger.LogWarning("[InitializeDiscoveryAsync] No MetadataUrl available, skipping discovery initialization");
            return;
        }

        _logger.LogInformation("[InitializeDiscoveryAsync] Initializing discovery document from {MetadataUrl}", metadataUrl);
        
        var document = await _discoveryService.GetDiscoveryDocumentAsync(metadataUrl);
        if (document != null)
        {
            _cachedEndSessionEndpoint = document.EndSessionEndpoint;
            _discoveryInitialized = true;
            _logger.LogInformation("[InitializeDiscoveryAsync] Discovery initialized. EndSessionEndpoint: {EndSessionEndpoint}", 
                _cachedEndSessionEndpoint ?? "not provided");
        }
    }

    /// <summary>
    /// Gets the end_session_endpoint from the OpenID discovery document asynchronously.
    /// </summary>
    public async Task<string?> GetEndSessionEndpointAsync()
    {
        // Return cached value if available
        if (!string.IsNullOrEmpty(_cachedEndSessionEndpoint))
        {
            return _cachedEndSessionEndpoint;
        }

        var metadataUrl = GetMetadataUrl();
        if (string.IsNullOrEmpty(metadataUrl))
        {
            _logger.LogWarning("[GetEndSessionEndpointAsync] No MetadataUrl available");
            return null;
        }

        _cachedEndSessionEndpoint = await _discoveryService.GetEndSessionEndpointAsync(metadataUrl);
        _discoveryInitialized = true;
        
        return _cachedEndSessionEndpoint;
    }

    /// <summary>
    /// Gets the provider's logout endpoint URL (if supported).
    /// Uses cached end_session_endpoint from discovery document if available,
    /// otherwise falls back to constructing the URL from configuration.
    /// </summary>
    public string? GetLogoutEndpoint()
    {
        try
        {
            _logger.LogInformation("[GetLogoutEndpoint][{Timestamp}] Getting logout endpoint - SelectedAuthConfig: {SelectedAuthConfig}", 
                DateTime.UtcNow.ToString("HH:mm:ss.fff"), _oidcConfig.SelectedAuthConfig);

            // First, try to use the cached end_session_endpoint from discovery document
            if (!string.IsNullOrEmpty(_cachedEndSessionEndpoint))
            {
                _logger.LogInformation("[GetLogoutEndpoint][{Timestamp}] Using cached end_session_endpoint from discovery document: {EndSessionEndpoint}", 
                    DateTime.UtcNow.ToString("HH:mm:ss.fff"), _cachedEndSessionEndpoint);
                return _cachedEndSessionEndpoint;
            }

            _logger.LogInformation("[GetLogoutEndpoint][{Timestamp}] No cached end_session_endpoint, falling back to configuration-based construction", 
                DateTime.UtcNow.ToString("HH:mm:ss.fff"));

            // Fallback to configuration-based construction
            return GetLogoutEndpointFromConfig();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GetLogoutEndpoint][{Timestamp}] Error getting logout endpoint", DateTime.UtcNow.ToString("HH:mm:ss.fff"));
        }

        _logger.LogWarning("[GetLogoutEndpoint][{Timestamp}] Returning null for logout endpoint", DateTime.UtcNow.ToString("HH:mm:ss.fff"));
        return null;
    }

    /// <summary>
    /// Gets the logout endpoint by constructing it from configuration (legacy fallback)
    /// </summary>
    private string? GetLogoutEndpointFromConfig()
    {
        if (!_oidcConfig.AuthConfigs.TryGetValue(_oidcConfig.SelectedAuthConfig, out var authConfig))
        {
            _logger.LogWarning("[GetLogoutEndpointFromConfig] Could not find authConfig for '{SelectedAuthConfig}'", _oidcConfig.SelectedAuthConfig);
            return null;
        }

        _logger.LogInformation("[GetLogoutEndpointFromConfig] Found authConfig for {SelectedAuthConfig}", _oidcConfig.SelectedAuthConfig);
        
        // Debug: Show all available fields in authConfig
        var allFields = new List<string>();
        foreach (var property in authConfig.Properties())
        {
            allFields.Add($"{property.Name}={property.Value}");
        }
        _logger.LogInformation("[GetLogoutEndpointFromConfig] All fields in authConfig: {Fields}", string.Join(", ", allFields));
        
        // Try to get HostedUIDomain first (new config format)
        var hostedUIDomain = authConfig["HostedUIDomain"]?.ToString();
        _logger.LogInformation("[GetLogoutEndpointFromConfig] HostedUIDomain field: '{HostedUIDomain}'", hostedUIDomain);
        
        if (!string.IsNullOrEmpty(hostedUIDomain))
        {
            var logoutEndpoint = $"{hostedUIDomain.TrimEnd('/')}/logout";
            _logger.LogInformation("[GetLogoutEndpointFromConfig] Using HostedUIDomain logout endpoint: {LogoutEndpoint}", logoutEndpoint);
            return logoutEndpoint;
        }

        // Fallback: Try to get cognitoDomain directly from config (old format)
        var cognitoDomain = authConfig["cognitoDomain"]?.ToString();
        _logger.LogInformation("[GetLogoutEndpointFromConfig] Direct cognitoDomain field: '{CognitoDomain}'", cognitoDomain);
        
        if (!string.IsNullOrEmpty(cognitoDomain))
        {
            var logoutEndpoint = $"{cognitoDomain.TrimEnd('/')}/logout";
            _logger.LogInformation("[GetLogoutEndpointFromConfig] Using direct cognitoDomain logout endpoint: {LogoutEndpoint}", logoutEndpoint);
            return logoutEndpoint;
        }

        // Fallback: construct from AWS region and domain if available
        var awsRegion = authConfig["awsRegion"]?.ToString();
        var domainPrefix = authConfig["cognitoDomainPrefix"]?.ToString();
        
        _logger.LogInformation("[GetLogoutEndpointFromConfig] Fallback fields - awsRegion: '{AwsRegion}', cognitoDomainPrefix: '{DomainPrefix}'", awsRegion, domainPrefix);
        
        if (!string.IsNullOrEmpty(awsRegion) && !string.IsNullOrEmpty(domainPrefix))
        {
            var constructedLogoutEndpoint = $"https://{domainPrefix}.auth.{awsRegion}.amazoncognito.com/logout";
            _logger.LogInformation("[GetLogoutEndpointFromConfig] Using constructed logout endpoint: {ConstructedLogoutEndpoint}", constructedLogoutEndpoint);
            return constructedLogoutEndpoint;
        }
        
        _logger.LogWarning("[GetLogoutEndpointFromConfig] No valid domain fields found");
        return null;
    }

    /// <summary>
    /// Gets the OIDC Client ID. First checks for client-specified override in ILzHost,
    /// then falls back to the server-provided configuration.
    /// </summary>
    public string? GetClientId()
    {
        try
        {
            _logger.LogInformation("[GetClientId][{Timestamp}] Getting ClientId for SelectedAuthConfig: {SelectedAuthConfig}", DateTime.UtcNow.ToString("HH:mm:ss.fff"), _oidcConfig.SelectedAuthConfig);

            // First check for client-specified override
            if (!string.IsNullOrEmpty(_lzHost.ClientId))
            {
                _logger.LogInformation("[GetClientId][{Timestamp}] Using client-specified ClientId from ILzHost: {ClientId}", DateTime.UtcNow.ToString("HH:mm:ss.fff"), _lzHost.ClientId);
                return _lzHost.ClientId;
            }

            if (_oidcConfig.AuthConfigs.TryGetValue(_oidcConfig.SelectedAuthConfig, out var authConfig))
            {
                _logger.LogInformation("[GetClientId][{Timestamp}] Found authConfig for {SelectedAuthConfig}", DateTime.UtcNow.ToString("HH:mm:ss.fff"), _oidcConfig.SelectedAuthConfig);

                // Try ClientId first (new config format)
                var clientId = authConfig["ClientId"]?.ToString();
                _logger.LogInformation("[GetClientId][{Timestamp}] Checking ClientId: '{ClientId}'", DateTime.UtcNow.ToString("HH:mm:ss.fff"), clientId);
                if (!string.IsNullOrEmpty(clientId))
                {
                    _logger.LogInformation("[GetClientId][{Timestamp}] Using ClientId: {ClientId}", DateTime.UtcNow.ToString("HH:mm:ss.fff"), clientId);
                    return clientId;
                }

                // Try userPoolClientId (old AWS Cognito format)
                clientId = authConfig["userPoolClientId"]?.ToString();
                _logger.LogInformation("[GetClientId][{Timestamp}] Checking userPoolClientId: '{UserPoolClientId}'", DateTime.UtcNow.ToString("HH:mm:ss.fff"), clientId);
                if (!string.IsNullOrEmpty(clientId))
                {
                    _logger.LogInformation("[GetClientId][{Timestamp}] Using userPoolClientId: {UserPoolClientId}", DateTime.UtcNow.ToString("HH:mm:ss.fff"), clientId);
                    return clientId;
                }

                // Fallback to generic clientId
                clientId = authConfig["clientId"]?.ToString();
                _logger.LogInformation("[GetClientId][{Timestamp}] Checking clientId: '{GenericClientId}'", DateTime.UtcNow.ToString("HH:mm:ss.fff"), clientId);
                if (!string.IsNullOrEmpty(clientId))
                {
                    _logger.LogInformation("[GetClientId][{Timestamp}] Using clientId: {GenericClientId}", DateTime.UtcNow.ToString("HH:mm:ss.fff"), clientId);
                    return clientId;
                }

                _logger.LogWarning("[GetClientId][{Timestamp}] No ClientId found in any format", DateTime.UtcNow.ToString("HH:mm:ss.fff"));
            }
            else
            {
                _logger.LogWarning("[GetClientId][{Timestamp}] Could not find authConfig for '{SelectedAuthConfig}'", DateTime.UtcNow.ToString("HH:mm:ss.fff"), _oidcConfig.SelectedAuthConfig);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GetClientId][{Timestamp}] Error getting Client ID", DateTime.UtcNow.ToString("HH:mm:ss.fff"));
        }

        return null;
    }

    /// <summary>
    /// Gets the Authority from the dynamic configuration
    /// </summary>
    public string? GetAuthority()
    {
        try
        {
            if (_oidcConfig.AuthConfigs.TryGetValue(_oidcConfig.SelectedAuthConfig, out var authConfig))
            {
                // Both casings — server-side config emitters vary (CFAuthConfig
                // uses PascalCase to match C# property names; older configs
                // use lowercase).
                return authConfig["authority"]?.ToString()
                    ?? authConfig["Authority"]?.ToString();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting Authority");
        }

        return null;
    }

    /// <summary>
    /// Builds the complete logout URL for the current provider (synchronous version).
    /// Uses cached end_session_endpoint if available.
    /// </summary>
    public string? BuildLogoutUrl(string postLogoutRedirectUri, string? idTokenHint = null)
    {
        _logger.LogInformation("[BuildLogoutUrl][{Timestamp}] Building logout URL for redirect URI: {PostLogoutRedirectUri}", 
            DateTime.UtcNow.ToString("HH:mm:ss.fff"), postLogoutRedirectUri);
        
        var providerType = GetProviderType();
        var logoutEndpoint = GetLogoutEndpoint();
        var clientId = GetClientId();
        
        return BuildLogoutUrlInternal(logoutEndpoint, clientId, providerType, postLogoutRedirectUri, idTokenHint);
    }

    /// <summary>
    /// Builds the complete logout URL for the current provider asynchronously.
    /// This ensures the end_session_endpoint is fetched from the discovery document.
    /// </summary>
    public async Task<string?> BuildLogoutUrlAsync(string postLogoutRedirectUri, string? idTokenHint = null)
    {
        _logger.LogInformation("[BuildLogoutUrlAsync][{Timestamp}] Building logout URL asynchronously for redirect URI: {PostLogoutRedirectUri}, IdTokenHint: {HasIdToken}", 
            DateTime.UtcNow.ToString("HH:mm:ss.fff"), postLogoutRedirectUri, !string.IsNullOrEmpty(idTokenHint));
        
        var providerType = GetProviderType();
        var clientId = GetClientId();
        
        // Try to get end_session_endpoint from discovery document first
        var logoutEndpoint = await GetEndSessionEndpointAsync();
        
        // Fall back to configuration-based endpoint if discovery didn't provide one
        if (string.IsNullOrEmpty(logoutEndpoint))
        {
            _logger.LogInformation("[BuildLogoutUrlAsync][{Timestamp}] No end_session_endpoint from discovery, falling back to configuration", 
                DateTime.UtcNow.ToString("HH:mm:ss.fff"));
            logoutEndpoint = GetLogoutEndpointFromConfig();
        }
        
        return BuildLogoutUrlInternal(logoutEndpoint, clientId, providerType, postLogoutRedirectUri, idTokenHint);
    }

    /// <summary>
    /// Internal method to build the logout URL with proper query parameters
    /// </summary>
    private string? BuildLogoutUrlInternal(string? logoutEndpoint, string? clientId, string? providerType, string postLogoutRedirectUri, string? idTokenHint = null)
    {
        var postLogoutRedirectEnabled = IsPostLogoutRedirectEnabled();
        _logger.LogInformation("[BuildLogoutUrlInternal] Logout URL components - Provider: '{ProviderType}', Endpoint: '{LogoutEndpoint}', ClientId: '{ClientId}', HasIdTokenHint: {HasIdTokenHint}, PostLogoutRedirectEnabled: {RedirectEnabled}", 
            providerType, logoutEndpoint, clientId, !string.IsNullOrEmpty(idTokenHint), postLogoutRedirectEnabled);
        
        if (string.IsNullOrEmpty(logoutEndpoint))
        {
            _logger.LogWarning("[BuildLogoutUrlInternal] Missing logout endpoint");
            return null;
        }
        
        var escapedRedirectUri = Uri.EscapeDataString(postLogoutRedirectUri);
        _logger.LogInformation("[BuildLogoutUrlInternal] Escaped redirect URI: {EscapedRedirectUri}", escapedRedirectUri);
        
        string logoutUrl;
        
        switch (providerType?.ToLower())
        {
            case "cognito":
                // Cognito uses logout_uri and requires client_id
                if (string.IsNullOrEmpty(clientId))
                {
                    _logger.LogWarning("[BuildLogoutUrlInternal] Cognito requires client_id but it's missing");
                    return null;
                }
                logoutUrl = $"{logoutEndpoint}?client_id={clientId}&logout_uri={escapedRedirectUri}";
                break;
                
            case "auth0":
                // Auth0 uses returnTo and requires client_id
                if (string.IsNullOrEmpty(clientId))
                {
                    _logger.LogWarning("[BuildLogoutUrlInternal] Auth0 requires client_id but it's missing");
                    return null;
                }
                logoutUrl = $"{logoutEndpoint.TrimEnd('/')}/v2/logout?client_id={clientId}&returnTo={escapedRedirectUri}";
                break;
                
            case "okta":
                // Okta uses id_token_hint and post_logout_redirect_uri
                logoutUrl = $"{logoutEndpoint}?post_logout_redirect_uri={escapedRedirectUri}";
                if (!string.IsNullOrEmpty(idTokenHint))
                {
                    logoutUrl += $"&id_token_hint={idTokenHint}";
                }
                break;
                
            case "azuread":
                // Azure AD uses post_logout_redirect_uri, id_token_hint is optional
                logoutUrl = $"{logoutEndpoint}?post_logout_redirect_uri={escapedRedirectUri}";
                if (!string.IsNullOrEmpty(idTokenHint))
                {
                    logoutUrl += $"&id_token_hint={idTokenHint}";
                }
                break;
                
            case "keycloak":
                // Keycloak RP-initiated logout requires post_logout_redirect_uri to be registered
                // in "Valid post logout redirect URIs" in the client settings.
                // If not configured, Keycloak returns 400 Bad Request.
                // 
                // With id_token_hint, Keycloak can logout without user confirmation (silent logout).
                // Without id_token_hint, Keycloak shows a confirmation page.
                if (!string.IsNullOrEmpty(idTokenHint))
                {
                    if (postLogoutRedirectEnabled)
                    {
                        // Full RP-initiated logout with redirect
                        logoutUrl = $"{logoutEndpoint}?id_token_hint={idTokenHint}&post_logout_redirect_uri={escapedRedirectUri}";
                        if (!string.IsNullOrEmpty(clientId))
                        {
                            logoutUrl += $"&client_id={clientId}";
                        }
                    }
                    else
                    {
                        // Logout without redirect - Keycloak will show "You have been logged out" page
                        _logger.LogInformation("[BuildLogoutUrlInternal] Keycloak logout without redirect (postLogoutRedirectEnabled=false)");
                        logoutUrl = $"{logoutEndpoint}?id_token_hint={idTokenHint}";
                        if (!string.IsNullOrEmpty(clientId))
                        {
                            logoutUrl += $"&client_id={clientId}";
                        }
                    }
                }
                else
                {
                    // No id_token_hint - Keycloak will show confirmation page
                    _logger.LogWarning("[BuildLogoutUrlInternal] Keycloak logout without id_token_hint - user will see confirmation page");
                    logoutUrl = logoutEndpoint;
                }
                break;
                
            default:
                // Generic OIDC - use standard parameters with id_token_hint if available
                logoutUrl = $"{logoutEndpoint}?post_logout_redirect_uri={escapedRedirectUri}";
                if (!string.IsNullOrEmpty(idTokenHint))
                {
                    logoutUrl += $"&id_token_hint={idTokenHint}";
                }
                // Also include client_id if available (some providers require it)
                if (!string.IsNullOrEmpty(clientId))
                {
                    logoutUrl += $"&client_id={clientId}";
                }
                break;
        }
        
        _logger.LogInformation("[BuildLogoutUrlInternal] Final logout URL constructed: {LogoutUrl}", logoutUrl);
        _logger.LogInformation("[BuildLogoutUrlInternal] Provider type matched: {ProviderTypeMatched}", providerType?.ToLower());
        
        return logoutUrl;
    }
    
    /// <summary>
    /// Gets the provider type from configuration
    /// </summary>
    public string? GetProviderType()
    {
        try
        {
            if (_oidcConfig.AuthConfigs.TryGetValue(_oidcConfig.SelectedAuthConfig, out var authConfig))
            {
                // Try to get explicit provider type
                var providerType = authConfig["providerType"]?.ToString();
                if (!string.IsNullOrEmpty(providerType))
                {
                    return providerType;
                }
                
                // Try to infer from authority URL
                var authority = authConfig["authority"]?.ToString();
                if (!string.IsNullOrEmpty(authority))
                {
                    if (authority.Contains("amazoncognito.com")) return "cognito";
                    if (authority.Contains("auth0.com")) return "auth0";
                    if (authority.Contains("okta.com")) return "okta";
                    if (authority.Contains("microsoftonline.com")) return "azuread";
                    // Keycloak URLs typically contain /realms/ in the path
                    if (authority.Contains("/realms/")) return "keycloak";
                }
                
                // Also check MetadataUrl for Keycloak pattern
                var metadataUrlForKeycloak = authConfig["MetadataUrl"]?.ToString() ?? authConfig["metadataUrl"]?.ToString();
                if (!string.IsNullOrEmpty(metadataUrlForKeycloak) && metadataUrlForKeycloak.Contains("/realms/"))
                {
                    return "keycloak";
                }
                
                // Check for Cognito-specific fields (new format)
                var hostedUIDomain = authConfig["HostedUIDomain"]?.ToString();
                var metadataUrl = authConfig["MetadataUrl"]?.ToString();
                
                if (!string.IsNullOrEmpty(hostedUIDomain) && hostedUIDomain.Contains("amazoncognito.com"))
                {
                    return "cognito";
                }
                
                if (!string.IsNullOrEmpty(metadataUrl) && metadataUrl.Contains("cognito-idp"))
                {
                    return "cognito";
                }
                
                // Check for Cognito-specific fields (old format)
                if (authConfig["cognitoDomain"] != null || authConfig["userPoolClientId"] != null)
                {
                    return "cognito";
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting provider type");
        }
        
        return "oidc"; // Generic OIDC fallback
    }

    /// <summary>
    /// Gets a configuration value by key
    /// </summary>
    public string? GetValue(string key)
    {
        return key switch
        {
            "oidc:logout_endpoint" or "Cognito:CognitoDomain" => GetLogoutEndpoint(),
            "oidc:client_id" or "Cognito:ClientId" => GetClientId(),
            "oidc:authority" or "Cognito:Authority" => GetAuthority(),
            "oidc:metadata_url" => GetMetadataUrl(),
            "oidc:provider_type" => GetProviderType(),
            _ => null
        };
    }

    /// <summary>
    /// Determines if the provider has native remember-me support.
    /// Providers like Keycloak have built-in "Remember Me" in their login UI.
    /// </summary>
    public bool HasNativeRememberMe()
    {
        var providerType = GetProviderType()?.ToLower();
        
        // Providers with native remember-me support
        return providerType switch
        {
            "keycloak" => true,      // Keycloak has built-in "Remember Me" checkbox
            "auth0" => true,         // Auth0 supports "Remember this device"
            "okta" => true,          // Okta supports "Keep me signed in"
            "azuread" => true,       // Azure AD supports "Stay signed in"
            "cognito" => false,      // Cognito does NOT have native remember-me
            _ => false               // Default: assume no native support
        };
    }

    /// <summary>
    /// Determines if the application should manage remember-me token storage.
    /// Returns false for providers with native remember-me support (e.g., Keycloak),
    /// where the provider handles session persistence.
    /// Returns true for providers without native remember-me (e.g., Cognito),
    /// where the application must store tokens locally.
    /// Can be overridden via "rememberMeEnabled" in config.
    /// </summary>
    public bool RequiresClientSideTokenStorage()
    {
        var providerType = GetProviderType();
        _logger.LogInformation("[RequiresClientSideTokenStorage] Called. ProviderType={ProviderType}, SelectedConfig={SelectedConfig}", 
            providerType, _oidcConfig.SelectedAuthConfig);
        
        try
        {
            if (_oidcConfig.AuthConfigs.TryGetValue(_oidcConfig.SelectedAuthConfig, out var authConfig))
            {
                // Check for explicit config override first
                var rememberMeEnabled = authConfig["rememberMeEnabled"]?.ToString();
                if (!string.IsNullOrEmpty(rememberMeEnabled))
                {
                    var enabled = rememberMeEnabled.Equals("true", StringComparison.OrdinalIgnoreCase);
                    _logger.LogInformation("[RequiresClientSideTokenStorage] Config override found: rememberMeEnabled={Enabled}", enabled);
                    return enabled;
                }
                
                // Also check lowercase version
                rememberMeEnabled = authConfig["remembermeenabled"]?.ToString();
                if (!string.IsNullOrEmpty(rememberMeEnabled))
                {
                    var enabled = rememberMeEnabled.Equals("true", StringComparison.OrdinalIgnoreCase);
                    _logger.LogInformation("[RequiresClientSideTokenStorage] Config override found: remembermeenabled={Enabled}", enabled);
                    return enabled;
                }
            }
            else
            {
                _logger.LogWarning("[RequiresClientSideTokenStorage] AuthConfig not found for '{SelectedConfig}'", _oidcConfig.SelectedAuthConfig);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[RequiresClientSideTokenStorage] Error reading config");
        }

        // Default: require client-side storage only if provider doesn't have native remember-me
        var hasNativeRememberMe = HasNativeRememberMe();
        var requiresStorage = !hasNativeRememberMe;
        _logger.LogInformation("[RequiresClientSideTokenStorage] HasNativeRememberMe={HasNative}, RequiresStorage={Requires}", 
            hasNativeRememberMe, requiresStorage);
        return requiresStorage;
    }

    /// <summary>
    /// Determines if post-logout redirect is enabled.
    /// When true (default), logout will redirect back to the application.
    /// When false, logout will end at the provider's logout confirmation page.
    /// Set "postLogoutRedirectEnabled": false in config to disable.
    /// </summary>
    public bool IsPostLogoutRedirectEnabled()
    {
        try
        {
            if (_oidcConfig.AuthConfigs.TryGetValue(_oidcConfig.SelectedAuthConfig, out var authConfig))
            {
                // Check for explicit config override
                var postLogoutRedirectEnabled = authConfig["postLogoutRedirectEnabled"]?.ToString();
                if (!string.IsNullOrEmpty(postLogoutRedirectEnabled))
                {
                    var enabled = postLogoutRedirectEnabled.Equals("true", StringComparison.OrdinalIgnoreCase);
                    _logger.LogInformation("[IsPostLogoutRedirectEnabled] Config override: {Enabled}", enabled);
                    return enabled;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[IsPostLogoutRedirectEnabled] Error reading config");
        }

        // Default: post-logout redirect is enabled
        return true;
    }

}
