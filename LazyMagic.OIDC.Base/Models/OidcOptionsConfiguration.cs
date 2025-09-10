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
    public List<string> DefaultScopes { get; set; } = new();
    public string? NameClaim { get; set; } = "name";
    public string? RoleClaim { get; set; } = "cognito:groups";

    /// <summary>
    /// Creates configuration from a JObject auth config
    /// </summary>
    public static OidcOptionsConfiguration FromAuthConfig(
        JObject authConfig,
        string baseAddress)
    {
        var options = new OidcOptionsConfiguration();

        // New config format with explicit URLs
        var hostedUIDomain = authConfig["HostedUIDomain"]?.ToString();
        var metadataUrl = authConfig["MetadataUrl"]?.ToString();
        var issuerUrl = authConfig["IssuerUrl"]?.ToString();
        var configAuthorityDomain = authConfig["AuthorityDomain"]?.ToString();
        
        // ClientId might be under different field names
        var clientId = authConfig["ClientId"]?.ToString() 
                    ?? authConfig["clientId"]?.ToString() 
                    ?? authConfig["userPoolClientId"]?.ToString();
        
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
                // Generic OIDC provider configuration
                options.Authority = authConfig["authority"]?.ToString();
                options.ClientId = authConfig["clientId"]?.ToString() ?? authConfig["ClientId"]?.ToString();
                options.ResponseType = authConfig["responseType"]?.ToString() ?? "code";
                options.MetadataUrl = authConfig["metadataUrl"]?.ToString() ?? authConfig["MetadataUrl"]?.ToString();
            }
        }
        if(!baseAddress.EndsWith('/'))
            baseAddress = baseAddress + '/';    
        options.RedirectUri = $"{baseAddress}authentication/login-callback";
        options.PostLogoutRedirectUri = baseAddress;
        
        options.DefaultScopes.Clear();
        options.DefaultScopes.Add("openid");
        options.DefaultScopes.Add("profile");
        options.DefaultScopes.Add("email");

        return options;
    }
}