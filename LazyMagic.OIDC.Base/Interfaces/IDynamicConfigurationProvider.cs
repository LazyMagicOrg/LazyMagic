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
    /// Gets the metadata URL (.well-known/openid-configuration) from the dynamic configuration
    /// </summary>
    string? GetMetadataUrl();
    
    /// <summary>
    /// Gets the provider's logout endpoint URL (if supported).
    /// This method first attempts to use the cached end_session_endpoint from the discovery document.
    /// Falls back to constructing the URL from configuration if discovery document is not available.
    /// </summary>
    string? GetLogoutEndpoint();
    
    /// <summary>
    /// Gets the end_session_endpoint from the OpenID discovery document asynchronously.
    /// This fetches and parses the .well-known/openid-configuration if not already cached.
    /// </summary>
    /// <returns>The end_session_endpoint URL or null if not available</returns>
    Task<string?> GetEndSessionEndpointAsync();
    
    /// <summary>
    /// Ensures the discovery document is loaded and cached.
    /// Call this during initialization to pre-fetch the discovery document.
    /// </summary>
    Task InitializeDiscoveryAsync();
    
    /// <summary>
    /// Builds the complete logout URL for the current provider
    /// Returns null if logout URLs are not supported by this provider
    /// </summary>
    string? BuildLogoutUrl(string postLogoutRedirectUri, string? idTokenHint = null);
    
    /// <summary>
    /// Builds the complete logout URL for the current provider asynchronously.
    /// This ensures the end_session_endpoint is fetched from the discovery document.
    /// </summary>
    /// <param name="postLogoutRedirectUri">The URI to redirect to after logout</param>
    /// <param name="idTokenHint">The ID token hint for providers that require it (e.g., Keycloak)</param>
    Task<string?> BuildLogoutUrlAsync(string postLogoutRedirectUri, string? idTokenHint = null);
    
    /// <summary>
    /// Gets a configuration value by key
    /// </summary>
    string? GetValue(string key);
    
    /// <summary>
    /// Gets the provider type (e.g., "cognito", "auth0", "okta", "azuread", "keycloak")
    /// </summary>
    string? GetProviderType();

    /// <summary>
    /// Determines if the application should manage remember-me token storage.
    /// Returns false for providers with native remember-me support (e.g., Keycloak),
    /// where the provider handles session persistence.
    /// Returns true for providers without native remember-me (e.g., Cognito),
    /// where the application must store tokens locally.
    /// Can be overridden via "rememberMeEnabled" in config.
    /// </summary>
    bool RequiresClientSideTokenStorage();

    /// <summary>
    /// Determines if the provider has native remember-me support.
    /// Providers like Keycloak have built-in "Remember Me" in their login UI.
    /// </summary>
    bool HasNativeRememberMe();

    /// <summary>
    /// Determines if post-logout redirect is enabled.
    /// When true (default), logout will redirect back to the application.
    /// When false, logout will end at the provider's logout confirmation page.
    /// Set "postLogoutRedirectEnabled": false in config to disable.
    /// Useful when post_logout_redirect_uri is not registered with the provider.
    /// </summary>
    bool IsPostLogoutRedirectEnabled();
}