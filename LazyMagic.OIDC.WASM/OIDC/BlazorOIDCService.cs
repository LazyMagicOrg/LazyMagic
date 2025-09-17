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

    public event EventHandler<OIDCAuthenticationStateChangedEventArgs>? AuthenticationStateChanged;
    public event Action<string>? OnAuthenticationRequested;

    public BlazorOIDCService(
        AuthenticationStateProvider authStateProvider,
        IAccessTokenProvider tokenProvider,
        IOidcConfig oidcConfig,
        NavigationManager navigation,
        ILogger<BlazorOIDCService> logger,
        IRememberMeService rememberMeService,
        IDynamicConfigurationProvider configProvider)
    {
        _authStateProvider = authStateProvider;
        _tokenProvider = tokenProvider;
        _oidcConfig = oidcConfig;
        _navigation = navigation;
        _logger = logger;
        _rememberMeService = rememberMeService;
        _configProvider = configProvider;

        // Subscribe to authentication state changes
        _authStateProvider.AuthenticationStateChanged += OnAuthenticationStateChanged;
    }

    private async void OnAuthenticationStateChanged(Task<Microsoft.AspNetCore.Components.Authorization.AuthenticationState> task)
    {
        try
        {
            var authState = await task;
            var state = await CreateAuthenticationState(authState.User);
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
            _logger.LogInformation($"[{DateTime.UtcNow:HH:mm:ss.fff}] Starting _authStateProvider.GetAuthenticationStateAsync()");
            _logger.LogInformation($"[{DateTime.UtcNow:HH:mm:ss.fff}] AuthStateProvider type: {_authStateProvider.GetType().Name}");
            
            var authState = await _authStateProvider.GetAuthenticationStateAsync();
            
            _logger.LogInformation($"[{DateTime.UtcNow:HH:mm:ss.fff}] Completed _authStateProvider.GetAuthenticationStateAsync()");
            _logger.LogInformation($"[{DateTime.UtcNow:HH:mm:ss.fff}] AuthState.User.Identity.IsAuthenticated: {authState.User?.Identity?.IsAuthenticated}");
            _logger.LogInformation($"[{DateTime.UtcNow:HH:mm:ss.fff}] AuthState.User.Identity.Name: {authState.User?.Identity?.Name ?? "null"}");
            _logger.LogInformation($"[{DateTime.UtcNow:HH:mm:ss.fff}] AuthState.User.Claims.Count: {authState.User?.Claims?.Count() ?? 0}");
            
            _logger.LogInformation($"[{DateTime.UtcNow:HH:mm:ss.fff}] Starting CreateAuthenticationState()");
            var result = await CreateAuthenticationState(authState.User);
            _logger.LogInformation($"[{DateTime.UtcNow:HH:mm:ss.fff}] Completed CreateAuthenticationState()");
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"[{DateTime.UtcNow:HH:mm:ss.fff}] Error in GetAuthenticationStateAsync: {ex.Message}");
            throw;
        }
    }

    public async Task<ClaimsPrincipal?> GetCurrentUserAsync()
    {
        var authState = await _authStateProvider.GetAuthenticationStateAsync();
        return authState.User;
    }

    public async Task<bool> IsAuthenticatedAsync()
    {
        var authState = await _authStateProvider.GetAuthenticationStateAsync();
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
        var authState = await _authStateProvider.GetAuthenticationStateAsync();
        return authState.User?.Claims ?? Enumerable.Empty<Claim>();
    }

    public async Task<string?> GetClaimValueAsync(string claimType)
    {
        var claims = await GetUserClaimsAsync();
        return claims.FirstOrDefault(c => c.Type == claimType)?.Value;
    }

    public async Task<bool> IsInRoleAsync(string role)
    {
        var authState = await _authStateProvider.GetAuthenticationStateAsync();
        return authState.User?.IsInRole(role) ?? false;
    }

    private async Task<OIDCAuthenticationState> CreateAuthenticationState(ClaimsPrincipal user)
    {
        _logger.LogInformation($"[{DateTime.UtcNow:HH:mm:ss.fff}] CreateAuthenticationState - Creating state for user: {user?.Identity?.Name ?? "anonymous"}");
        
        var state = new OIDCAuthenticationState
        {
            IsAuthenticated = user?.Identity?.IsAuthenticated ?? false,
            UserName = user?.Identity?.Name,
            Email = user?.FindFirst("email")?.Value ?? user?.FindFirst(ClaimTypes.Email)?.Value
        };

        _logger.LogInformation($"[{DateTime.UtcNow:HH:mm:ss.fff}] CreateAuthenticationState - IsAuthenticated: {state.IsAuthenticated}");

        if (state.IsAuthenticated)
        {
            _logger.LogInformation($"[{DateTime.UtcNow:HH:mm:ss.fff}] CreateAuthenticationState - User authenticated, getting token expiry");
            
            // Get token expiry
            try
            {
                _logger.LogInformation($"[{DateTime.UtcNow:HH:mm:ss.fff}] CreateAuthenticationState - Requesting access token");
                var tokenResult = await _tokenProvider.RequestAccessToken();
                _logger.LogInformation($"[{DateTime.UtcNow:HH:mm:ss.fff}] CreateAuthenticationState - Token request completed");
                
                if (tokenResult.TryGetToken(out var token))
                {
                    state.TokenExpiry = token.Expires.DateTime;
                    _logger.LogInformation($"[{DateTime.UtcNow:HH:mm:ss.fff}] CreateAuthenticationState - Token expires: {token.Expires.DateTime}");
                }
                else
                {
                    _logger.LogInformation($"[{DateTime.UtcNow:HH:mm:ss.fff}] CreateAuthenticationState - No token returned");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[{DateTime.UtcNow:HH:mm:ss.fff}] Error getting token expiry");
            }

            // Populate claims
            _logger.LogInformation($"[{DateTime.UtcNow:HH:mm:ss.fff}] CreateAuthenticationState - Processing {user!.Claims.Count()} claims");
            var claims = new Dictionary<string, string>();
            foreach (var claim in user!.Claims)
            {
                claims[claim.Type] = claim.Value;
            }
            state.Claims = claims;
        }

        _logger.LogInformation($"[{DateTime.UtcNow:HH:mm:ss.fff}] CreateAuthenticationState - State creation completed");
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
            _logger.LogInformation("Starting logout process");
            
            // Clear tokens from storage to prevent immediate re-login
            await _rememberMeService.ClearTokensAsync();
            _logger.LogInformation("Tokens cleared from storage");
            
            // Build logout URL to clear Cognito session
            var postLogoutRedirectUri = _navigation.BaseUri;
            _logger.LogInformation("PostLogoutRedirectUri: {PostLogoutRedirectUri}", postLogoutRedirectUri);
            
            var logoutUrl = _configProvider.BuildLogoutUrl(postLogoutRedirectUri);
            
            if (!string.IsNullOrEmpty(logoutUrl))
            {
                _logger.LogInformation("Navigating to Cognito logout: {LogoutUrl}", logoutUrl);
                _logger.LogInformation("Navigation BaseUri: {BaseUri}", _navigation.BaseUri);
                _logger.LogInformation("Navigation Uri: {Uri}", _navigation.Uri);
                
                // Force a full page reload to ensure we hit the Cognito logout endpoint
                _navigation.NavigateTo(logoutUrl, forceLoad: true);
                
                _logger.LogInformation("Navigation to Cognito logout URL initiated");
            }
            else
            {
                _logger.LogWarning("Could not build logout URL, falling back to local logout");
                _logger.LogWarning("Provider type: {ProviderType}", _configProvider.GetProviderType());
                _logger.LogWarning("Logout endpoint: {LogoutEndpoint}", _configProvider.GetLogoutEndpoint());
                _logger.LogWarning("Client ID: {ClientId}", _configProvider.GetClientId());
                
                OnAuthenticationRequested?.Invoke("logout");
            }
            
            _logger.LogInformation("Logout process completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during logout");
        }
    }

    public void Dispose()
    {
        _authStateProvider.AuthenticationStateChanged -= OnAuthenticationStateChanged;
    }
}