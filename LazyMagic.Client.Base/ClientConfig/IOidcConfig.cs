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

    /// <summary>
    /// Per-host, per-path resolution of which auth pool gates which app.
    /// Empty when configuration hasn't loaded yet OR when the deploy
    /// plugin doesn't emit <c>apps[]</c>. Lazy implementations may return
    /// an empty list pre-load; check <see cref="IsConfigured"/> first if
    /// you need to distinguish "loading" from "no apps."
    /// </summary>
    List<AppEntry> Apps { get; }

    /// <summary>
    /// Tri-state lookup of the auth pool for a given pathname. See
    /// <see cref="OidcConfig.TryResolveAuthConfigForPath"/> for the
    /// match rule and the contract of the three outcomes. The WASM
    /// bootstrap calls this before configuring OIDC so explicit-public
    /// apps never wire up an authority (and thus never fire silent
    /// renewal probes against Cognito).
    /// </summary>
    AppAuthResolutionKind TryResolveAuthConfigForPath(string pathname, out string? poolName);
}