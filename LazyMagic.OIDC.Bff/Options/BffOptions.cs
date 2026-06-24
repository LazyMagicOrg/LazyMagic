using Microsoft.Extensions.Configuration;

namespace LazyMagic.OIDC.Bff;

/// <summary>
/// Strongly-typed configuration for the LazyMagic BFF. Bound from environment
/// variables (prefix <c>LZ_BFF_</c>) and/or <see cref="IConfiguration"/>.
/// Per MultiTenantAuth.md §8.3, §8.4, §8.12, §8.14.
/// </summary>
public sealed class BffOptions
{
    /// <summary>Master switch. The whole library is inert unless this is true (LZ_BFF_ENABLED).</summary>
    public bool Enabled { get; set; }

    /// <summary>Which IdP flavor: cognito | keycloak (LZ_BFF_PROVIDER).</summary>
    public BffProvider Provider { get; set; } = BffProvider.Cognito;

    /// <summary>OIDC authority / issuer base (LZ_BFF_AUTHORITY).</summary>
    public string Authority { get; set; } = string.Empty;

    /// <summary>
    /// OIDC discovery metadata URL (LZ_BFF_METADATA_URL).
    /// Defaults to <c>{Authority}/.well-known/openid-configuration</c>.
    /// </summary>
    public string? MetadataUrl { get; set; }

    /// <summary>Confidential client id (LZ_BFF_CLIENT_ID).</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>Confidential client secret (LZ_BFF_CLIENT_SECRET). Prefer Secrets Manager in production.</summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// Space-delimited scopes (LZ_BFF_SCOPES). When unset, defaults to
    /// <c>openid profile email</c> (+ <c>offline_access</c> for Keycloak to get a refresh token).
    /// </summary>
    public string? Scopes { get; set; }

    /// <summary>The redirect_uri path registered with the IdP (LZ_BFF_CALLBACK_PATH). Default <c>/bff/callback</c>.</summary>
    public string CallbackPath { get; set; } = "/bff/callback";

    /// <summary>Session cookie name (LZ_BFF_COOKIE_NAME). Default <c>__bff</c>.</summary>
    public string CookieName { get; set; } = "__bff";

    /// <summary>
    /// Cookie Domain attribute (LZ_BFF_COOKIE_DOMAIN), e.g. <c>.lazymagicdev.click</c>.
    /// Required for cross-subtenant SSO (§8.13). When null, no Domain attribute is set.
    /// </summary>
    public string? CookieDomain { get; set; }

    /// <summary>
    /// Seconds of remaining access-token lifetime below which the cookie-&gt;Bearer
    /// middleware proactively refreshes (LZ_BFF_ACCESS_TOKEN_SKEW_SECONDS). Default 60.
    /// </summary>
    public int AccessTokenSkewSeconds { get; set; } = 60;

    /// <summary>DynamoDB table holding session/refresh rows (LZ_BFF_SESSION_TABLE).</summary>
    public string SessionTableName { get; set; } = string.Empty;

    /// <summary>
    /// Absolute session TTL in hours (LZ_BFF_SESSION_TTL_HOURS). Per-pool:
    /// employees ~12h, consumers ~720h (30d). Default 12. (§8.14)
    /// </summary>
    public int SessionTtlHours { get; set; } = 12;

    // --- Data Protection key ring (§8.4) ---

    /// <summary>SSM Parameter Store path for the Data Protection key ring (LZ_BFF_DP_PARAM).</summary>
    public string? DataProtectionSsmParam { get; set; }

    /// <summary>Data Protection application name — must match across all tasks/Lambda envs (LZ_BFF_DP_APPNAME).</summary>
    public string? DataProtectionAppName { get; set; }

    /// <summary>AWS region for the DynamoDB and SSM clients (LZ_BFF_AWS_REGION). Falls back to the ambient SDK region.</summary>
    public string? AwsRegion { get; set; }

    /// <summary>
    /// The <c>lz-authname</c> value stamped on attached Bearer requests so the host's
    /// multi-scheme JWT middleware selects the correct Cognito pool scheme (LZ_BFF_AUTHNAME).
    /// When unset, falls back to the lower-cased provider name.
    /// </summary>
    public string? AuthName { get; set; }

    /// <summary>Effective metadata URL, computed from <see cref="MetadataUrl"/> or <see cref="Authority"/>.</summary>
    public string ResolvedMetadataUrl =>
        !string.IsNullOrWhiteSpace(MetadataUrl)
            ? MetadataUrl!
            : $"{Authority.TrimEnd('/')}/.well-known/openid-configuration";

