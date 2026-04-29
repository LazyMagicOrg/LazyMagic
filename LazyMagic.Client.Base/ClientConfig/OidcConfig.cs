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
    /// Per-host, per-path resolution of which auth pool gates which app
    /// after the system→tenant→subtenant cascade in the deploy plugin
    /// (e.g. BCPlugin). Each entry: <c>{ Path, Name, AuthConfig }</c> where
    /// <c>AuthConfig</c> is the pool name for gated apps; <c>null</c> or
    /// empty marks an explicit-public app.
    /// <para>
    /// Used by the WASM bootstrap to decide whether to wire up an OIDC
    /// authority for the current path at all — see
    /// <see cref="TryResolveAuthConfigForPath"/>. login.html consumes the
    /// same data to pick which pool to authenticate against on the
    /// /authentication/login interstitial.
    /// </para>
    /// </summary>
    public List<AppEntry> Apps { get; set; } = new();

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

    /// <summary>
    /// Tri-state lookup of the auth pool for a given request pathname.
    /// Returns the resolution kind via <paramref name="kind"/>; when
    /// <see cref="AppAuthResolutionKind.Gated"/>, <paramref name="poolName"/>
    /// holds the pool name (matching a key in <see cref="AuthConfigs"/>).
    /// <para>
    /// Match rule: longest-prefix on <see cref="AppEntry.Path"/>, mirroring
    /// the same rule CFRequest.js uses at the edge. Tied lengths take the
    /// first declared (configs typically declare entries with distinct
    /// path prefixes, so ties are rare).
    /// </para>
    /// <para>
    /// This is the canonical "should I OIDC-init?" check at WASM bootstrap.
    /// <see cref="AppAuthResolutionKind.Public"/> means "an entry matched
    /// AND the cascade resolved AuthConfig to empty/null" — i.e. an
    /// explicit-public verdict, NOT a missing entry. Bootstrap should
    /// short-circuit OIDC entirely in that case.
    /// </para>
    /// </summary>
    public AppAuthResolutionKind TryResolveAuthConfigForPath(string pathname, out string? poolName)
    {
        poolName = null;
        if (Apps.Count == 0 || string.IsNullOrEmpty(pathname)) return AppAuthResolutionKind.NoMatch;

        AppEntry? best = null;
        foreach (var entry in Apps)
        {
            if (string.IsNullOrEmpty(entry.Path)) continue;
            if (!pathname.StartsWith(entry.Path, StringComparison.Ordinal)) continue;
            if (best == null || entry.Path.Length > best.Path.Length) best = entry;
        }

        if (best == null) return AppAuthResolutionKind.NoMatch;
        if (string.IsNullOrEmpty(best.AuthConfig)) return AppAuthResolutionKind.Public;

        poolName = best.AuthConfig;
        return AppAuthResolutionKind.Gated;
    }
}

/// <summary>
/// Per-app routing entry from <c>/config</c>'s <c>apps[]</c>. Emitted by
/// the deploy plugin (e.g. BCPlugin) after resolving the
/// system→tenant→subtenant <c>WebApps</c> cascade.
/// </summary>
public sealed class AppEntry
{
    /// <summary>Path prefix the app serves at (e.g. <c>"/"</c>, <c>"/admin/"</c>).</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>App name (used for bucket-naming downstream; not consumed by the WASM bootstrap).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Resolved auth pool name for this path. <c>null</c> or empty means
    /// the explicit-public marker (<c>WebApps[Path].AuthConfig = ""</c>
    /// override at any cascade level). Non-empty values must match a key
    /// in <see cref="OidcConfig.AuthConfigs"/>.
    /// </summary>
    public string? AuthConfig { get; set; }
}

/// <summary>
/// Outcome of <see cref="OidcConfig.TryResolveAuthConfigForPath"/>.
/// Distinguishes "no matching entry at all" (caller may fall back to a
/// bundle-level default) from "explicit-public" (caller MUST NOT init
/// OIDC for this path).
/// </summary>
public enum AppAuthResolutionKind
{
    /// <summary>No <see cref="AppEntry"/> matched the requested path.</summary>
    NoMatch,
    /// <summary>An entry matched and its <c>AuthConfig</c> is null/empty (explicit public).</summary>
    Public,
    /// <summary>An entry matched and named a non-empty pool. <c>poolName</c> out param is set.</summary>
    Gated,
}