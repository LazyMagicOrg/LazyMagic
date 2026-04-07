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
    /// Dictionary of Events API configurations by resource name
    /// Each JObject contains: authConfig (string) and wsUrl (string)
    /// Example: { "tenantEvents": { "authConfig": "tenantauth", "wsUrl": "https://..." } }
    /// </summary>
    public Dictionary<string, JObject> EventsApis { get; set; } = new();

    /// <summary>
    /// Gets or sets the currently selected auth configuration name
    /// </summary>
    public string SelectedAuthConfig { get; set;  } = "ConsumerAuth";
    
    /// <summary>
    /// Gets the current auth configuration JObject
    /// </summary>
    public JObject? GetCurrentAuthConfig()
    {
        return AuthConfigs.TryGetValue(SelectedAuthConfig, out var config) ? config : null;
    }

    /// <summary>
    /// Gets the Events API URL for the current auth configuration
    /// Returns null if no matching Events API is found
    /// </summary>
    public string? GetCurrentEventsApiUrl()
    {
        // Find Events API entry that matches the current auth configuration
        foreach (var eventsApi in EventsApis.Values)
        {
            var authConfig = eventsApi["authConfig"]?.ToString();
            if (string.Equals(authConfig, SelectedAuthConfig, StringComparison.OrdinalIgnoreCase))
            {
                return eventsApi["wsUrl"]?.ToString();
            }
        }
        return null;
    }

    /// <summary>
    /// Checks if configuration is loaded
    /// </summary>
    public bool IsConfigured => AuthConfigs.Count > 0;
}