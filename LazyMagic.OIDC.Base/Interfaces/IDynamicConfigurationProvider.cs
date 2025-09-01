namespace LazyMagic.OIDC.Base;

/// <summary>
/// Platform-agnostic interface for accessing dynamic OIDC configuration
/// Supports any OIDC provider (Cognito, Auth0, Okta, Azure AD, etc.)
/// </summary>
public interface IDynamicConfigurationProvider
{
    /// <summary>
    /// Gets the OIDC Authority URL (issuer) from the dynamic configuration
    /// </summary>
    string? GetAuthority();
    
    /// <summary>
    /// Gets the OIDC Client ID from the dynamic configuration
    /// </summary>
    string? GetClientId();
    
    /// <summary>
    /// Gets the provider's logout endpoint URL (if supported)
    /// Returns null if the provider doesn't support logout endpoints
    /// </summary>
    string? GetLogoutEndpoint();
    
    /// <summary>
    /// Builds the complete logout URL for the current provider
    /// Returns null if logout URLs are not supported by this provider
    /// </summary>
    string? BuildLogoutUrl(string postLogoutRedirectUri);
    
    /// <summary>
    /// Gets a configuration value by key
    /// </summary>
    string? GetValue(string key);
    
    /// <summary>
    /// Gets the provider type (e.g., "cognito", "auth0", "okta", "azuread")
    /// </summary>
    string? GetProviderType();
}