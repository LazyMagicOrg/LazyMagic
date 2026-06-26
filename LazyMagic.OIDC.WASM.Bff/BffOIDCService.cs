namespace LazyMagic.OIDC.WASM.Bff;

/// <summary>
/// Thin, provider-agnostic <see cref="IOIDCService"/> implementation for BFF mode. The UI binds
/// to this abstraction exactly as it does for the SPA-token providers, but here:
///   - <see cref="LoginAsync"/> navigates (forceLoad) to <c>/bff/login?returnUrl=...</c>; the BFF
///     issues a full-page 302 to the IdP.
///   - <see cref="LogoutAsync"/> navigates (forceLoad) to <c>/bff/logout</c>; the BFF clears the
///     session and ends the IdP session.
///   - <see cref="GetAuthenticationStateAsync"/> delegates to the <see cref="BffAuthenticationStateProvider"/>.
///   - <see cref="GetAccessTokenAsync"/> returns <c>null</c> — there is NO token in the SPA in BFF mode.
///
/// Login/logout are browser navigations (cookies ride top-level navigations), so there is no
/// token machinery, no silent-renew iframe, and no client-side storage.
/// </summary>
public sealed class BffOIDCService : IOIDCService, IDisposable
{
    // HOST-ABSOLUTE (leading slash): the /bff/* endpoints live at the host ROOT, but the
    // WASM may be mounted under a sub-path (e.g. <base href="/store/">). A base-relative
    // "bff/login" would resolve to "/store/bff/login" (NavigateTo against the base) or
    // "/store/bff/login" (HttpClient against a /store/ BaseAddress) — i.e. the app itself,
    // not the BFF. The leading slash pins them to the host root regardless of the mount.
    private const string LoginPath = "/bff/login";
    private const string LogoutPath = "/bff/logout";

    private readonly AuthenticationStateProvider _authStateProvider;
    private readonly NavigationManager _navigation;
    private readonly HttpClient _http;
    private readonly ILogger<BffOIDCService> _logger;

    public event EventHandler<OIDCAuthenticationStateChangedEventArgs>? AuthenticationStateChanged;
    public event Action<string>? OnAuthenticationRequested;

    public BffOIDCService(
        AuthenticationStateProvider authStateProvider,
        NavigationManager navigation,
        HttpClient http,
        ILogger<BffOIDCService> logger)
    {
        _authStateProvider = authStateProvider;
        _navigation = navigation;
        _http = http;
        _logger = logger;

        _authStateProvider.AuthenticationStateChanged += OnProviderStateChanged;
    }

    private async void OnProviderStateChanged(Task<AuthenticationState> task)
    {
        try
        {
            var authState = await task.ConfigureAwait(false);
            var state = ToOidcState(authState.User);
            AuthenticationStateChanged?.Invoke(this, new OIDCAuthenticationStateChangedEventArgs(state));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[BFF] Error handling provider auth state change");
        }
    }

    public async Task<OIDCAuthenticationState> GetAuthenticationStateAsync()
    {
        var authState = await _authStateProvider.GetAuthenticationStateAsync().ConfigureAwait(false);
        return ToOidcState(authState.User);
    }

    public async Task<ClaimsPrincipal?> GetCurrentUserAsync()
    {
        var authState = await _authStateProvider.GetAuthenticationStateAsync().ConfigureAwait(false);
        return authState.User;
    }

    public async Task<bool> IsAuthenticatedAsync()
    {
        var authState = await _authStateProvider.GetAuthenticationStateAsync().ConfigureAwait(false);
        return authState.User?.Identity?.IsAuthenticated ?? false;
    }

    /// <summary>No token in the SPA in BFF mode — the cookie carries the session.</summary>
    public Task<string?> GetAccessTokenAsync() => Task.FromResult<string?>(null);

    public async Task<IEnumerable<Claim>> GetUserClaimsAsync()
    {
        var authState = await _authStateProvider.GetAuthenticationStateAsync().ConfigureAwait(false);
        return authState.User?.Claims ?? Enumerable.Empty<Claim>();
    }

    public async Task<string?> GetClaimValueAsync(string claimType)
    {
        var claims = await GetUserClaimsAsync().ConfigureAwait(false);
        return claims.FirstOrDefault(c => c.Type == claimType)?.Value;
    }

