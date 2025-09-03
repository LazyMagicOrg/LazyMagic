namespace LazyMagic.OIDC.MAUI;

/// <summary>
/// Interface for authentication pages that handle login flows
/// </summary>
public interface IAuthenticationPage
{
    /// <summary>
    /// Waits for the authentication callback to complete
    /// </summary>
    /// <returns>The callback URL received from authentication, or null if cancelled</returns>
    Task<string?> WaitForCallbackAsync();
}

/// <summary>
/// Interface for password operation pages that handle password change/reset flows
/// </summary>
public interface IPasswordPage
{
    /// <summary>
    /// Waits for the password operation callback to complete
    /// </summary>
    /// <returns>The callback URL received from password operation, or null if cancelled</returns>
    Task<string?> WaitForCallbackAsync();
}

/// <summary>
/// Interface for logout pages that handle logout flows
/// </summary>
public interface ILogoutPage
{
    /// <summary>
    /// Waits for the logout operation to complete
    /// </summary>
    /// <returns>True when logout is complete</returns>
    Task<bool> WaitForLogoutAsync();
}