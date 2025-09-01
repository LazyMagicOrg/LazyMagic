using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;

namespace LazyMagic.OIDC.MAUI;

/// <summary>
/// MAUI implementation of IOIDCService using real Cognito authentication
/// Uses WebAuthenticator for OAuth flows
/// </summary>
public class MauiOIDCService : IOIDCService, IDisposable
{
    private readonly ILogger<MauiOIDCService> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IOidcConfig _oidcConfig;
    private readonly ITokenStorageService _tokenStorage;
    private readonly IWebViewAuthenticationProvider? _webViewProvider;
    private OIDCAuthenticationState? _currentState;
    private string? _accessToken;
    private string? _idToken;
    private string? _pendingAuthUrl;
    private bool _forceLogin = false;

    public event EventHandler<OIDCAuthenticationStateChangedEventArgs>? AuthenticationStateChanged;
    public event Action<string>? OnAuthenticationRequested;

    public MauiOIDCService(ILogger<MauiOIDCService> logger, ILoggerFactory loggerFactory, IOidcConfig oidcConfig, ITokenStorageService tokenStorage, IWebViewAuthenticationProvider? webViewProvider = null)
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
        _oidcConfig = oidcConfig;
        _tokenStorage = tokenStorage;
        _webViewProvider = webViewProvider;
        
        // Initialize with unauthenticated state
        _currentState = new OIDCAuthenticationState
        {
            IsAuthenticated = false
        };
        