    /// <summary>Effective scope string, applying provider-aware defaults when unset.</summary>
    public string ResolvedScopes
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Scopes))
                return Scopes!;
            // Keycloak needs offline_access for a refresh token; Cognito issues one for code+PKCE without it.
            return Provider == BffProvider.Keycloak
                ? "openid profile email offline_access"
                : "openid profile email";
        }
    }

    /// <summary>
    /// Bind values from environment variables (LZ_BFF_*) over an optional IConfiguration
    /// section ("LzBff"). Environment variables take precedence so deploy tooling can
    /// override config without rebuilding.
    /// </summary>
    public static BffOptions FromConfiguration(IConfiguration configuration)
    {
        var o = new BffOptions();

        // 1) IConfiguration section (lowest precedence). Tolerant of absence.
        configuration.GetSection("LzBff").Bind(o);

        // 2) Environment variables (highest precedence).
        o.Enabled = ReadBool("LZ_BFF_ENABLED", o.Enabled);
        o.Provider = ReadProvider("LZ_BFF_PROVIDER", o.Provider);
        o.Authority = ReadString("LZ_BFF_AUTHORITY", o.Authority);
        o.MetadataUrl = ReadStringOrNull("LZ_BFF_METADATA_URL", o.MetadataUrl);
        o.ClientId = ReadString("LZ_BFF_CLIENT_ID", o.ClientId);
        o.ClientSecret = ReadString("LZ_BFF_CLIENT_SECRET", o.ClientSecret);
        o.Scopes = ReadStringOrNull("LZ_BFF_SCOPES", o.Scopes);
        o.CallbackPath = ReadString("LZ_BFF_CALLBACK_PATH", o.CallbackPath);
        o.CookieName = ReadString("LZ_BFF_COOKIE_NAME", o.CookieName);
        o.CookieDomain = ReadStringOrNull("LZ_BFF_COOKIE_DOMAIN", o.CookieDomain);
        o.AccessTokenSkewSeconds = ReadInt("LZ_BFF_ACCESS_TOKEN_SKEW_SECONDS", o.AccessTokenSkewSeconds);
        o.SessionTableName = ReadString("LZ_BFF_SESSION_TABLE", o.SessionTableName);
        o.SessionTtlHours = ReadInt("LZ_BFF_SESSION_TTL_HOURS", o.SessionTtlHours);
        o.DataProtectionSsmParam = ReadStringOrNull("LZ_BFF_DP_PARAM", o.DataProtectionSsmParam);
        o.DataProtectionAppName = ReadStringOrNull("LZ_BFF_DP_APPNAME", o.DataProtectionAppName);
        o.AwsRegion = ReadStringOrNull("LZ_BFF_AWS_REGION", o.AwsRegion);
        o.AuthName = ReadStringOrNull("LZ_BFF_AUTHNAME", o.AuthName);

        return o;
    }

    /// <summary>True if LZ_BFF_ENABLED resolves to true via env var OR IConfiguration. Used by the hosting startup.</summary>
    public static bool IsEnabled(IConfiguration configuration)
    {
        var env = Environment.GetEnvironmentVariable("LZ_BFF_ENABLED");
        if (!string.IsNullOrWhiteSpace(env))
            return env.Trim().Equals("true", StringComparison.OrdinalIgnoreCase) || env.Trim() == "1";
        return configuration.GetValue<bool>("LzBff:Enabled");
    }

    private static string ReadString(string key, string fallback)
    {
        var v = Environment.GetEnvironmentVariable(key);
        return string.IsNullOrWhiteSpace(v) ? fallback : v;
    }

    private static string? ReadStringOrNull(string key, string? fallback)
    {
        var v = Environment.GetEnvironmentVariable(key);
        return string.IsNullOrWhiteSpace(v) ? fallback : v;
    }

    private static bool ReadBool(string key, bool fallback)
    {
        var v = Environment.GetEnvironmentVariable(key);
        if (string.IsNullOrWhiteSpace(v)) return fallback;
        return v.Trim().Equals("true", StringComparison.OrdinalIgnoreCase) || v.Trim() == "1";
    }

    private static int ReadInt(string key, int fallback)
    {
        var v = Environment.GetEnvironmentVariable(key);
        return int.TryParse(v, out var n) ? n : fallback;
    }

    private static BffProvider ReadProvider(string key, BffProvider fallback)
    {
        var v = Environment.GetEnvironmentVariable(key);
        if (string.IsNullOrWhiteSpace(v)) return fallback;
        return v.Trim().ToLowerInvariant() switch
        {
            "keycloak" => BffProvider.Keycloak,
            "cognito" => BffProvider.Cognito,
            _ => fallback,
        };
    }
}
