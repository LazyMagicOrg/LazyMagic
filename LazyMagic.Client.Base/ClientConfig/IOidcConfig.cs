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
    /// Dictionary of Events API configurations by resource name
    /// Each JObject contains: authConfig (string) and wsUrl (string)
    /// </summary>
    Dictionary<string, JObject> EventsApis { get; }

    /// <summary>
    /// Gets the currently selected auth configuration name
    /// </summary>
    string SelectedAuthConfig { get; set; }

    /// <summary>
    /// Gets the current auth configuration JObject
    /// </summary>
    JObject? GetCurrentAuthConfig();

    /// <summary>
    /// Gets the Events API URL for the current auth configuration
    /// Returns null if no matching Events API is found
    /// </summary>
    string? GetCurrentEventsApiUrl();

    /// <summary>
    /// Checks if configuration is loaded
    /// </summary>
    bool IsConfigured { get; }
}