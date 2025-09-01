namespace LazyMagic.OIDC.MAUI;

/// <summary>
/// MAUI implementation of IDynamicConfigurationProvider
/// Provides configuration values from dynamically loaded config
/// </summary>
public class DynamicConfigurationProvider : IDynamicConfigurationProvider
{
    private readonly IOidcConfig _oidcConfig;

    public DynamicConfigurationProvider(IOidcConfig oidcConfig)
    {
        _oidcConfig = oidcConfig;
    }

    /// <summary>
    /// Gets the provider's logout endpoint URL (if supported)
    /// </summary>
    public string? GetLogoutEndpoint()
    {
        try
        {
            Console.WriteLine($"[MauiDynamicConfigurationProvider] 🔍 Getting Cognito domain - SelectedAuthConfig: {_oidcConfig.SelectedAuthConfig}");
            Console.WriteLine($"[MauiDynamicConfigurationProvider] 📋 Available AuthConfigs: {string.Join(", ", _oidcConfig.AuthConfigs.Keys)}");
            
            if (_oidcConfig.AuthConfigs.TryGetValue(_oidcConfig.SelectedAuthConfig, out var authConfig))
            {
                Console.WriteLine($"[MauiDynamicConfigurationProvider] ✅ Found authConfig for {_oidcConfig.SelectedAuthConfig}");
                
                // Debug: Show all available fields in authConfig
                var allFields = new List<string>();
                foreach (var property in authConfig.Properties())
                {
                    allFields.Add($"{property.Name}={property.Value}");
                }
                Console.WriteLine($"[MauiDynamicConfigurationProvider] 📝 All fields in authConfig: {string.Join(", ", allFields)}");
                
                // Try to get cognitoDomain directly from config
                var cognitoDomain = authConfig["cognitoDomain"]?.ToString();
                Console.WriteLine($"[MauiDynamicConfigurationProvider] 🔍 Direct cognitoDomain field: '{cognitoDomain}'");
                
                if (!string.IsNullOrEmpty(cognitoDomain))
                {
                    var logoutEndpoint = $"{cognitoDomain.TrimEnd('/')}/logout";
                    Console.WriteLine($"[MauiDynamicConfigurationProvider] ✅ Using direct cognitoDomain logout endpoint: {logoutEndpoint}");
                    return logoutEndpoint;
                }

                // Fallback: construct from AWS region and domain if available
                var awsRegion = authConfig["awsRegion"]?.ToString();
                var domainPrefix = authConfig["cognitoDomainPrefix"]?.ToString() ?? "magicpets";
                
                Console.WriteLine($"[MauiDynamicConfigurationProvider] 🔍 Fallback fields - awsRegion: '{awsRegion}', cognitoDomainPrefix: '{domainPrefix}'");
                
                if (!string.IsNullOrEmpty(awsRegion) && !string.IsNullOrEmpty(domainPrefix))
                {
                    var constructedLogoutEndpoint = $"https://{domainPrefix}.auth.{awsRegion}.amazoncognito.com/logout";
                    Console.WriteLine($"[MauiDynamicConfigurationProvider] ✅ Using constructed logout endpoint: {constructedLogoutEndpoint}");
                    return constructedLogoutEndpoint;
                }
                
                Console.WriteLine($"[MauiDynamicConfigurationProvider] ❌ No valid domain fields found");
            }
            else
            {
                Console.WriteLine($"[MauiDynamicConfigurationProvider] ❌ Could not find authConfig for '{_oidcConfig.SelectedAuthConfig}'");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MauiDynamicConfigurationProvider] Error getting Cognito domain: {ex.Message}");
        }

        Console.WriteLine($"[MauiDynamicConfigurationProvider] ❌ Returning null for Cognito domain");
        return null;
    }

    /// <summary>
    /// Gets the Cognito Client ID from the dynamic configuration
    /// </summary>
    public string? GetClientId()
    {
        try
        {
            if (_oidcConfig.AuthConfigs.TryGetValue(_oidcConfig.SelectedAuthConfig, out var authConfig))
            {
                // Try userPoolClientId first (AWS Cognito format)
                var clientId = authConfig["userPoolClientId"]?.ToString();
                if (!string.IsNullOrEmpty(clientId))
                {
                    return clientId;
                }

                // Fallback to generic clientId
                return authConfig["clientId"]?.ToString();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MauiDynamicConfigurationProvider] Error getting Client ID: {ex.Message}");
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
            Console.WriteLine($"[MauiDynamicConfigurationProvider] Error getting Authority: {ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// Builds the complete logout URL for the current provider
    /// </summary>
    public string? BuildLogoutUrl(string postLogoutRedirectUri)
    {
        var providerType = GetProviderType();
        var logoutEndpoint = GetLogoutEndpoint();
        var clientId = GetClientId();
        
        if (string.IsNullOrEmpty(logoutEndpoint) || string.IsNullOrEmpty(clientId))
        {
            Console.WriteLine($"[MauiDynamicConfigurationProvider] ❌ Missing logout endpoint or client ID");
            return null;
        }
        
        return providerType?.ToLower() switch
        {
            "cognito" => $"{logoutEndpoint}?client_id={clientId}&logout_uri={Uri.EscapeDataString(postLogoutRedirectUri)}",
            "auth0" => $"{logoutEndpoint.TrimEnd('/')}/v2/logout?client_id={clientId}&returnTo={Uri.EscapeDataString(postLogoutRedirectUri)}",
            "okta" => $"{logoutEndpoint}?id_token_hint={{id_token}}&post_logout_redirect_uri={Uri.EscapeDataString(postLogoutRedirectUri)}",
            "azuread" => $"{logoutEndpoint}?post_logout_redirect_uri={Uri.EscapeDataString(postLogoutRedirectUri)}",
            _ => $"{logoutEndpoint}?post_logout_redirect_uri={Uri.EscapeDataString(postLogoutRedirectUri)}" // Generic OIDC
        };
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
                
                // Check for Cognito-specific fields
                if (authConfig["cognitoDomain"] != null || authConfig["userPoolClientId"] != null)
                {
                    return "cognito";
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MauiDynamicConfigurationProvider] Error getting provider type: {ex.Message}");
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