        // Check for stored tokens on startup
        _ = Task.Run(async () => 
        {
            _logger.LogInformation("=== STARTUP SESSION CHECK ===");
            
            // Check if user explicitly logged out
            _forceLogin = await _tokenStorage.HasLoggedOutAsync();
            _logger.LogInformation("Logout flag on startup: {ForceLogin}", _forceLogin);
            
            if (!_forceLogin)
            {
                _logger.LogInformation("No logout flag - attempting session restore");
                var restored = await TryRestoreSessionAsync();
                _logger.LogInformation("Session restore result: {Restored}", restored);
            }
            else
            {
                _logger.LogInformation("User had logged out - will require fresh login");
            }
            
            _logger.LogInformation("=== STARTUP SESSION CHECK COMPLETE ===");
        });
    }
    
    /// <summary>
    /// Tries to restore a previous session from stored tokens
    /// </summary>
    private async Task<bool> TryRestoreSessionAsync()
    {
        try
        {
            var (accessToken, idToken, refreshToken) = await _tokenStorage.GetTokensAsync();
            
            if (!string.IsNullOrEmpty(idToken))
            {
                _logger.LogInformation("Found stored tokens, attempting to restore session");
                
                // Parse the ID token to check if it's still valid
                var userInfo = ParseIdToken(idToken);
                if (userInfo != null && userInfo.TokenExpiry > DateTime.UtcNow)
                {
                    _accessToken = accessToken;
                    _idToken = idToken;
                    _currentState = userInfo;
                    
                    AuthenticationStateChanged?.Invoke(this, new OIDCAuthenticationStateChangedEventArgs(_currentState));
                    _logger.LogInformation("Session restored for user: {UserName}", userInfo.UserName);
                    return true;
                }
                else if (!string.IsNullOrEmpty(refreshToken))
                {
                    _logger.LogInformation("Token expired, attempting refresh");
                    // TODO: Implement token refresh
                    return false;
                }
            }
            
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restore session");
            return false;
        }
    }

    public Task<OIDCAuthenticationState> GetAuthenticationStateAsync()
    {
        return Task.FromResult(_currentState ?? new OIDCAuthenticationState { IsAuthenticated = false });
    }

    public Task<ClaimsPrincipal?> GetCurrentUserAsync()
    {
        if (_currentState?.IsAuthenticated ?? false)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, _currentState.UserName ?? "User"),
                new Claim(ClaimTypes.Email, _currentState.Email ?? "user@example.com")
            };
            
            var identity = new ClaimsIdentity(claims, "maui");
            return Task.FromResult<ClaimsPrincipal?>(new ClaimsPrincipal(identity));
        }
        
        return Task.FromResult<ClaimsPrincipal?>(null);
    }

    public Task<bool> IsAuthenticatedAsync()
    {
        return Task.FromResult(_currentState?.IsAuthenticated ?? false);
    }

    public Task<string?> GetAccessTokenAsync()
    {
        return Task.FromResult(_accessToken);
    }

    public async Task<IEnumerable<Claim>> GetUserClaimsAsync()
    {
        var user = await GetCurrentUserAsync();
        return user?.Claims ?? Enumerable.Empty<Claim>();
    }

    public async Task<string?> GetClaimValueAsync(string claimType)
    {
        var claims = await GetUserClaimsAsync();
        return claims.FirstOrDefault(c => c.Type == claimType)?.Value;
    }

    public async Task<bool> IsInRoleAsync(string role)
    {
        var user = await GetCurrentUserAsync();
        return user?.IsInRole(role) ?? false;
    }

    /// <summary>
    /// Initiates login using system browser
    /// </summary>
    public async Task<bool> LoginAsync()
    {
        try
        {
            _logger.LogInformation("🚀 Starting LoginAsync...");
            
            // Get the authentication URL (will include prompt=login if _forceLogin is true)
            _logger.LogInformation("📝 Getting authentication URL...");
            var authUrl = await GetAuthenticationUrlAsync();
            if (string.IsNullOrEmpty(authUrl))
            {
                _logger.LogError("❌ Failed to get authentication URL - cannot proceed with login");
                return false;
            }
            _logger.LogInformation("✅ Got authentication URL: {AuthUrl}", authUrl);

            // Use WebView for authentication if provider is available
            if (_webViewProvider != null)
            {
                _logger.LogInformation("🌐 Using WebView for authentication - calling AuthenticateAsync...");
                
                var callbackUrl = await _webViewProvider.AuthenticateAsync(authUrl, "awsloginmaui://auth-callback");
                
                _logger.LogInformation("✅ WebView AuthenticateAsync completed, received callback: {CallbackUrl}", callbackUrl ?? "null");
                
                if (!string.IsNullOrEmpty(callbackUrl))
                {
                    _logger.LogInformation("📞 Processing callback from WebView: {CallbackUrl}", callbackUrl);
                    var success = await HandleAuthCallbackAsync(callbackUrl);
                    _logger.LogInformation("🎯 HandleAuthCallbackAsync result: {Success}", success);
                    return success;
                }
                else
                {
                    _logger.LogInformation("❌ Authentication was cancelled by user");
                    return false;
                }
            }
            else
            {
                _logger.LogWarning("No WebView authentication provider available. Please register IWebViewAuthenticationProvider in DI container.");
                
                // Fallback: Use the OnAuthenticationRequested event to delegate to the UI layer
                if (OnAuthenticationRequested != null)
                {
                    _logger.LogInformation("Delegating authentication request to UI layer");
                    OnAuthenticationRequested.Invoke(authUrl);
                    
                    // The UI layer should call HandleAuthCallbackAsync when authentication completes
                    // For now, return false as this requires manual handling
                    return false;
                }
                else
                {
                    _logger.LogError("No authentication mechanism available. Either register IWebViewAuthenticationProvider or subscribe to OnAuthenticationRequested event.");
                    return false;
                }
            }
            
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login");
            return false;
        }
    }

    /// <summary>
    /// Gets the authentication URL for system browser
    /// </summary>
    public Task<string?> GetAuthenticationUrlAsync()
    {
        try
        {
            var selectedConfig = _oidcConfig.SelectedAuthConfig;
            var authConfigs = _oidcConfig.AuthConfigs;
            
            if (authConfigs == null || !authConfigs.TryGetValue(selectedConfig, out var authConfig))
            {
                _logger.LogWarning("No auth config found for: {SelectedConfig}", selectedConfig);
                return Task.FromResult<string?>(null);
            }

            // Extract AWS Cognito configuration values
            var userPoolId = authConfig["userPoolId"]?.ToString();
            var userPoolClientId = authConfig["userPoolClientId"]?.ToString();
            var awsRegion = authConfig["awsRegion"]?.ToString();
            
            // Use the configured Cognito domain for OAuth operations
            var authority = !string.IsNullOrEmpty(awsRegion) 
                ? $"https://magicpets.auth.{awsRegion}.amazoncognito.com"
                : null;
            var clientId = userPoolClientId;
            
            if (string.IsNullOrEmpty(authority) || string.IsNullOrEmpty(clientId))
            {
                _logger.LogWarning("Missing authority or clientId for auth config: {SelectedConfig}", selectedConfig);
                return Task.FromResult<string?>(null);
            }

            // Build authorization URL
            var state = Guid.NewGuid().ToString();
            var nonce = Guid.NewGuid().ToString();
            var redirectUri = "awsloginmaui://auth-callback";
            
            var authUrl = $"{authority}/oauth2/authorize?" +
                         $"response_type=code&" +
                         $"client_id={clientId}&" +
                         $"redirect_uri={HttpUtility.UrlEncode(redirectUri)}&" +
                         $"scope=openid%20email%20profile&" +
                         $"state={state}&" +
                         $"nonce={nonce}";
            
            // Add prompt=login if we need to force fresh login (after logout)
            if (_forceLogin)
            {
                authUrl += "&prompt=login";
                _logger.LogInformation("Added prompt=login to auth URL because user logged out");
                _forceLogin = false; // Reset the flag
                _ = _tokenStorage.SetLoggedOutAsync(false); // Clear the persisted flag
            }

            _logger.LogInformation("Generated authentication URL successfully");
            return Task.FromResult<string?>(authUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating authentication URL");
            return Task.FromResult<string?>(null);
        }
    }

    /// <summary>
    /// Handles the authentication callback from the WebView
    /// This method is public to allow the UI layer to call it after receiving the callback
    /// </summary>
    public async Task<bool> HandleAuthCallbackAsync(string callbackUrl)
    {
        try
        {
            _logger.LogInformation("Handling auth callback: {CallbackUrl}", callbackUrl);

            // Parse the callback URL to extract the authorization code
            var uri = new Uri(callbackUrl.Replace("awsloginmaui://", "https://"));
            var query = HttpUtility.ParseQueryString(uri.Query);
            
            var code = query["code"];
            var error = query["error"];
            
            if (!string.IsNullOrEmpty(error))
            {
                _logger.LogError("Authentication error: {Error}", error);
                return false;
            }
            
            if (string.IsNullOrEmpty(code))
            {
                _logger.LogError("No authorization code found in callback URL");
                return false;
            }

            // Get auth configuration for token exchange
            var selectedConfig = _oidcConfig.SelectedAuthConfig ?? "ConsumerAuth";
            if (!_oidcConfig.AuthConfigs.TryGetValue(selectedConfig, out var authConfig))
            {
                _logger.LogError("No auth configuration found for token exchange");
                return false;
            }

            // Extract AWS Cognito configuration values  
            var userPoolId = authConfig["userPoolId"]?.ToString();
            var userPoolClientId = authConfig["userPoolClientId"]?.ToString();
            var awsRegion = authConfig["awsRegion"]?.ToString();
            
            // Use the configured Cognito domain for OAuth operations
            var authority = !string.IsNullOrEmpty(awsRegion) 
                ? $"https://magicpets.auth.{awsRegion}.amazoncognito.com"
                : null;
            var clientId = userPoolClientId;
            
            if (string.IsNullOrEmpty(authority) || string.IsNullOrEmpty(clientId))
            {
                _logger.LogError("Missing authority or clientId in configuration");
                return false;
            }

            // Exchange code for tokens - use the same redirect URI that was used in the auth request
            var redirectUri = callbackUrl.StartsWith("http://localhost:7777") 
                ? "http://localhost:7777/auth-callback" 
                : "awsloginmaui://auth-callback";
            var tokens = await ExchangeCodeForTokensAsync(authority, clientId, code, redirectUri);
            if (tokens != null)
            {
                _accessToken = tokens.AccessToken;
                _idToken = tokens.IdToken;
                
                _logger.LogInformation("Received tokens - AccessToken length: {AccessTokenLength}, IdToken length: {IdTokenLength}", 
                    tokens.AccessToken?.Length ?? 0, tokens.IdToken?.Length ?? 0);
                
                // Parse user information from ID token
                var userInfo = ParseIdToken(tokens.IdToken);
                if (userInfo != null)
                {
                    _currentState = userInfo;
                    
                    // Save tokens for Remember Me functionality
                    await _tokenStorage.SaveTokensAsync(tokens.AccessToken, tokens.IdToken, tokens.RefreshToken);
                    
                    AuthenticationStateChanged?.Invoke(this, new OIDCAuthenticationStateChangedEventArgs(_currentState));
                    
                    _logger.LogInformation("User logged in successfully: {UserName}", userInfo.UserName);
                    return true;
                }
            }
            
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling auth callback");
            return false;
        }
    }

    /// <summary>
    /// Logs out the user
    /// </summary>
    public async Task LogoutAsync()
    {
        _logger.LogInformation("=== LOGOUT STARTED ===");
        
        // Clear local state
        _currentState = new OIDCAuthenticationState
        {
            IsAuthenticated = false
        };
        
        _accessToken = null;
        _idToken = null;
        _logger.LogInformation("Cleared in-memory tokens and state");
        
        // Clear stored tokens
        await _tokenStorage.ClearTokensAsync();
        _logger.LogInformation("Called ClearTokensAsync()");
        
        // Verify tokens were cleared
        var verifyTokens = await _tokenStorage.GetTokensAsync();
        _logger.LogInformation("Verification after ClearTokensAsync - AccessToken: {HasAccess}, IdToken: {HasId}, RefreshToken: {HasRefresh}",
            !string.IsNullOrEmpty(verifyTokens.accessToken),
            !string.IsNullOrEmpty(verifyTokens.idToken),
            !string.IsNullOrEmpty(verifyTokens.refreshToken));
        
        // CRITICAL: Clear WebView session data AND perform server-side logout
        if (_webViewProvider != null)
        {
            _logger.LogInformation("🧹 Clearing WebView session data during logout...");
            await _webViewProvider.ClearWebViewSessionAsync();
            _logger.LogInformation("✅ WebView session data cleared");
            
            // Perform server-side logout by navigating to Cognito logout endpoint
            _logger.LogInformation("🚪 Performing server-side logout via Cognito logout endpoint...");
            await PerformServerSideLogoutAsync();
        }
        else
        {
            _logger.LogWarning("No WebView provider available - cannot clear WebView session data");
        }
        
        // Mark that we need fresh login on next attempt (persisted)
        // This is CRITICAL - without this, prompt=login won't be added
        await _tokenStorage.SetLoggedOutAsync(true);
        _forceLogin = true;
        _logger.LogInformation("✅ CRITICAL: Set logout flag and _forceLogin = true - next login will include prompt=login");
        
        // Verify logout flag was set
        var verifyLogout = await _tokenStorage.HasLoggedOutAsync();
        _logger.LogInformation("✅ Verification after SetLoggedOutAsync - Logout flag: {LogoutFlag}", verifyLogout);
        _logger.LogInformation("✅ In-memory _forceLogin flag: {ForceLogin}", _forceLogin);
        
        AuthenticationStateChanged?.Invoke(this, new OIDCAuthenticationStateChangedEventArgs(_currentState));
        _logger.LogInformation("Fired AuthenticationStateChanged event");
        
        _logger.LogInformation("=== LOGOUT COMPLETED - WEBVIEW SESSION CLEARED - NEXT LOGIN WILL FORCE CREDENTIALS ===");
    }

    private async Task<TokenResponse?> ExchangeCodeForTokensAsync(string authority, string clientId, string code, string redirectUri)
    {
        try
        {
            var tokenUrl = $"{authority}/oauth2/token";
            
            var parameters = new Dictionary<string, string>
            {
                {"grant_type", "authorization_code"},
                {"client_id", clientId},
                {"code", code},
                {"redirect_uri", redirectUri}
            };

            using var httpClient = new HttpClient();
            var content = new FormUrlEncodedContent(parameters);
            
            _logger.LogInformation("Exchanging code for tokens at: {TokenUrl}", tokenUrl);
            var response = await httpClient.PostAsync(tokenUrl, content);
            
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("Token response JSON: {Json}", json);
                
                var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                _logger.LogInformation("Successfully exchanged code for tokens");
                _logger.LogInformation("Deserialized - AccessToken: {HasAccessToken}, IdToken: {HasIdToken}, RefreshToken: {HasRefreshToken}",
                    !string.IsNullOrEmpty(tokenResponse?.AccessToken),
                    !string.IsNullOrEmpty(tokenResponse?.IdToken),
                    !string.IsNullOrEmpty(tokenResponse?.RefreshToken));
                    
                return tokenResponse;
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Token exchange failed: {StatusCode} - {Error}", response.StatusCode, error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exchanging code for tokens");
        }
        
        return null;
    }

    private OIDCAuthenticationState? ParseIdToken(string idToken)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var token = handler.ReadJwtToken(idToken);
            
            var claims = token.Claims.ToDictionary(c => c.Type, c => c.Value);
            
            return new OIDCAuthenticationState
            {
                IsAuthenticated = true,
                UserName = claims.TryGetValue("cognito:username", out var username) ? username :
                          claims.TryGetValue("preferred_username", out var prefUsername) ? prefUsername :
                          claims.TryGetValue("name", out var name) ? name : "Unknown User",
                Email = claims.TryGetValue("email", out var email) ? email : null,
                TokenExpiry = token.ValidTo,
                Claims = claims
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing ID token");
            return null;
        }
    }

    private async Task PerformServerSideLogoutAsync()
    {
        try
        {
            // Get auth configuration to build logout URL
            var selectedConfig = _oidcConfig.SelectedAuthConfig ?? "ConsumerAuth";
            if (!_oidcConfig.AuthConfigs.TryGetValue(selectedConfig, out var authConfig))
            {
                _logger.LogWarning("No auth configuration found for server-side logout");
                return;
            }

            // Extract AWS Cognito configuration values  
            var userPoolId = authConfig["userPoolId"]?.ToString();
            var userPoolClientId = authConfig["userPoolClientId"]?.ToString();
            var awsRegion = authConfig["awsRegion"]?.ToString();
            
            // Use the configured Cognito domain for logout
            var authority = !string.IsNullOrEmpty(awsRegion) 
                ? $"https://magicpets.auth.{awsRegion}.amazoncognito.com"
                : null;
                
            if (string.IsNullOrEmpty(authority) || string.IsNullOrEmpty(userPoolClientId))
            {
                _logger.LogWarning("Missing authority or clientId for server-side logout");
                return;
            }

            // Build Cognito logout URL - try without redirect_uri first to avoid configuration requirement
            var logoutUrl = $"{authority}/logout?client_id={userPoolClientId}";
            _logger.LogInformation("Navigating to Cognito logout URL: {LogoutUrl}", logoutUrl);
            
            // Create the logout page with optional UI injection
            IAuthenticationUI? customUI = null;
            if (_webViewProvider is MauiWebViewAuthenticationProvider webViewProvider)
            {
                // Try to get the UI from the provider using reflection
                customUI = webViewProvider.GetType().GetField("_ui", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(webViewProvider) as IAuthenticationUI;
            }
            
            // Create and show the logout page with the appropriate UI
            await ShowLogoutPageAsync(logoutUrl, customUI);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during server-side logout");
        }
    }

    private async Task ShowLogoutPageAsync(string logoutUrl, IAuthenticationUI? customUI = null)
    {
        // Create dedicated logout page with optional custom UI
        var pageLogger = _loggerFactory.CreateLogger<ProgrammaticLogoutPage>();
        var logoutPage = new ProgrammaticLogoutPage(logoutUrl, pageLogger, customUI);
        
        // Show the logout page
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            if (Application.Current?.MainPage?.Navigation != null)
            {
                _logger.LogInformation("📱 Showing dedicated logout page...");
                await Application.Current.MainPage.Navigation.PushModalAsync(logoutPage);
                _logger.LogInformation("✅ Logout page displayed");
            }
        });

        // Wait for logout to complete
        var logoutCompleted = await logoutPage.WaitForLogoutAsync();
        
        // Close the logout page
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            if (Application.Current?.MainPage?.Navigation != null)
            {
                await Application.Current.MainPage.Navigation.PopModalAsync();
                _logger.LogInformation("✅ Server-side logout completed and page closed");
            }
        });
    }

    private class TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = "";
        
        [JsonPropertyName("id_token")]
        public string IdToken { get; set; } = "";
        
        [JsonPropertyName("refresh_token")]
        public string RefreshToken { get; set; } = "";
        
        [JsonPropertyName("token_type")]
        public string TokenType { get; set; } = "";
        
        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
    }


    public void Dispose()
    {
        // Cleanup if needed
    }
}