    public async Task<bool> IsInRoleAsync(string role)
    {
        var authState = await _authStateProvider.GetAuthenticationStateAsync().ConfigureAwait(false);
        return authState.User?.IsInRole(role) ?? false;
    }

    public Task<bool> LoginAsync()
    {
        try
        {
            // returnUrl = the app-relative path the user is on, so the BFF returns them here.
            var returnUrl = GetReturnUrl();
            var loginUrl = $"{LoginPath}?returnUrl={Uri.EscapeDataString(returnUrl)}";
            _logger.LogInformation("[BFF] Navigating to login: {LoginUrl}", loginUrl);

            OnAuthenticationRequested?.Invoke("login");

            // Full-page navigation: the BFF responds with a 302 to the IdP, and the
            // SameSite=Lax cookie is sent on the subsequent top-level navigation back.
            _navigation.NavigateTo(loginUrl, forceLoad: true);
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[BFF] Error during login navigation");
            return Task.FromResult(false);
        }
    }

    public async Task LogoutAsync()
    {
        OnAuthenticationRequested?.Invoke("logout");

        var returnUrl = GetReturnUrl();
        var logoutEndpoint = $"{LogoutPath}?returnUrl={Uri.EscapeDataString(returnUrl)}";

        try
        {
            // Credentialed POST with the X-CSRF marker. A top-level GET navigation cannot carry
            // the non-safelisted header, so the destructive logout is CSRF-protected server-side.
            // The BFF clears the session + cookie and returns the provider logout URL to navigate to.
            using var request = new HttpRequestMessage(HttpMethod.Post, logoutEndpoint);
            request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
            request.Headers.Add("X-CSRF", "1");

            using var response = await _http.SendAsync(request).ConfigureAwait(false);

            string? providerLogoutUrl = null;
            if (response.IsSuccessStatusCode)
            {
                try
                {
                    var body = await response.Content.ReadFromJsonAsync<LogoutResponse>().ConfigureAwait(false);
                    providerLogoutUrl = body?.LogoutUrl;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[BFF] Could not parse logout response; falling back to home.");
                }
            }
            else
            {
                _logger.LogWarning("[BFF] /bff/logout returned {Status}; falling back to home.", response.StatusCode);
            }

            // Navigate (full page) to the provider logout URL, or home as a fallback.
            _navigation.NavigateTo(string.IsNullOrWhiteSpace(providerLogoutUrl) ? "/" : providerLogoutUrl!, forceLoad: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[BFF] Error during logout; navigating home.");
            _navigation.NavigateTo("/", forceLoad: true);
        }
    }

    private sealed class LogoutResponse
    {
        public string? LogoutUrl { get; set; }
    }

    /// <summary>
    /// The HOST-ABSOLUTE path (incl. any app base like <c>/store/</c>) of the current page,
    /// used as the BFF <c>returnUrl</c> so the callback returns the user to where they were.
    /// ToBaseRelativePath would STRIP the <c>/store/</c> base and yield <c>"/"</c>, which the
    /// BFF callback would honor by redirecting to the SITE ROOT instead of back into the app.
    /// </summary>
    private string GetReturnUrl() => new Uri(_navigation.Uri).PathAndQuery;

    private static OIDCAuthenticationState ToOidcState(ClaimsPrincipal? user)
    {
        var state = new OIDCAuthenticationState
        {
            IsAuthenticated = user?.Identity?.IsAuthenticated ?? false,
            UserName = user?.Identity?.Name
                       ?? user?.FindFirst("cognito:username")?.Value
                       ?? user?.FindFirst("preferred_username")?.Value
                       ?? user?.FindFirst("sub")?.Value,
            Email = user?.FindFirst("email")?.Value ?? user?.FindFirst(ClaimTypes.Email)?.Value
        };

        if (state.IsAuthenticated && user != null)
        {
            var claims = new Dictionary<string, string>();
            foreach (var claim in user.Claims)
                claims[claim.Type] = claim.Value; // last write wins; fine for display purposes
            state.Claims = claims;
        }

        return state;
    }

    public void Dispose()
    {
        _authStateProvider.AuthenticationStateChanged -= OnProviderStateChanged;
    }
}
