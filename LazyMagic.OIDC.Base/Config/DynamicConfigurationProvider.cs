namespace LazyMagic.OIDC.Base;

/// <summary>
/// Provides configuration values from dynamically loaded config
/// Acts as a bridge between the dynamic config and IConfiguration consumers
/// </summary>
public class DynamicConfigurationProvider : IDynamicConfigurationProvider
{
    private readonly IOidcConfig _oidcConfig;
    private readonly ILogger<DynamicConfigurationProvider> _logger;

    public DynamicConfigurationProvider(IOidcConfig oidcConfig, ILogger<DynamicConfigurationProvider> logger)
    {
        _oidcConfig = oidcConfig;
        _logger = logger;
    }

    /// <summary>
    /// Gets the provider's logout endpoint URL (if supported)
    /// </summary>
    public string? GetLogoutEndpoint()
    {
        try
        {
            _logger.LogInformation("[GetLogoutEndpoint][{Timestamp}] Getting Cognito domain - SelectedAuthConfig: {SelectedAuthConfig}", DateTime.UtcNow.ToString("HH:mm:ss.fff"), _oidcConfig.SelectedAuthConfig);
            _logger.LogInformation("[GetLogoutEndpoint][{Timestamp}] Available AuthConfigs: {AuthConfigs}", DateTime.UtcNow.ToString("HH:mm:ss.fff"), string.Join(", ", _oidcConfig.AuthConfigs.Keys));
            
            if (_oidcConfig.AuthConfigs.TryGetValue(_oidcConfig.SelectedAuthConfig, out var authConfig))
            {
                _logger.LogInformation("[GetLogoutEndpoint][{Timestamp}] Found authConfig for {SelectedAuthConfig}", DateTime.UtcNow.ToString("HH:mm:ss.fff"), _oidcConfig.SelectedAuthConfig);
                
                // Debug: Show all available fields in authConfig
                var allFields = new List<string>();
                foreach (var property in authConfig.Properties())
                {
                    allFields.Add($"{property.Name}={property.Value}");
                }
                _logger.LogInformation("[GetLogoutEndpoint][{Timestamp}] All fields in authConfig: {Fields}", DateTime.UtcNow.ToString("HH:mm:ss.fff"), string.Join(", ", allFields));
                
                // Try to get HostedUIDomain first (new config format)
                var hostedUIDomain = authConfig["HostedUIDomain"]?.ToString();
                _logger.LogInformation("[GetLogoutEndpoint][{Timestamp}] HostedUIDomain field: '{HostedUIDomain}'", DateTime.UtcNow.ToString("HH:mm:ss.fff"), hostedUIDomain);
                
                if (!string.IsNullOrEmpty(hostedUIDomain))
                {
                    var logoutEndpoint = $"{hostedUIDomain.TrimEnd('/')}/logout";
                    _logger.LogInformation("[GetLogoutEndpoint][{Timestamp}] Using HostedUIDomain logout endpoint: {LogoutEndpoint}", DateTime.UtcNow.ToString("HH:mm:ss.fff"), logoutEndpoint);
                    return logoutEndpoint;
                }

                // Fallback: Try to get cognitoDomain directly from config (old format)
                var cognitoDomain = authConfig["cognitoDomain"]?.ToString();
                _logger.LogInformation("[GetLogoutEndpoint][{Timestamp}] Direct cognitoDomain field: '{CognitoDomain}'", DateTime.UtcNow.ToString("HH:mm:ss.fff"), cognitoDomain);
                
                if (!string.IsNullOrEmpty(cognitoDomain))
                {
                    var logoutEndpoint = $"{cognitoDomain.TrimEnd('/')}/logout";
                    _logger.LogInformation("[GetLogoutEndpoint][{Timestamp}] Using direct cognitoDomain logout endpoint: {LogoutEndpoint}", DateTime.UtcNow.ToString("HH:mm:ss.fff"), logoutEndpoint);
                    return logoutEndpoint;
                }

                // Fallback: construct from AWS region and domain if available
                var awsRegion = authConfig["awsRegion"]?.ToString();
                var domainPrefix = authConfig["cognitoDomainPrefix"]?.ToString();
                
                _logger.LogInformation("[GetLogoutEndpoint][{Timestamp}] Fallback fields - awsRegion: '{AwsRegion}', cognitoDomainPrefix: '{DomainPrefix}'", DateTime.UtcNow.ToString("HH:mm:ss.fff"), awsRegion, domainPrefix);
                
                if (!string.IsNullOrEmpty(awsRegion) && !string.IsNullOrEmpty(domainPrefix))
                {
                    var constructedLogoutEndpoint = $"https://{domainPrefix}.auth.{awsRegion}.amazoncognito.com/logout";
                    _logger.LogInformation("[GetLogoutEndpoint][{Timestamp}] Using constructed logout endpoint: {ConstructedLogoutEndpoint}", DateTime.UtcNow.ToString("HH:mm:ss.fff"), constructedLogoutEndpoint);
                    return constructedLogoutEndpoint;
                }
                
                _logger.LogWarning("[GetLogoutEndpoint][{Timestamp}] No valid domain fields found", DateTime.UtcNow.ToString("HH:mm:ss.fff"));
            }
            else
            {
                _logger.LogWarning("[GetLogoutEndpoint][{Timestamp}] Could not find authConfig for '{SelectedAuthConfig}'", DateTime.UtcNow.ToString("HH:mm:ss.fff"), _oidcConfig.SelectedAuthConfig);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GetLogoutEndpoint][{Timestamp}] Error getting Cognito domain", DateTime.UtcNow.ToString("HH:mm:ss.fff"));
        }

        _logger.LogWarning("[GetLogoutEndpoint][{Timestamp}] Returning null for Cognito domain", DateTime.UtcNow.ToString("HH:mm:ss.fff"));
        return null;
    }

