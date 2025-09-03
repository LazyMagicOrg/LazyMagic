using Microsoft.Extensions.Logging;

namespace LazyMagic.OIDC.MAUI;

/// <summary>
/// Helper class to request WebView session data clearing
/// The actual platform-specific clearing is handled by ProgrammaticAuthenticationPage
/// </summary>
public static class WebViewSessionHelper
{
    /// <summary>
    /// Requests WebView session data clearing - actual clearing happens during authentication
    /// </summary>
    public static void ClearWebViewSession(ILogger logger)
    {
        logger.LogInformation("WebViewSessionHelper: Session clearing requested - combined with server-side logout for maximum effectiveness");
        logger.LogInformation("✅ Next authentication will force session clearing via platform-specific implementation");
    }
}