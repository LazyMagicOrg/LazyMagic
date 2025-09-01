using Microsoft.Extensions.Logging;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Dispatching;

namespace LazyMagic.OIDC.MAUI;

/// <summary>
/// Programmatically created password page for password change/reset flows
/// </summary>
public class ProgrammaticPasswordPage : ContentPage, IPasswordPage
{
    private readonly ILogger<ProgrammaticPasswordPage> _logger;
    private readonly TaskCompletionSource<string?> _passwordCompletionSource;
    private readonly string _callbackUrlPrefix;
    private readonly string _pageTitle;
    private readonly string _pageColor;
    private readonly IAuthenticationUI _ui;
    private bool _callbackHandled = false;
    
    private WebView _webView = null!;
    private ActivityIndicator _loadingIndicator = null!;

    public ProgrammaticPasswordPage(
        string passwordUrl, 
        string callbackUrlPrefix, 
        ILogger<ProgrammaticPasswordPage> logger, 
        IAuthenticationUI? ui = null,
        string pageTitle = "Password", 
        string pageColor = "#FFA500")
    {
        _logger = logger;
        _callbackUrlPrefix = callbackUrlPrefix;
        _pageTitle = pageTitle;
        _pageColor = pageColor;
        _ui = ui ?? new DefaultAuthenticationUI();
        _passwordCompletionSource = new TaskCompletionSource<string?>();
        
        Title = _pageTitle;
        
        CreateUI();
        
        // Navigate to the password URL
        _webView.Source = passwordUrl;
        _logger.LogInformation("Loading {PageTitle} URL: {PasswordUrl}", _pageTitle, passwordUrl);
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
            Color = Color.FromArgb(_pageColor)
        };

        // Use the injected UI to create the layout
        Content = _ui.CreateAuthenticationLayout(
            title: _pageTitle,
            headerColor: _pageColor,
            webView: _webView,
            loadingIndicator: _loadingIndicator,
            onCloseClicked: OnCloseClicked
        );
    }

    public Task<string?> WaitForCallbackAsync()
    {
        return _passwordCompletionSource.Task;
    }

    private void OnNavigating(object? sender, WebNavigatingEventArgs e)
    {
        _logger.LogInformation("{PageTitle} WebView navigating to: {Url}", _pageTitle, e.Url);
        
        // Check if this is our callback URL
        if (e.Url.StartsWith(_callbackUrlPrefix, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("{PageTitle} callback URL detected: {CallbackUrl}", _pageTitle, e.Url);
            
            // Mark callback as handled
            _callbackHandled = true;
            
            // Cancel the navigation since we don't need to load the callback page
            e.Cancel = true;
            
            // Complete the password operation with the callback URL
            _passwordCompletionSource.TrySetResult(e.Url);
            
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
        
        _logger.LogInformation("{PageTitle} WebView navigated to: {Url}", _pageTitle, e.Url);
        
        // Log navigation results but don't show error dialogs to user
        if (e.Result != WebNavigationResult.Success && !_callbackHandled)
        {
            _logger.LogInformation("{PageTitle} WebView navigation result: {Result} (user may have cancelled)", _pageTitle, e.Result);
        }
        else if (_callbackHandled)
        {
            _logger.LogInformation("{PageTitle} callback handled successfully", _pageTitle);
        }
    }

    private async void OnCloseClicked(object? sender, EventArgs e)
    {
        _logger.LogInformation("User closed {PageTitle} dialog", _pageTitle);
        
        // Complete with null to indicate cancellation
        _passwordCompletionSource.TrySetResult(null);
        
        // Close this modal page
        await Application.Current.MainPage.Navigation.PopModalAsync();
    }

    protected override bool OnBackButtonPressed()
    {
        // Handle hardware back button on Android
        _logger.LogInformation("User pressed back button during {PageTitle}", _pageTitle);
        _passwordCompletionSource.TrySetResult(null);
        return base.OnBackButtonPressed();
    }
}