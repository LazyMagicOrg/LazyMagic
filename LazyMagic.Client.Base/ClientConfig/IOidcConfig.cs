using Newtonsoft.Json.Linq;

namespace LazyMagic.Client.Base;

/// <summary>
/// Interface for OIDC configuration service
/// Provides access to dynamically loaded OIDC configuration
/// </summary>
public interface IOidcConfig
{
    /// <summary>
    /// Dictionary of auth configurations by name
    /// Each JObject contains provider-specific configuration
    /// </summary>
    Dictionary<string, JObject> AuthConfigs { get; }
    
    /// <summary>
    /// Gets the currently selected auth configuration name
    /// </summary>
    string SelectedAuthConfig { get; set; }
    
    /// <summary>
    /// Gets the current auth configuration JObject
    /// </summary>
    JObject? GetCurrentAuthConfig();
    
    /// <summary>
    /// Checks if configuration is loaded
    /// </summary>
    bool IsConfigured { get; }
}