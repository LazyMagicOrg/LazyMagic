using Microsoft.Extensions.Logging;

namespace LazyMagic.OIDC.MAUI;

/// <summary>
/// Interface for providing WebView-based authentication functionality
/// This allows the MauiServices library to delegate UI operations back to the main MAUI app
/// </summary>
public interface IWebViewAuthenticationProvider
{
    /// <summary>
    /// Shows a WebView for authentication and waits for the callback
    /// </summary>
    /// <param name="authUrl">The authentication URL to navigate to</param>
    /// <param name="callbackUrlScheme">The expected callback URL scheme</param>
    /// <returns>The callback URL received from the authentication flow, or null if cancelled</returns>
    Task<string?> AuthenticateAsync(string authUrl, string callbackUrlScheme);

    /// <summary>
    /// Clears all WebView session data including cookies, cache, and stored credentials
    /// This should be called during logout to ensure the user must re-authenticate
    /// </summary>
    Task ClearWebViewSessionAsync();

    /// <summary>
    /// Shows a WebView for password-related operations (change password, reset password) with appropriate UI
    /// </summary>
    /// <param name="passwordUrl">The password operation URL to navigate to</param>
    /// <param name="callbackUrlScheme">The expected callback URL scheme</param>
    /// <param name="operationType">Type of password operation (e.g., "Password Change", "Password Reset")</param>
    /// <returns>The callback URL received from the password operation flow, or null if cancelled</returns>
    Task<string?> HandlePasswordOperationAsync(string passwordUrl, string callbackUrlScheme, string operationType);
}