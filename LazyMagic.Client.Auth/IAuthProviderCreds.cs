namespace LazyMagic.Client.Auth;
/// <summary>
/// Interface for authentication providers that can provide and manage credentials (tokens, etc.)
/// for accessing external services. This interface contains the core credential 
/// management methods that are needed for both traditional and external auth flows.
/// </summary>
public interface IAuthProviderCreds
{
    // Properties
    /// <summary>
    /// Security level for API calls: 0=insecure, 1=JWT token, 2=AWS SigV4
    /// </summary>
    int SecurityLevel { get; set; }

    /// <summary>
    /// A unique identifier for the current session, used to track user sessions
    /// </summary>
    string SessionId { get; set; }

    // Get methods
    /// <summary>
    /// Gets the AWS credentials for API signing (if applicable)
    /// </summary>
    /// <returns>AWS credentials or null if not available/applicable</returns>
    Task<Creds?> GetCredsAsync();

    /// <summary>
    /// Gets the identity token (typically a JWT containing user claims)
    /// </summary>
    /// <returns>Identity token or null if not available</returns>
    Task<string?> GetIdentityToken();

    /// <summary>
    /// Gets the access token for API authorization
    /// </summary>
    /// <returns>Access token or null if not available</returns>
    Task<string?> GetAccessToken();

    /// <summary>
    /// Gets the refresh token used to obtain new access/identity tokens
    /// </summary>
    /// <returns>Refresh token or null if not available</returns>
    Task<string?> GetRefreshToken();

    // Set methods
    /// <summary>
    /// Sets the AWS credentials for API signing
    /// </summary>
    /// <param name="creds">AWS credentials to set</param>
    Task SetCredsAsync(Creds? creds);

    /// <summary>
    /// Sets the identity token (typically a JWT containing user claims)
    /// </summary>
    /// <param name="token">Identity token to set</param>
    Task SetIdentityToken(string? token);

    /// <summary>
    /// Sets the access token for API authorization
    /// </summary>
    /// <param name="token">Access token to set</param>
    Task SetAccessToken(string? token);

    /// <summary>
    /// Sets the refresh token used to obtain new access/identity tokens
    /// </summary>
    /// <param name="token">Refresh token to set</param>
    Task SetRefreshToken(string? token);

    
}