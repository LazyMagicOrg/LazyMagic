using Microsoft.Extensions.Logging;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;

namespace LazyMagic.OIDC.MAUI;

/// <summary>
/// MAUI implementation of IWebViewAuthenticationProvider that creates authentication UI programmatically
/// </summary>
public class MauiWebViewAuthenticationProvider : IWebViewAuthenticationProvider
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<MauiWebViewAuthenticationProvider> _logger;
    private readonly IAuthenticationUI _ui;
    private bool _shouldClearSessionOnNextAuth = false;

    public MauiWebViewAuthenticationProvider(ILoggerFactory loggerFactory, IAuthenticationUI? ui = null)
    {
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _logger = loggerFactory.CreateLogger<MauiWebViewAuthenticationProvider>();
        _ui = ui ?? new DefaultAuthenticationUI();
    }

    public async Task<string?> AuthenticateAsync(string authUrl, string callbackUrlScheme)
    {
        try
        {
            _logger.LogInformation("Starting WebView authentication with URL: {AuthUrl}", authUrl);

            // Create the authentication page
            var pageLogger = _loggerFactory.CreateLogger<ProgrammaticAuthenticationPage>();
            
            // Clear session if explicitly requested via logout OR if URL contains prompt=login
            var hasPromptLogin = authUrl.Contains("prompt=login", StringComparison.OrdinalIgnoreCase);
            var shouldClearSession = _shouldClearSessionOnNextAuth || hasPromptLogin;
            
            _logger.LogInformation("🔍 Session clearing decision - LogoutRequested: {LogoutRequested}, PromptLogin: {PromptLogin}, WillClear: {WillClear}", 
                _shouldClearSessionOnNextAuth, hasPromptLogin, shouldClearSession);
            
            if (shouldClearSession)
            {
                _logger.LogInformation("🧹 WILL CLEAR WEBVIEW SESSION - this should force credential re-entry");
                    
                // Reset the flag after using it
                _shouldClearSessionOnNextAuth = false;
            }
            else
            {
                _logger.LogInformation("❌ Will NOT clear WebView session - user may be auto-logged in");
            }
            
            var authPage = new ProgrammaticAuthenticationPage(authUrl, callbackUrlScheme, pageLogger, _ui, shouldClearSession);

            // Show the authentication page as a modal
            _logger.LogInformation("📱 Pushing authentication page as modal...");
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                // Wait for main page to be ready (reduced timeout)
                var attempts = 0;
                while (Application.Current?.MainPage?.Navigation == null && attempts < 10)
                {
                    await Task.Delay(50);
                    attempts++;
                }

                if (Application.Current?.MainPage?.Navigation != null)
                {
                    _logger.LogInformation("✅ MainPage.Navigation available, pushing modal...");
                    await Application.Current.MainPage.Navigation.PushModalAsync(authPage);
                    _logger.LogInformation("✅ Modal pushed successfully");
                }
                else
                {
                    _logger.LogError("❌ Main page navigation not available after waiting");
                    throw new InvalidOperationException("Main page navigation not available");
                }
            });

            // Wait for the authentication to complete
            _logger.LogInformation("⏳ Waiting for authentication callback...");
            var callbackUrl = await authPage.WaitForCallbackAsync();
            _logger.LogInformation("🎯 WaitForCallbackAsync completed with result: {Result}", callbackUrl ?? "null");
            
            if (!string.IsNullOrEmpty(callbackUrl))
            {
                _logger.LogInformation("Authentication completed successfully with callback: {CallbackUrl}", callbackUrl);
            }
            else
            {
                _logger.LogInformation("Authentication was cancelled by user");
            }

            return callbackUrl;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during WebView authentication");
            return null;
        }
    }

    public async Task ClearWebViewSessionAsync()
    {
        try
        {
            _logger.LogInformation("=== CLEARING WEBVIEW SESSION DATA ===");
            
            // Set flag to ensure next authentication attempt clears WebView session
            _shouldClearSessionOnNextAuth = true;
            _logger.LogInformation("✅ Set flag to clear WebView session on next authentication attempt");
            
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                WebViewSessionHelper.ClearWebViewSession(_logger);
            });
            
            _logger.LogInformation("✅ WebView session clearing requested - will be performed during next authentication");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing WebView session");
        }
    }

    public async Task<string?> HandlePasswordOperationAsync(string passwordUrl, string callbackUrlScheme, string operationType)
    {
        try
        {
            _logger.LogInformation("Starting {OperationType} with URL: {PasswordUrl}", operationType, passwordUrl);

            // Create the password page with appropriate branding
            var pageLogger = _loggerFactory.CreateLogger<ProgrammaticPasswordPage>();
            var pageColor = operationType.ToLower().Contains("reset") ? "#FF6B35" : "#FFA500"; // Different colors for reset vs change
            var passwordPage = new ProgrammaticPasswordPage(passwordUrl, callbackUrlScheme, pageLogger, _ui, operationType, pageColor);

            // Show the password page as a modal
            _logger.LogInformation("📱 Pushing {OperationType} page as modal...", operationType);
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                // Wait for main page to be ready (reduced timeout)
                var attempts = 0;
                while (Application.Current?.MainPage?.Navigation == null && attempts < 10)
                {
                    await Task.Delay(50);
                    attempts++;
                }

                if (Application.Current?.MainPage?.Navigation != null)
                {
                    _logger.LogInformation("✅ MainPage.Navigation available, pushing modal...");
                    await Application.Current.MainPage.Navigation.PushModalAsync(passwordPage);
                    _logger.LogInformation("✅ Modal pushed successfully");
                }
                else
                {
                    _logger.LogError("❌ Main page navigation not available after waiting");
                    throw new InvalidOperationException("Main page navigation not available");
                }
            });

            // Wait for the password operation to complete
            _logger.LogInformation("⏳ Waiting for {OperationType} callback...", operationType);
            var callbackUrl = await passwordPage.WaitForCallbackAsync();
            _logger.LogInformation("🎯 {OperationType} completed with result: {Result}", operationType, callbackUrl ?? "null");
            
            if (!string.IsNullOrEmpty(callbackUrl))
            {
                _logger.LogInformation("{OperationType} completed successfully with callback: {CallbackUrl}", operationType, callbackUrl);
            }
            else
            {
                _logger.LogInformation("{OperationType} was cancelled by user", operationType);
            }

            return callbackUrl;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during {OperationType}", operationType);
            return null;
        }
    }
}