    /// <summary>
    /// Gets the Cognito Client ID from the dynamic configuration
    /// </summary>
    public string? GetClientId()
    {
        try
        {
            _logger.LogInformation("[GetClientId][{Timestamp}] Getting ClientId for SelectedAuthConfig: {SelectedAuthConfig}", DateTime.UtcNow.ToString("HH:mm:ss.fff"), _oidcConfig.SelectedAuthConfig);
            
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
                return authConfig["authority"]?.ToString();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting Authority");
        }

        return null;
    }

    /// <summary>
    /// Builds the complete logout URL for the current provider
    /// </summary>
    public string? BuildLogoutUrl(string postLogoutRedirectUri)
    {
        _logger.LogInformation("[BuildLogoutUrl][{Timestamp}] Building logout URL for redirect URI: {PostLogoutRedirectUri}", DateTime.UtcNow.ToString("HH:mm:ss.fff"), postLogoutRedirectUri);
        
        var providerType = GetProviderType();
        var logoutEndpoint = GetLogoutEndpoint();
        var clientId = GetClientId();
        
        _logger.LogInformation("[BuildLogoutUrl][{Timestamp}] Logout URL components - Provider: '{ProviderType}', Endpoint: '{LogoutEndpoint}', ClientId: '{ClientId}'", DateTime.UtcNow.ToString("HH:mm:ss.fff"), providerType, logoutEndpoint, clientId);
        
        if (string.IsNullOrEmpty(logoutEndpoint) || string.IsNullOrEmpty(clientId))
        {
            _logger.LogWarning("[BuildLogoutUrl][{Timestamp}] Missing logout endpoint or client ID - Endpoint: '{LogoutEndpoint}', ClientId: '{ClientId}'", DateTime.UtcNow.ToString("HH:mm:ss.fff"), logoutEndpoint, clientId);
            return null;
        }
        
        var escapedRedirectUri = Uri.EscapeDataString(postLogoutRedirectUri);
        _logger.LogInformation("[BuildLogoutUrl][{Timestamp}] Escaped redirect URI: {EscapedRedirectUri}", DateTime.UtcNow.ToString("HH:mm:ss.fff"), escapedRedirectUri);
        
        var logoutUrl = providerType?.ToLower() switch
        {
            "cognito" => $"{logoutEndpoint}?client_id={clientId}&logout_uri={escapedRedirectUri}",
            "auth0" => $"{logoutEndpoint.TrimEnd('/')}/v2/logout?client_id={clientId}&returnTo={escapedRedirectUri}",
            "okta" => $"{logoutEndpoint}?id_token_hint={{id_token}}&post_logout_redirect_uri={escapedRedirectUri}",
            "azuread" => $"{logoutEndpoint}?post_logout_redirect_uri={escapedRedirectUri}",
            _ => $"{logoutEndpoint}?post_logout_redirect_uri={escapedRedirectUri}" // Generic OIDC
        };
        
        _logger.LogInformation("[BuildLogoutUrl][{Timestamp}] Final logout URL constructed: {LogoutUrl}", DateTime.UtcNow.ToString("HH:mm:ss.fff"), logoutUrl);
        _logger.LogInformation("[BuildLogoutUrl][{Timestamp}] Provider type matched: {ProviderTypeMatched}", DateTime.UtcNow.ToString("HH:mm:ss.fff"), providerType?.ToLower());
        
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
            "oidc:provider_type" => GetProviderType(),
            _ => null
        };
    }
}