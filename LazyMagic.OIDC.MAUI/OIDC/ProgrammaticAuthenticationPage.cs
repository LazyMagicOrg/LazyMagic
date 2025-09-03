using Microsoft.Extensions.Logging;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Dispatching;

namespace LazyMagic.OIDC.MAUI;

/// <summary>
/// Programmatically created authentication page that replaces AuthenticationWebView.xaml
/// </summary>
public class ProgrammaticAuthenticationPage : ContentPage, IAuthenticationPage
{
    private readonly ILogger<ProgrammaticAuthenticationPage> _logger;
    private readonly TaskCompletionSource<string?> _authCompletionSource;
    private readonly string _callbackUrlPrefix;
    private readonly bool _clearSession;
    private readonly IAuthenticationUI _ui;
    private bool _callbackHandled = false;
    
    private WebView _webView = null!;
    private ActivityIndicator _loadingIndicator = null!;

    public ProgrammaticAuthenticationPage(
        string authUrl, 
        string callbackUrlPrefix, 
        ILogger<ProgrammaticAuthenticationPage> logger, 
        IAuthenticationUI? ui = null, 
        bool clearSession = false)
    {
        _logger = logger;
        _callbackUrlPrefix = callbackUrlPrefix;
        _clearSession = clearSession;
        _ui = ui ?? new DefaultAuthenticationUI();
        _authCompletionSource = new TaskCompletionSource<string?>();
        
        Title = "Sign In";
        
        CreateUI();
        
        // If we need to clear session, do it when the WebView handler is ready
        if (_clearSession)
        {
            _logger.LogInformation("Will clear WebView session data when handler is ready");
            _webView.HandlerChanged += OnWebViewHandlerChanged;
        }
        
        // Navigate to the authentication URL
        _webView.Source = authUrl;
        _logger.LogInformation("Loading authentication URL: {AuthUrl}", authUrl);
    }
    
    private void CreateUI()
    {
        // Create WebView and loading indicator
        _webView = new WebView();
        _webView.Navigating += OnNavigating;
        _webView.Navigated += OnNavigated;
        
        _loadingIndicator = new ActivityIndicator
        {
            IsVisible = true,
            IsRunning = true,
            Color = Color.FromArgb("#007ACC")
        };

        // Use the injected UI to create the layout
        Content = _ui.CreateAuthenticationLayout(
            title: "Sign In",
            headerColor: "#007ACC",
            webView: _webView,
            loadingIndicator: _loadingIndicator,
            onCloseClicked: OnCloseClicked
        );
    }
    
    private void OnWebViewHandlerChanged(object? sender, EventArgs e)
    {
        if (_clearSession && _webView.Handler != null)
        {
            _logger.LogInformation("=== WEBVIEW SESSION CLEAR INITIATED ===");
            _logger.LogInformation("WebView handler is ready, proceeding to clear session data");
            
            // Clear session before loading the auth page
            WebViewSessionHelper.ClearWebViewSession(_logger);
            
            _logger.LogInformation("=== WEBVIEW SESSION CLEAR COMPLETED ===");
            
            // Only clear once
            _webView.HandlerChanged -= OnWebViewHandlerChanged;
        }
    }

    public Task<string?> WaitForCallbackAsync()
    {
        return _authCompletionSource.Task;
    }

    private void OnNavigating(object? sender, WebNavigatingEventArgs e)
    {
        _logger.LogInformation("WebView navigating to: {Url}", e.Url);
        
        // Check if this is our callback URL
        if (e.Url.StartsWith(_callbackUrlPrefix, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("Callback URL detected: {CallbackUrl}", e.Url);
            
            // Mark callback as handled
            _callbackHandled = true;
            
            // Cancel the navigation since we don't need to load the callback page
            e.Cancel = true;
            
            // Complete the authentication with the callback URL
            _authCompletionSource.TrySetResult(e.Url);
            
            // Close this modal page
            _ = Task.Run(async () =>
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await Application.Current.MainPage.Navigation.PopModalAsync();
                });
            });
        }
    }

    private void OnNavigated(object? sender, WebNavigatedEventArgs e)
    {
        // Hide loading indicator once navigation completes
        _loadingIndicator.IsVisible = false;
        _loadingIndicator.IsRunning = false;
        
        _logger.LogInformation("WebView navigated to: {Url}", e.Url);
        
        // Log navigation results but don't show error dialogs to user
        // User cancellation is a normal flow and shouldn't show error messages
        if (e.Result != WebNavigationResult.Success && !_callbackHandled)
        {
            _logger.LogInformation("WebView navigation result: {Result} (user may have cancelled)", e.Result);
        }
        else if (_callbackHandled)
        {
            _logger.LogInformation("Authentication callback handled successfully");
        }
    }

    private async void OnCloseClicked(object? sender, EventArgs e)
    {
        _logger.LogInformation("User closed authentication dialog");
        
        // Complete with null to indicate cancellation
        _authCompletionSource.TrySetResult(null);
        
        // Close this modal page
        await Application.Current.MainPage.Navigation.PopModalAsync();
    }

    protected override bool OnBackButtonPressed()
    {
        // Handle hardware back button on Android
        _logger.LogInformation("User pressed back button during authentication");
        _authCompletionSource.TrySetResult(null);
        return base.OnBackButtonPressed();
    }
}