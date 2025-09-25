using Microsoft.Extensions.Logging;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Dispatching;

namespace LazyMagic.OIDC.MAUI;

/// <summary>
/// Programmatically created logout page that handles logout flow
/// </summary>
public class ProgrammaticLogoutPage : ContentPage, ILogoutPage
{
    private readonly ILogger<ProgrammaticLogoutPage> _logger;
    private readonly TaskCompletionSource<bool> _logoutCompletionSource;
    private readonly IAuthenticationUI _ui;
    
    private WebView _webView = null!;
    private ActivityIndicator _loadingIndicator = null!;

    public ProgrammaticLogoutPage(
        string logoutUrl, 
        ILogger<ProgrammaticLogoutPage> logger,
        IAuthenticationUI? ui = null)
    {
        _logger = logger;
        _ui = ui ?? new DefaultAuthenticationUI();
        _logoutCompletionSource = new TaskCompletionSource<bool>();
        
        Title = "Log Out";
        
        CreateUI();
        
        // Clear session when the WebView handler is ready
        _logger.LogInformation("Will clear WebView session data when handler is ready");
        _webView.HandlerChanged += OnWebViewHandlerChanged;
        
        // Navigate to the logout URL
        _webView.Source = logoutUrl;
        _logger.LogInformation("Loading logout URL: {LogoutUrl}", logoutUrl);
        
        // Auto-close after a delay since logout typically doesn't require user interaction
        _ = Task.Run(async () =>
        {
            await Task.Delay(3000); // Wait 3 seconds for logout to complete
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                _logoutCompletionSource.TrySetResult(true);
                if (Navigation != null)
                {
                    await Navigation.PopModalAsync();
                }
            });
        });
    }
    
    private void CreateUI()
    {
        // Create WebView (hidden for logout)
        _webView = new WebView
        {
            IsVisible = false // Hide WebView for logout to avoid showing error pages
        };
        _webView.Navigating += OnNavigating;
        _webView.Navigated += OnNavigated;
        
        _loadingIndicator = new ActivityIndicator
        {
            IsVisible = true,
            IsRunning = true,
            Color = Color.FromArgb("#DC3545") // Red for logout
        };

        // Use the injected UI to create the layout
        var layout = _ui.CreateAuthenticationLayout(
            title: "Signing Out",
            headerColor: "#DC3545", // Red for logout
            webView: _webView,
            loadingIndicator: _loadingIndicator,
            onCloseClicked: OnCloseClicked
        );
        
        // Add a status message
        if (layout is Grid grid && grid.Children.Count > 1)
        {
            var statusStack = new StackLayout
            {
                VerticalOptions = LayoutOptions.Center,
                HorizontalOptions = LayoutOptions.Center,
                Spacing = 10
            };
            
            var statusLabel = new Label
            {
                Text = "Please wait while we log you out...",
                FontSize = 16,
                TextColor = Color.FromArgb("#666666"),
                HorizontalTextAlignment = TextAlignment.Center
            };
            statusStack.Children.Add(statusLabel);
            
            Grid.SetRow(statusStack, 1);
            grid.Children.Insert(grid.Children.Count - 1, statusStack); // Insert before loading indicator
        }
        
        Content = layout;
    }
    
    private void OnWebViewHandlerChanged(object? sender, EventArgs e)
    {
        if (_webView.Handler != null)
        {
            _logger.LogInformation("=== LOGOUT WEBVIEW SESSION CLEAR INITIATED ===");
            _logger.LogInformation("WebView handler is ready, proceeding to clear session data");
            
            // Clear session before/during logout
            WebViewSessionHelper.ClearWebViewSession(_logger);
            
            _logger.LogInformation("=== LOGOUT WEBVIEW SESSION CLEAR COMPLETED ===");
            
            // Only clear once
            _webView.HandlerChanged -= OnWebViewHandlerChanged;
        }
    }

    public Task<bool> WaitForLogoutAsync()
    {
        return _logoutCompletionSource.Task;
    }

    private void OnNavigating(object? sender, WebNavigatingEventArgs e)
    {
        _logger.LogInformation("Logout WebView navigating to: {Url}", e.Url);
    }

    private void OnNavigated(object? sender, WebNavigatedEventArgs e)
    {
        _logger.LogInformation("Logout WebView navigated to: {Url}, Result: {Result}", e.Url, e.Result);
        
        // Hide loading indicator once navigation completes (even though WebView is hidden)
        _loadingIndicator.IsVisible = false;
        _loadingIndicator.IsRunning = false;
        
        if (e.Result == WebNavigationResult.Success)
        {
            _logger.LogInformation("✅ Logout URL loaded successfully");
        }
        else
        {
            _logger.LogWarning("⚠️ Logout URL failed to load: {Result}", e.Result);
        }
    }

    private async void OnCloseClicked(object? sender, EventArgs e)
    {
        _logger.LogInformation("User closed logout dialog");
        
        // Complete the logout
        _logoutCompletionSource.TrySetResult(true);
        
        // Close this modal page
        await Application.Current.MainPage.Navigation.PopModalAsync();
    }

    protected override bool OnBackButtonPressed()
    {
        // Handle hardware back button on Android
        _logger.LogInformation("User pressed back button during logout");
        _logoutCompletionSource.TrySetResult(true);
        return base.OnBackButtonPressed();
    }
}