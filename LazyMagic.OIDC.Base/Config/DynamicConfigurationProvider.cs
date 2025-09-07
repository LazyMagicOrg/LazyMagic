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
            _logger.LogInformation("Getting Cognito domain - SelectedAuthConfig: {SelectedAuthConfig}", _oidcConfig.SelectedAuthConfig);
            _logger.LogInformation("Available AuthConfigs: {AuthConfigs}", string.Join(", ", _oidcConfig.AuthConfigs.Keys));
            
            if (_oidcConfig.AuthConfigs.TryGetValue(_oidcConfig.SelectedAuthConfig, out var authConfig))
            {
                _logger.LogInformation("Found authConfig for {SelectedAuthConfig}", _oidcConfig.SelectedAuthConfig);
                
                // Debug: Show all available fields in authConfig
                var allFields = new List<string>();
                foreach (var property in authConfig.Properties())
                {
                    allFields.Add($"{property.Name}={property.Value}");
                }
                _logger.LogInformation("All fields in authConfig: {Fields}", string.Join(", ", allFields));
                
                // Try to get HostedUIDomain first (new config format)
                var hostedUIDomain = authConfig["HostedUIDomain"]?.ToString();
                _logger.LogInformation("HostedUIDomain field: '{HostedUIDomain}'", hostedUIDomain);
                
                if (!string.IsNullOrEmpty(hostedUIDomain))
                {
                    var logoutEndpoint = $"{hostedUIDomain.TrimEnd('/')}/logout";
                    _logger.LogInformation("Using HostedUIDomain logout endpoint: {LogoutEndpoint}", logoutEndpoint);
                    return logoutEndpoint;
                }

                // Fallback: Try to get cognitoDomain directly from config (old format)
                var cognitoDomain = authConfig["cognitoDomain"]?.ToString();
                _logger.LogInformation("Direct cognitoDomain field: '{CognitoDomain}'", cognitoDomain);
                
                if (!string.IsNullOrEmpty(cognitoDomain))
                {
                    var logoutEndpoint = $"{cognitoDomain.TrimEnd('/')}/logout";
                    _logger.LogInformation("Using direct cognitoDomain logout endpoint: {LogoutEndpoint}", logoutEndpoint);
                    return logoutEndpoint;
                }

                // Fallback: construct from AWS region and domain if available
                var awsRegion = authConfig["awsRegion"]?.ToString();
                var domainPrefix = authConfig["cognitoDomainPrefix"]?.ToString();
                
                _logger.LogInformation("Fallback fields - awsRegion: '{AwsRegion}', cognitoDomainPrefix: '{DomainPrefix}'", awsRegion, domainPrefix);
                
                if (!string.IsNullOrEmpty(awsRegion) && !string.IsNullOrEmpty(domainPrefix))
                {
                    var constructedLogoutEndpoint = $"https://{domainPrefix}.auth.{awsRegion}.amazoncognito.com/logout";
                    _logger.LogInformation("Using constructed logout endpoint: {ConstructedLogoutEndpoint}", constructedLogoutEndpoint);
                    return constructedLogoutEndpoint;
                }
                
                _logger.LogWarning("No valid domain fields found");
            }
            else
            {
                _logger.LogWarning("Could not find authConfig for '{SelectedAuthConfig}'", _oidcConfig.SelectedAuthConfig);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting Cognito domain");
        }

        _logger.LogWarning("Returning null for Cognito domain");
        return null;
    }

    /// <summary>
    /// Gets the Cognito Client ID from the dynamic configuration
    /// </summary>
    public string? GetClientId()
    {
        try
        {
            _logger.LogInformation("Getting ClientId for SelectedAuthConfig: {SelectedAuthConfig}", _oidcConfig.SelectedAuthConfig);
            
            if (_oidcConfig.AuthConfigs.TryGetValue(_oidcConfig.SelectedAuthConfig, out var authConfig))
            {
                _logger.LogInformation("Found authConfig for {SelectedAuthConfig}", _oidcConfig.SelectedAuthConfig);
                
                // Try ClientId first (new config format)
                var clientId = authConfig["ClientId"]?.ToString();
                _logger.LogInformation("Checking ClientId: '{ClientId}'", clientId);
                if (!string.IsNullOrEmpty(clientId))
                {
                    _logger.LogInformation("Using ClientId: {ClientId}", clientId);
                    return clientId;
                }

                // Try userPoolClientId (old AWS Cognito format)
                clientId = authConfig["userPoolClientId"]?.ToString();
                _logger.LogInformation("Checking userPoolClientId: '{UserPoolClientId}'", clientId);
                if (!string.IsNullOrEmpty(clientId))
                {
                    _logger.LogInformation("Using userPoolClientId: {UserPoolClientId}", clientId);
                    return clientId;
                }

                // Fallback to generic clientId
                clientId = authConfig["clientId"]?.ToString();
                _logger.LogInformation("Checking clientId: '{GenericClientId}'", clientId);
                if (!string.IsNullOrEmpty(clientId))
                {
                    _logger.LogInformation("Using clientId: {GenericClientId}", clientId);
                    return clientId;
                }
                
                _logger.LogWarning("No ClientId found in any format");
            }
            else
            {
                _logger.LogWarning("Could not find authConfig for '{SelectedAuthConfig}'", _oidcConfig.SelectedAuthConfig);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting Client ID");
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
        _logger.LogInformation("Building logout URL for redirect URI: {PostLogoutRedirectUri}", postLogoutRedirectUri);
        
        var providerType = GetProviderType();
        var logoutEndpoint = GetLogoutEndpoint();
        var clientId = GetClientId();
        
        _logger.LogInformation("Logout URL components - Provider: '{ProviderType}', Endpoint: '{LogoutEndpoint}', ClientId: '{ClientId}'", providerType, logoutEndpoint, clientId);
        
        if (string.IsNullOrEmpty(logoutEndpoint) || string.IsNullOrEmpty(clientId))
        {
            _logger.LogWarning("Missing logout endpoint or client ID - Endpoint: '{LogoutEndpoint}', ClientId: '{ClientId}'", logoutEndpoint, clientId);
            return null;
        }
        
        var logoutUrl = providerType?.ToLower() switch
        {
            "cognito" => $"{logoutEndpoint}?client_id={clientId}&logout_uri={Uri.EscapeDataString(postLogoutRedirectUri)}",
            "auth0" => $"{logoutEndpoint.TrimEnd('/')}/v2/logout?client_id={clientId}&returnTo={Uri.EscapeDataString(postLogoutRedirectUri)}",
            "okta" => $"{logoutEndpoint}?id_token_hint={{id_token}}&post_logout_redirect_uri={Uri.EscapeDataString(postLogoutRedirectUri)}",
            "azuread" => $"{logoutEndpoint}?post_logout_redirect_uri={Uri.EscapeDataString(postLogoutRedirectUri)}",
            _ => $"{logoutEndpoint}?post_logout_redirect_uri={Uri.EscapeDataString(postLogoutRedirectUri)}" // Generic OIDC
        };
        
        _logger.LogInformation("Built logout URL: {LogoutUrl}", logoutUrl);
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