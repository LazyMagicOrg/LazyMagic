using LazyMagic.OIDC.Base.Services;
using LazyMagic.OIDC.WASM.Services;

namespace LazyMagic.OIDC.WASM;

/// <summary>
/// Blazor implementation of IOIDCService
/// This bridges the Blazor OIDC authentication system with the MVVM pattern
/// </summary>
public class BlazorOIDCService : IOIDCService, IDisposable
{
    private readonly AuthenticationStateProvider _authStateProvider;
    private readonly IAccessTokenProvider _tokenProvider;
    private readonly IOidcConfig _oidcConfig;
    private readonly NavigationManager _navigation;
    private readonly ILogger<BlazorOIDCService> _logger;
    private readonly IRememberMeService _rememberMeService;
    private readonly IDynamicConfigurationProvider _configProvider;
    private readonly IFastAuthenticationService? _fastAuth;
    private readonly ITokenRefreshService? _tokenRefreshService;

    public event EventHandler<OIDCAuthenticationStateChangedEventArgs>? AuthenticationStateChanged;
    public event Action<string>? OnAuthenticationRequested;

    public BlazorOIDCService(
        AuthenticationStateProvider authStateProvider,
        IAccessTokenProvider tokenProvider,
        IOidcConfig oidcConfig,
        NavigationManager navigation,
        ILogger<BlazorOIDCService> logger,
        IRememberMeService rememberMeService,
        IDynamicConfigurationProvider configProvider,
        IFastAuthenticationService? fastAuth = null,
        ITokenRefreshService? tokenRefreshService = null)
    {
        _authStateProvider = authStateProvider;
        _tokenProvider = tokenProvider;
        _oidcConfig = oidcConfig;
        _navigation = navigation;
        _logger = logger;
        _rememberMeService = rememberMeService;
        _configProvider = configProvider;
        _fastAuth = fastAuth;
        _tokenRefreshService = tokenRefreshService;

        // Initialize fast auth if available
        if (_fastAuth != null)
        {
            _ = Task.Run(async () => await _fastAuth.InitializeAsync());
        }

        // Subscribe to authentication state changes
        _authStateProvider.AuthenticationStateChanged += OnAuthenticationStateChanged;
    }

    private async void OnAuthenticationStateChanged(Task<Microsoft.AspNetCore.Components.Authorization.AuthenticationState> task)
    {
        try
        {
            // Invalidate fast auth cache since state changed
            if (_fastAuth != null)
            {
                await _fastAuth.InvalidateCacheAsync();
            }
            
            var authState = await task;
            var state = await CreateAuthenticationState(authState.User);
            
            // Cache the new state for future fast auth calls
            if (_fastAuth != null)
            {
                await _fastAuth.CacheAuthenticationStateAsync(authState);
            }
            
            // Start or stop token refresh monitoring based on auth state
            if (_tokenRefreshService != null)
            {
                if (state.IsAuthenticated && state.TokenExpiry.HasValue)
                {
                    _logger.LogInformation("[TokenRefresh] User authenticated, starting token monitoring. Token expires: {Expiry}", 
                        state.TokenExpiry.Value);
                    _tokenRefreshService.UpdateTokenExpiration(state.TokenExpiry.Value);
                    await _tokenRefreshService.StartMonitoringAsync();
                }
                else
                {
                    _logger.LogInformation("[TokenRefresh] User not authenticated, stopping token monitoring");
                    _tokenRefreshService.StopMonitoring();
                }
            }
            
            AuthenticationStateChanged?.Invoke(this, new OIDCAuthenticationStateChangedEventArgs(state));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling authentication state change");
        }
    }

