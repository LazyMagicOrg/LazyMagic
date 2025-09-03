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

        // Check if it's AWS Cognito configuration
        var userPoolId = authConfig["userPoolId"]?.ToString();
        var userPoolClientId = authConfig["userPoolClientId"]?.ToString();
        var awsRegion = authConfig["awsRegion"]?.ToString();
        var cognitoDomainPrefix = authConfig["cognitoDomainPrefix"]?.ToString() ?? authConfig["domainPrefix"]?.ToString() ?? "magicpets";
        var cognitoDomain = authConfig["cognitoDomain"]?.ToString();

        if (!string.IsNullOrEmpty(userPoolId) && 
            !string.IsNullOrEmpty(userPoolClientId) && 
            !string.IsNullOrEmpty(awsRegion))
        {
            // Configure for AWS Cognito
            // For OAuth flow, we need to use the Cognito hosted UI domain as Authority
            string authorityDomain;
            
            if (!string.IsNullOrEmpty(cognitoDomain))
            {
                // Use explicit cognito domain if provided
                authorityDomain = cognitoDomain.TrimEnd('/');
            }
            else if (!string.IsNullOrEmpty(cognitoDomainPrefix))
            {
                // Construct from domain prefix
                authorityDomain = $"https://{cognitoDomainPrefix}.auth.{awsRegion}.amazoncognito.com";
            }
            else
            {
                // Fallback to issuer (this won't work for OAuth flow but allows token validation)
                authorityDomain = $"https://cognito-idp.{awsRegion}.amazonaws.com/{userPoolId}";
                Console.WriteLine("[OidcOptionsConfiguration] WARNING: No Cognito domain configured, OAuth flow may not work");
            }
            
            options.Authority = authorityDomain;
            options.ClientId = userPoolClientId;
            // Metadata URL should always point to the issuer for token validation
            options.MetadataUrl = $"https://cognito-idp.{awsRegion}.amazonaws.com/{userPoolId}/.well-known/openid-configuration";
        }
        else
        {
            // Generic OIDC provider configuration
            options.Authority = authConfig["authority"]?.ToString();
            options.ClientId = authConfig["clientId"]?.ToString();
            options.ResponseType = authConfig["responseType"]?.ToString() ?? "code";
            options.MetadataUrl = authConfig["metadataUrl"]?.ToString();
        }
        if(!baseAddress.EndsWith('/'))
            baseAddress = baseAddress + '/';    
        options.RedirectUri = $"{baseAddress}AuthPage/login-callback";
        options.PostLogoutRedirectUri = baseAddress;
        
        options.DefaultScopes.Clear();
        options.DefaultScopes.Add("openid");
        options.DefaultScopes.Add("profile");
        options.DefaultScopes.Add("email");

        return options;
    }
}