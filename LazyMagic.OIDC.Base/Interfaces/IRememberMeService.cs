namespace LazyMagic.OIDC.Base;

/// <summary>
/// Interface for managing Remember Me functionality and token persistence
/// </summary>
public interface IRememberMeService
{
    /// <summary>
    /// Gets the current Remember Me setting
    /// </summary>
    Task<bool> GetRememberMeAsync();

    /// <summary>
    /// Sets the Remember Me setting and manages token storage accordingly
    /// </summary>
    Task SetRememberMeAsync(bool rememberMe);

    /// <summary>
    /// Clears all authentication tokens
    /// </summary>
    Task ClearTokensAsync();

    /// <summary>
    /// Checks if there are any OIDC tokens in storage
    /// </summary>
    Task<bool> HasTokensAsync();

    /// <summary>
    /// Initializes authentication on app startup based on RememberMe setting
    /// Ensures tokens are available to Blazor while maintaining persistence
    /// </summary>
    Task InitializeAuthenticationAsync();
}