    public async Task<OIDCAuthenticationState> GetAuthenticationStateAsync()
    {
        try
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            _logger.LogInformation("[GetAuthenticationStateAsync][{Timestamp}] Starting authentication check (FastAuth: {HasFastAuth})", DateTime.UtcNow.ToString("HH:mm:ss.fff"), _fastAuth != null);
            
            Microsoft.AspNetCore.Components.Authorization.AuthenticationState authState;
            
            // Try fast auth first if available
            if (_fastAuth != null)
            {
                try
                {
                    _logger.LogDebug("[GetAuthenticationStateAsync][{Timestamp}] Using fast authentication service", DateTime.UtcNow.ToString("HH:mm:ss.fff"));
                    authState = await _fastAuth.GetFastAuthenticationStateAsync();
                    stopwatch.Stop();
                    _logger.LogInformation("[GetAuthenticationStateAsync][{Timestamp}] Fast auth completed in {ElapsedMs}ms", DateTime.UtcNow.ToString("HH:mm:ss.fff"), stopwatch.ElapsedMilliseconds);
                    
                    // If we got an unauthenticated result from fast auth, validate with full check in background
                    if (!authState.User?.Identity?.IsAuthenticated == true)
                    {
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                _logger.LogDebug("[GetAuthenticationStateAsync][{Timestamp}] Running background full auth validation", DateTime.UtcNow.ToString("HH:mm:ss.fff"));
                                var fullAuthState = await _authStateProvider.GetAuthenticationStateAsync();
                                
                                // If full check shows authenticated but fast auth didn't, update cache
                                if (fullAuthState.User?.Identity?.IsAuthenticated == true && 
                                    authState.User?.Identity?.IsAuthenticated != true)
                                {
                                    _logger.LogInformation("[GetAuthenticationStateAsync][{Timestamp}] Background validation found authenticated state, updating cache", DateTime.UtcNow.ToString("HH:mm:ss.fff"));
                                    await _fastAuth.CacheAuthenticationStateAsync(fullAuthState);
                                    
                                    // Trigger authentication state changed event
                                    var state = await CreateAuthenticationState(fullAuthState.User);
                                    AuthenticationStateChanged?.Invoke(this, new OIDCAuthenticationStateChangedEventArgs(state));
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "[GetAuthenticationStateAsync][{Timestamp}] Background validation failed", DateTime.UtcNow.ToString("HH:mm:ss.fff"));
                            }
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[GetAuthenticationStateAsync][{Timestamp}] Fast auth failed, falling back to standard auth", DateTime.UtcNow.ToString("HH:mm:ss.fff"));
                    authState = await _authStateProvider.GetAuthenticationStateAsync();
                    stopwatch.Stop();
                    _logger.LogInformation("[GetAuthenticationStateAsync][{Timestamp}] Standard auth fallback completed in {ElapsedMs}ms", DateTime.UtcNow.ToString("HH:mm:ss.fff"), stopwatch.ElapsedMilliseconds);
                }
            }
            else
            {
                // No fast auth available, use standard provider
                _logger.LogDebug("[GetAuthenticationStateAsync][{Timestamp}] Using standard authentication provider", DateTime.UtcNow.ToString("HH:mm:ss.fff"));
                authState = await _authStateProvider.GetAuthenticationStateAsync();
                stopwatch.Stop();
                _logger.LogInformation("[GetAuthenticationStateAsync][{Timestamp}] Standard auth completed in {ElapsedMs}ms", DateTime.UtcNow.ToString("HH:mm:ss.fff"), stopwatch.ElapsedMilliseconds);
            }
            
            _logger.LogDebug("[GetAuthenticationStateAsync][{Timestamp}] AuthState.User.Identity.IsAuthenticated: {IsAuthenticated}", DateTime.UtcNow.ToString("HH:mm:ss.fff"), authState.User?.Identity?.IsAuthenticated);
            _logger.LogDebug("[GetAuthenticationStateAsync][{Timestamp}] AuthState.User.Identity.Name: {UserName}", DateTime.UtcNow.ToString("HH:mm:ss.fff"), authState.User?.Identity?.Name ?? "null");
            _logger.LogDebug("[GetAuthenticationStateAsync][{Timestamp}] AuthState.User.Claims.Count: {ClaimsCount}", DateTime.UtcNow.ToString("HH:mm:ss.fff"), authState.User?.Claims?.Count() ?? 0);
            
            var result = await CreateAuthenticationState(authState.User);
            _logger.LogInformation("[GetAuthenticationStateAsync][{Timestamp}] Total auth state creation completed", DateTime.UtcNow.ToString("HH:mm:ss.fff"));
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GetAuthenticationStateAsync][{Timestamp}] Error in GetAuthenticationStateAsync: {ErrorMessage}", DateTime.UtcNow.ToString("HH:mm:ss.fff"), ex.Message);
            throw;
        }
    }

    public async Task<ClaimsPrincipal?> GetCurrentUserAsync()
    {
        var authState = _fastAuth != null 
            ? await _fastAuth.GetFastAuthenticationStateAsync()
            : await _authStateProvider.GetAuthenticationStateAsync();
        return authState.User;
    }

    public async Task<bool> IsAuthenticatedAsync()
    {
        var authState = _fastAuth != null 
            ? await _fastAuth.GetFastAuthenticationStateAsync()
            : await _authStateProvider.GetAuthenticationStateAsync();
        return authState.User?.Identity?.IsAuthenticated ?? false;
    }

    public async Task<string?> GetAccessTokenAsync()
    {
        try
        {
            var tokenResult = await _tokenProvider.RequestAccessToken();
            if (tokenResult.TryGetToken(out var token))
            {
                return token.Value;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting access token");
        }
        
        return null;
    }

    public async Task<IEnumerable<Claim>> GetUserClaimsAsync()
    {
        var authState = _fastAuth != null 
            ? await _fastAuth.GetFastAuthenticationStateAsync()
            : await _authStateProvider.GetAuthenticationStateAsync();
        return authState.User?.Claims ?? Enumerable.Empty<Claim>();
    }

    public async Task<string?> GetClaimValueAsync(string claimType)
    {
        var claims = await GetUserClaimsAsync();
        return claims.FirstOrDefault(c => c.Type == claimType)?.Value;
    }

    public async Task<bool> IsInRoleAsync(string role)
    {
        var authState = _fastAuth != null 
            ? await _fastAuth.GetFastAuthenticationStateAsync()
            : await _authStateProvider.GetAuthenticationStateAsync();
        return authState.User?.IsInRole(role) ?? false;
    }

    private async Task<OIDCAuthenticationState> CreateAuthenticationState(ClaimsPrincipal user)
    {
        _logger.LogInformation("[CreateAuthenticationState][{Timestamp}] Creating state for user: {UserName}", DateTime.UtcNow.ToString("HH:mm:ss.fff"), user?.Identity?.Name ?? "anonymous");
        
        var state = new OIDCAuthenticationState
        {
            IsAuthenticated = user?.Identity?.IsAuthenticated ?? false,
            UserName = user?.Identity?.Name ?? 
                      user?.FindFirst("cognito:username")?.Value ?? 
                      user?.FindFirst("preferred_username")?.Value ??
                      user?.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
                      user?.FindFirst("sub")?.Value,
            Email = user?.FindFirst("email")?.Value ?? user?.FindFirst(ClaimTypes.Email)?.Value
        };

        _logger.LogInformation("[CreateAuthenticationState][{Timestamp}] IsAuthenticated: {IsAuthenticated}", DateTime.UtcNow.ToString("HH:mm:ss.fff"), state.IsAuthenticated);

        if (state.IsAuthenticated)
        {
            _logger.LogInformation("[CreateAuthenticationState][{Timestamp}] User authenticated, getting token expiry", DateTime.UtcNow.ToString("HH:mm:ss.fff"));
            
            // Get token expiry
            try
            {
                _logger.LogInformation("[CreateAuthenticationState][{Timestamp}] Requesting access token", DateTime.UtcNow.ToString("HH:mm:ss.fff"));
                var tokenResult = await _tokenProvider.RequestAccessToken();
                _logger.LogInformation("[CreateAuthenticationState][{Timestamp}] Token request completed", DateTime.UtcNow.ToString("HH:mm:ss.fff"));
                
                if (tokenResult.TryGetToken(out var token))
                {
                    state.TokenExpiry = token.Expires.DateTime;
                    _logger.LogInformation("[CreateAuthenticationState][{Timestamp}] Token expires: {TokenExpiry}", DateTime.UtcNow.ToString("HH:mm:ss.fff"), token.Expires.DateTime);
                }
                else
                {
                    _logger.LogInformation("[CreateAuthenticationState][{Timestamp}] No token returned", DateTime.UtcNow.ToString("HH:mm:ss.fff"));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CreateAuthenticationState][{Timestamp}] Error getting token expiry", DateTime.UtcNow.ToString("HH:mm:ss.fff"));
            }

            // Populate claims
            _logger.LogInformation("[CreateAuthenticationState][{Timestamp}] Processing {ClaimsCount} claims", DateTime.UtcNow.ToString("HH:mm:ss.fff"), user!.Claims.Count());
            var claims = new Dictionary<string, string>();
            foreach (var claim in user!.Claims)
            {
                claims[claim.Type] = claim.Value;
            }
            state.Claims = claims;
        }

        _logger.LogInformation("[CreateAuthenticationState][{Timestamp}] State creation completed", DateTime.UtcNow.ToString("HH:mm:ss.fff"));
        return state;
    }

    public Task<bool> LoginAsync()
    {
        try
        {
            _logger.LogInformation("Initiating Blazor WebAssembly login");
            
            // Log the current base URI and where we're redirecting to
            _logger.LogInformation("Base URI: {BaseUri}", _navigation.BaseUri);
            _logger.LogInformation("Navigating to login endpoint: authentication/login");
            
            // Navigate to the authentication/login endpoint
            _navigation.NavigateToLogin("authentication/login");
            
            // Trigger the event in case any subscribers need it
            OnAuthenticationRequested?.Invoke("login");
            
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login");
            return Task.FromResult(false);
        }
    }

    public async Task LogoutAsync()
    {
        try
        {
            _logger.LogInformation("[LogoutAsync][{Timestamp}] Starting logout process", DateTime.UtcNow.ToString("HH:mm:ss.fff"));
            
            // Clear fast auth cache
            if (_fastAuth != null)
            {
                await _fastAuth.InvalidateCacheAsync();
                _logger.LogInformation("[LogoutAsync][{Timestamp}] Fast auth cache cleared", DateTime.UtcNow.ToString("HH:mm:ss.fff"));
            }
            
            // Clear tokens from storage to prevent immediate re-login
            await _rememberMeService.ClearTokensAsync();
            _logger.LogInformation("[LogoutAsync][{Timestamp}] Tokens cleared from storage", DateTime.UtcNow.ToString("HH:mm:ss.fff"));
            
            // Build logout URL to clear Cognito session
            var postLogoutRedirectUri = _navigation.BaseUri;
            _logger.LogInformation("[LogoutAsync][{Timestamp}] PostLogoutRedirectUri: {PostLogoutRedirectUri}", DateTime.UtcNow.ToString("HH:mm:ss.fff"), postLogoutRedirectUri);
            
            var logoutUrl = _configProvider.BuildLogoutUrl(postLogoutRedirectUri);
            
            if (!string.IsNullOrEmpty(logoutUrl))
            {
                _logger.LogInformation("[LogoutAsync][{Timestamp}] Navigating to Cognito logout: {LogoutUrl}", DateTime.UtcNow.ToString("HH:mm:ss.fff"), logoutUrl);
                _logger.LogInformation("[LogoutAsync][{Timestamp}] Navigation BaseUri: {BaseUri}", DateTime.UtcNow.ToString("HH:mm:ss.fff"), _navigation.BaseUri);
                _logger.LogInformation("[LogoutAsync][{Timestamp}] Navigation Uri: {Uri}", DateTime.UtcNow.ToString("HH:mm:ss.fff"), _navigation.Uri);
                
                // Force a full page reload to ensure we hit the Cognito logout endpoint
                _navigation.NavigateTo(logoutUrl, forceLoad: true);
                
                _logger.LogInformation("[LogoutAsync][{Timestamp}] Navigation to Cognito logout URL initiated", DateTime.UtcNow.ToString("HH:mm:ss.fff"));
            }
            else
            {
                _logger.LogWarning("[LogoutAsync][{Timestamp}] Could not build logout URL, falling back to local logout", DateTime.UtcNow.ToString("HH:mm:ss.fff"));
                _logger.LogWarning("[LogoutAsync][{Timestamp}] Provider type: {ProviderType}", DateTime.UtcNow.ToString("HH:mm:ss.fff"), _configProvider.GetProviderType());
                _logger.LogWarning("[LogoutAsync][{Timestamp}] Logout endpoint: {LogoutEndpoint}", DateTime.UtcNow.ToString("HH:mm:ss.fff"), _configProvider.GetLogoutEndpoint());
                _logger.LogWarning("[LogoutAsync][{Timestamp}] Client ID: {ClientId}", DateTime.UtcNow.ToString("HH:mm:ss.fff"), _configProvider.GetClientId());
                
                OnAuthenticationRequested?.Invoke("logout");
            }
            
            _logger.LogInformation("[LogoutAsync][{Timestamp}] Logout process completed", DateTime.UtcNow.ToString("HH:mm:ss.fff"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[LogoutAsync][{Timestamp}] Error during logout", DateTime.UtcNow.ToString("HH:mm:ss.fff"));
        }
    }

    public void Dispose()
    {
        _authStateProvider.AuthenticationStateChanged -= OnAuthenticationStateChanged;
    }
}