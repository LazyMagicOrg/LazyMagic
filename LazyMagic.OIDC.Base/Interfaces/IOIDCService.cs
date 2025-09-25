namespace LazyMagic.OIDC.Base;

/// <summary>
/// OIDC service interface for MVVM pattern
/// This abstraction allows LazyMagic.OIDC.Base to work with OIDC authentication
/// without depending on specific UI frameworks
/// </summary>
public interface IOIDCService
{
    /// <summary>
    /// Gets the current authentication state
    /// </summary>
    Task<OIDCAuthenticationState> GetAuthenticationStateAsync();

    /// <summary>
    /// Gets the current user as ClaimsPrincipal
    /// </summary>
    Task<ClaimsPrincipal?> GetCurrentUserAsync();

    /// <summary>
    /// Checks if the user is authenticated
    /// </summary>
    Task<bool> IsAuthenticatedAsync();

    /// <summary>
    /// Gets the access token for API calls
    /// </summary>
    Task<string?> GetAccessTokenAsync();

    /// <summary>
    /// Gets all user claims
    /// </summary>
    Task<IEnumerable<Claim>> GetUserClaimsAsync();

    /// <summary>
    /// Gets a specific claim value
    /// </summary>
    Task<string?> GetClaimValueAsync(string claimType);

    /// <summary>
    /// Checks if user is in a specific role
    /// </summary>
    Task<bool> IsInRoleAsync(string role);

    /// <summary>
    /// Initiates login process
    /// </summary>
    Task<bool> LoginAsync();

    /// <summary>
    /// Logs out the user
    /// </summary>
    Task LogoutAsync();

    /// <summary>
    /// Event raised when authentication state changes
    /// </summary>
    event EventHandler<OIDCAuthenticationStateChangedEventArgs>? AuthenticationStateChanged;

    /// <summary>
    /// Event raised when authentication is requested (optional for UI handling)
    /// </summary>
    event Action<string>? OnAuthenticationRequested;
}

/// <summary>
/// OIDC authentication state information
/// </summary>
public class OIDCAuthenticationState
{
    public bool IsAuthenticated { get; set; }
    public string? UserName { get; set; }
    public string? Email { get; set; }
    public DateTime? TokenExpiry { get; set; }
    public Dictionary<string, string>? Claims { get; set; }
}

/// <summary>
/// Event args for OIDC authentication state changes
/// </summary>
public class OIDCAuthenticationStateChangedEventArgs : EventArgs
{
    public OIDCAuthenticationState NewState { get; }

    public OIDCAuthenticationStateChangedEventArgs(OIDCAuthenticationState newState)
    {
        NewState = newState;
    }
}