using Microsoft.Maui.Controls;

namespace LazyMagic.OIDC.MAUI;

/// <summary>
/// Interface for creating authentication-related UI components
/// </summary>
public interface IAuthenticationUI
{
    /// <summary>
    /// Creates the UI layout for authentication pages
    /// </summary>
    /// <param name="title">The title to display in the header</param>
    /// <param name="headerColor">The background color for the header</param>
    /// <param name="webView">The WebView control to include in the layout</param>
    /// <param name="loadingIndicator">The loading indicator to include in the layout</param>
    /// <param name="onCloseClicked">The event handler for the close button</param>
    /// <returns>The complete View to use as the page content</returns>
    View CreateAuthenticationLayout(
        string title,
        string headerColor,
        WebView webView,
        ActivityIndicator loadingIndicator,
        EventHandler onCloseClicked);
}