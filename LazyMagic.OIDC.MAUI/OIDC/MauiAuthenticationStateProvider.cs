using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;

namespace LazyMagic.OIDC.MAUI;

public class MauiAuthenticationStateProvider : AuthenticationStateProvider, IDisposable
{
    private readonly IOIDCService _oidcService;
    private readonly ILogger<MauiAuthenticationStateProvider> _logger;
    private AuthenticationState _authenticationState;

    public MauiAuthenticationStateProvider(IOIDCService oidcService, ILogger<MauiAuthenticationStateProvider> logger)
    {
        _oidcService = oidcService;
        _logger = logger;
        _authenticationState = new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));

        _logger.LogInformation("=== MAUI AUTHENTICATION STATE PROVIDER INITIALIZED ===");

        // Subscribe to authentication state changes
        _oidcService.AuthenticationStateChanged += OnAuthenticationStateChanged;
        
        // Initialize with current state after OIDC service startup
        _ = Task.Run(async () =>
        {
            // Wait for OIDC service to complete startup by checking multiple times
            for (int i = 0; i < 10; i++)
            {
                await Task.Delay(50);
                var isInitialized = await _oidcService.IsAuthenticatedAsync();
                _logger.LogInformation("[AuthStateProvider] Startup check {Attempt}: IsAuthenticated = {IsAuth}", i + 1, isInitialized);
                
                // If we got a definitive answer (true or tokens are checked), proceed
                await UpdateAuthenticationStateAsync();
                
                // If the state changed from our initial unauthenticated state, we're done
                if (_authenticationState.User.Identity?.IsAuthenticated == true)
                {
                    _logger.LogInformation("[AuthStateProvider] Authentication state initialized successfully");
                    break;
                }
            }
        });
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        await UpdateAuthenticationStateAsync();
        return _authenticationState;
    }

    private async void OnAuthenticationStateChanged(object? sender, OIDCAuthenticationStateChangedEventArgs e)
    {
        _logger.LogInformation("[AuthStateProvider] 🔥 AuthenticationStateChanged event received! IsAuthenticated: {IsAuth}, UserName: {UserName}",
            e.NewState?.IsAuthenticated ?? false,
            e.NewState?.UserName ?? "null");
        
        await UpdateAuthenticationStateAsync();
        NotifyAuthenticationStateChanged(Task.FromResult(_authenticationState));
        
        _logger.LogInformation("[AuthStateProvider] 🔥 Blazor AuthenticationStateChanged notification sent");
    }

    private async Task UpdateAuthenticationStateAsync()
    {
        try
        {
            _logger.LogInformation("[AuthStateProvider] Updating authentication state...");
            var isAuthenticated = await _oidcService.IsAuthenticatedAsync();
            _logger.LogInformation("[AuthStateProvider] IsAuthenticated result: {IsAuth}", isAuthenticated);
            
            if (isAuthenticated)
            {
                var user = await _oidcService.GetCurrentUserAsync();
                if (user != null)
                {
                    _authenticationState = new AuthenticationState(user);
                    _logger.LogInformation("[AuthStateProvider] ✅ Set authenticated state with user: {UserName}", user.Identity?.Name);
                }
                else
                {
                    // Create a basic authenticated user if we can't get the full claims
                    var claims = await _oidcService.GetUserClaimsAsync();
                    var identity = new ClaimsIdentity(claims, "OIDC");
                    _authenticationState = new AuthenticationState(new ClaimsPrincipal(identity));
                    _logger.LogInformation("[AuthStateProvider] ✅ Set authenticated state with claims count: {ClaimCount}", claims.Count());
                }
            }
            else
            {
                _authenticationState = new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
                _logger.LogInformation("[AuthStateProvider] ❌ Set unauthenticated state");
            }
        }
        catch (Exception ex)
        {
            // If there's an error, assume not authenticated
            _authenticationState = new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
            _logger.LogError("[AuthStateProvider] Error updating auth state: {Error}", ex.Message);
        }
    }

    public void Dispose()
    {
        if (_oidcService != null)
        {
            _oidcService.AuthenticationStateChanged -= OnAuthenticationStateChanged;
        }
    }
}