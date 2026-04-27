namespace LazyMagic.OIDC.Base;

/// <summary>
/// Platform-agnostic OIDC configuration options
/// </summary>
public class OidcOptionsConfiguration
{
    public string? Authority { get; set; }
    public string? ClientId { get; set; }
    public string? ResponseType { get; set; } = "code";
    public string? MetadataUrl { get; set; }
    public string? RedirectUri { get; set; }
    public string? PostLogoutRedirectUri { get; set; }
    /// <summary>
    /// The end_session_endpoint from the OpenID discovery document.
    /// This is populated by fetching the .well-known/openid-configuration.
    /// </summary>
    public string? EndSessionEndpoint { get; set; }
    public List<string> DefaultScopes { get; set; } = new();
    public string? NameClaim { get; set; } = "name";
    public string? RoleClaim { get; set; } = "cognito:groups";

    /// <summary>
    /// Creates configuration from a JObject auth config
    /// </summary>
    /// <param name="authConfig">The auth configuration from the server</param>
    /// <param name="baseAddress">The base address for redirect URIs</param>
    /// <param name="clientIdOverride">Optional client-specified ClientId that overrides the server value</param>
    public static OidcOptionsConfiguration FromAuthConfig(
        JObject authConfig,
        string baseAddress,
        string? clientIdOverride = null)
    {
        var options = new OidcOptionsConfiguration();

        Console.WriteLine($"[OidcOptionsConfiguration.FromAuthConfig] clientIdOverride parameter: '{clientIdOverride}' (null: {clientIdOverride == null})");

        // New config format with explicit URLs
        var hostedUIDomain = authConfig["HostedUIDomain"]?.ToString();
        var metadataUrl = authConfig["MetadataUrl"]?.ToString();
        var issuerUrl = authConfig["IssuerUrl"]?.ToString();
        var configAuthorityDomain = authConfig["AuthorityDomain"]?.ToString();

        // Use client-specified clientId if provided, otherwise fall back to server config
        var serverClientId = authConfig["ClientId"]?.ToString()
                    ?? authConfig["clientId"]?.ToString()
                    ?? authConfig["userPoolClientId"]?.ToString();
        Console.WriteLine($"[OidcOptionsConfiguration.FromAuthConfig] Server config clientId: '{serverClientId}'");

        var clientId = !string.IsNullOrEmpty(clientIdOverride) ? clientIdOverride : serverClientId;
        Console.WriteLine($"[OidcOptionsConfiguration.FromAuthConfig] Final clientId to use: '{clientId}'");
        
        // Check if new format is present
        if (!string.IsNullOrEmpty(hostedUIDomain) && !string.IsNullOrEmpty(metadataUrl))
        {
            // Use new explicit format
            // For OAuth flows (login/logout), use the Hosted UI domain
            options.Authority = hostedUIDomain.TrimEnd('/');
            options.MetadataUrl = metadataUrl;
            options.ClientId = clientId;
            
            Console.WriteLine($"[OidcOptionsConfiguration] Using new config format - Authority: {options.Authority}, ClientId: {options.ClientId}");
        }
        else
        {
            // Fallback to old format for backward compatibility
            var userPoolId = authConfig["userPoolId"]?.ToString();
            var userPoolClientId = authConfig["userPoolClientId"]?.ToString();
            var awsRegion = authConfig["awsRegion"]?.ToString();
            var cognitoDomainPrefix = authConfig["cognitoDomainPrefix"]?.ToString() ?? authConfig["domainPrefix"]?.ToString();
            var cognitoDomain = authConfig["cognitoDomain"]?.ToString();

            if (!string.IsNullOrEmpty(userPoolId) && 
                !string.IsNullOrEmpty(userPoolClientId) && 
                !string.IsNullOrEmpty(awsRegion))
            {
                // Configure for AWS Cognito (old format)
                string authorityDomain;
                
                if (!string.IsNullOrEmpty(cognitoDomain))
                {
                    authorityDomain = cognitoDomain.TrimEnd('/');
                }
                else if (!string.IsNullOrEmpty(cognitoDomainPrefix) && !string.IsNullOrEmpty(awsRegion))
                {
                    authorityDomain = $"https://{cognitoDomainPrefix}.auth.{awsRegion}.amazoncognito.com";
                }
                else
                {
                    throw new InvalidOperationException($"[OidcOptionsConfiguration] Missing required configuration: cognitoDomain or (cognitoDomainPrefix + awsRegion). Cannot configure OAuth authority. Please use the new config format with explicit HostedUIDomain.");
                }
                
                options.Authority = authorityDomain;
                options.ClientId = userPoolClientId;
                options.MetadataUrl = $"https://cognito-idp.{awsRegion}.amazonaws.com/{userPoolId}/.well-known/openid-configuration";
                
                Console.WriteLine("[OidcOptionsConfiguration] Using old config format (backward compatibility)");
            }
            else
            {
                // Generic OIDC provider configuration. Read both casings —
                // server-side config emitters vary (CFAuthConfig.js uses
                // PascalCase to match the C# property names; older configs
                // use lowercase). Without this, Authority silently lands as
                // null and the WASM app's logout/silent-renew calls skip
                // through (BuildLogoutUrlAsync returns null).
                options.Authority = authConfig["authority"]?.ToString() ?? authConfig["Authority"]?.ToString();
                options.ClientId = authConfig["clientId"]?.ToString() ?? authConfig["ClientId"]?.ToString();
                options.ResponseType = authConfig["responseType"]?.ToString() ?? authConfig["ResponseType"]?.ToString() ?? "code";
                options.MetadataUrl = authConfig["metadataUrl"]?.ToString() ?? authConfig["MetadataUrl"]?.ToString();
            }
        }
        if(!baseAddress.EndsWith('/'))
            baseAddress = baseAddress + '/';

        // RedirectUri / PostLogoutRedirectUri precedence:
        //   1. authConfig.RedirectUri / PostLogoutRedirectUri (server-supplied) —
        //      lets the deployment route the OIDC callback through a domain other
        //      than the WASM app's origin (e.g., a tenant-apex callback that fans
        //      out to the originating subtenant via OAuth state). Required when
        //      the IdP only allows exact-match callback URLs (Cognito) and the
        //      app runs on multiple subdomains under one tenant.
        //   2. Fallback: baseAddress-derived (current behavior). Used when the
        //      IdP allows wildcards (Keycloak) or per-subdomain registration.
        var serverRedirectUri = authConfig["RedirectUri"]?.ToString()
                                ?? authConfig["redirectUri"]?.ToString();
        var serverPostLogoutRedirectUri = authConfig["PostLogoutRedirectUri"]?.ToString()
                                          ?? authConfig["postLogoutRedirectUri"]?.ToString();
        options.RedirectUri = !string.IsNullOrEmpty(serverRedirectUri)
            ? serverRedirectUri
            : $"{baseAddress}authentication/login-callback";
        options.PostLogoutRedirectUri = !string.IsNullOrEmpty(serverPostLogoutRedirectUri)
            ? serverPostLogoutRedirectUri
            : baseAddress;
        
        options.DefaultScopes.Clear();
        options.DefaultScopes.Add("openid");
        options.DefaultScopes.Add("profile");
        options.DefaultScopes.Add("email");

        return options;
    }
}