using Newtonsoft.Json.Linq;

namespace LazyMagic.Client.Base;

/// <summary>
/// OIDC configuration supporting multiple auth providers
/// Uses JObject for flexibility to support Cognito, Auth0, Okta, Azure AD, etc.
/// </summary>
public class OidcConfig : IOidcConfig
{
    /// <summary>
    /// Dictionary of auth configurations by name
    /// Each JObject contains provider-specific configuration
    /// </summary>
    public Dictionary<string, JObject> AuthConfigs { get; set; } = new();
    
    /// <summary>
    /// Gets or sets the currently selected auth configuration name
    /// </summary>
    public string SelectedAuthConfig { get; set; } = "ConsumerAuth";
    
    /// <summary>
    /// Gets the current auth configuration JObject
    /// </summary>
    public JObject? GetCurrentAuthConfig()
    {
        return AuthConfigs.TryGetValue(SelectedAuthConfig, out var config) ? config : null;
    }
    
    /// <summary>
    /// Checks if configuration is loaded
    /// </summary>
    public bool IsConfigured => AuthConfigs.Count > 0;
}