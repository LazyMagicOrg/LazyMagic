namespace LazyMagic.OIDC.WASM;

/// <summary>
/// Service to manage Remember Me functionality for token persistence.
/// For providers with native remember-me support (e.g., Keycloak), most operations become no-ops
/// since the provider handles session persistence.
/// </summary>
public class BlazorRememberMeService : IRememberMeService
{
    private readonly IJSRuntime _jsRuntime;
    private readonly ILogger<BlazorRememberMeService> _logger;
    private readonly IDynamicConfigurationProvider _configProvider;
    private const string REMEMBER_ME_KEY = "rememberMe";
    private const string REMEMBER_ME_PREFERENCE_KEY = "rememberMePreference";

    public BlazorRememberMeService(
        IJSRuntime jsRuntime, 
        ILogger<BlazorRememberMeService> logger,
        IDynamicConfigurationProvider configProvider)
    {
        _jsRuntime = jsRuntime;
        _logger = logger;
        _configProvider = configProvider;
    }

    /// <summary>
    /// Checks if client-side token storage is required for the current provider.
    /// </summary>
    private bool RequiresClientSideStorage()
    {
        return _configProvider.RequiresClientSideTokenStorage();
    }

    /// <summary>
    /// Gets the current Remember Me setting.
    /// For providers with native remember-me, always returns false (not managed client-side).
    /// </summary>
    public async Task<bool> GetRememberMeAsync()
    {
        // For providers with native remember-me, we don't manage this client-side
        if (!RequiresClientSideStorage())
        {
            _logger.LogDebug("[GetRememberMeAsync] Provider has native remember-me, returning false");
            return false;
        }

        try
        {
            var value = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", REMEMBER_ME_KEY);
            return value == "true";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting Remember Me setting");
            return false;
        }
    }

    /// <summary>
    /// Sets the Remember Me setting and manages token storage accordingly.
    /// For providers with native remember-me, this is a no-op.
    /// </summary>
    public async Task SetRememberMeAsync(bool rememberMe)
    {
        // For providers with native remember-me, this is a no-op
        if (!RequiresClientSideStorage())
        {
            _logger.LogInformation("[SetRememberMeAsync] Provider has native remember-me, skipping client-side storage management");
            return;
        }

        try
        {
            if (rememberMe)
            {
                // Store preference and move ONLY user tokens to localStorage
                // Let Blazor manage its own configuration in sessionStorage
                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", REMEMBER_ME_KEY, "true");
                await MoveUserTokensToLocalStorage();
                _logger.LogInformation("RememberMe enabled - user tokens moved to localStorage");
            }
            else
            {
                // Clear preference and move user tokens to sessionStorage
                await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", REMEMBER_ME_KEY);
                await MoveUserTokensToSessionStorage();
                _logger.LogInformation("RememberMe disabled - user tokens moved to sessionStorage");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting Remember Me");
        }
    }

    /// <summary>
    /// Clears all authentication tokens based on Remember Me setting.
    /// For providers with native remember-me, still clears any cached tokens but doesn't affect provider session.
    /// </summary>
    public async Task ClearTokensAsync()
    {
        // For providers with native remember-me, we still clear local cache
        // but the actual session is managed by the provider
        if (!RequiresClientSideStorage())
        {
            _logger.LogInformation("[ClearTokensAsync] Provider has native remember-me, clearing local cache only");
            // Still clear any locally cached tokens for security
            await ClearLocalCacheAsync();
            return;
        }

        try
        {
            _logger.LogInformation("Starting to clear OIDC tokens from storage");
            
            // Clear only OIDC-related tokens from localStorage
            var localOidcKeys = await _jsRuntime.InvokeAsync<string[]>("eval", 
                "Object.keys(localStorage).filter(k => k.startsWith('oidc.') || k.includes('Authentication'))");
            foreach (var key in localOidcKeys)
            {
                await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", key);
                _logger.LogInformation($"Removed localStorage key: {key}");
            }
            
            // Clear only OIDC-related tokens from sessionStorage
            var sessionOidcKeys = await _jsRuntime.InvokeAsync<string[]>("eval", 
                "Object.keys(sessionStorage).filter(k => k.startsWith('oidc.') || k.includes('Authentication'))");
            foreach (var key in sessionOidcKeys)
            {
                await _jsRuntime.InvokeVoidAsync("sessionStorage.removeItem", key);
                _logger.LogInformation($"Removed sessionStorage key: {key}");
            }
            
            // Also clear the RememberMe preference
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", REMEMBER_ME_KEY);
            
            _logger.LogInformation($"Cleared {localOidcKeys.Length} localStorage and {sessionOidcKeys.Length} sessionStorage OIDC tokens");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing tokens");
        }
    }

    /// <summary>
    /// Moves OIDC tokens and auth data from sessionStorage to localStorage
    /// </summary>
    private async Task MoveTokensToLocalStorage()
    {
        try
        {
            // Get all OIDC and authentication-related keys from sessionStorage
            var keys = await _jsRuntime.InvokeAsync<string[]>("eval", 
                "Object.keys(sessionStorage).filter(k => k.startsWith('oidc.') || k.includes('Authentication'))");

            foreach (var key in keys)
            {
                var value = await _jsRuntime.InvokeAsync<string>("sessionStorage.getItem", key);
                if (!string.IsNullOrEmpty(value))
                {
                    await _jsRuntime.InvokeVoidAsync("localStorage.setItem", key, value);
                    await _jsRuntime.InvokeVoidAsync("sessionStorage.removeItem", key);
                }
            }

            _logger.LogInformation("Moved tokens to localStorage (Remember Me enabled)");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error moving tokens to localStorage");
        }
    }

    /// <summary>
    /// Moves OIDC tokens and auth data from localStorage to sessionStorage
    /// </summary>
    private async Task MoveTokensToSessionStorage()
    {
        try
        {
            // Get all OIDC and authentication-related keys from localStorage
            var keys = await _jsRuntime.InvokeAsync<string[]>("eval", 
                "Object.keys(localStorage).filter(k => k.startsWith('oidc.') || k.includes('Authentication'))");

            foreach (var key in keys)
            {
                var value = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", key);
                if (!string.IsNullOrEmpty(value))
                {
                    await _jsRuntime.InvokeVoidAsync("sessionStorage.setItem", key, value);
                    await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", key);
                }
            }

            _logger.LogInformation("Moved tokens to sessionStorage (Remember Me disabled)");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error moving tokens to sessionStorage");
        }
    }

    /// <summary>
    /// Checks if there are any OIDC tokens in storage
    /// </summary>
    public async Task<bool> HasTokensAsync()
    {
        try
        {
            // Add a check to ensure JS runtime is available
            await _jsRuntime.InvokeVoidAsync("eval", "void 0");
            
            // Check for actual token values, not just key presence
            var localTokens = await _jsRuntime.InvokeAsync<string[]>("eval", 
                "Object.keys(localStorage).filter(k => k.startsWith('oidc.') || k.includes('Authentication')).filter(k => localStorage.getItem(k) && localStorage.getItem(k).trim() !== '')");
            var sessionTokens = await _jsRuntime.InvokeAsync<string[]>("eval", 
                "Object.keys(sessionStorage).filter(k => k.startsWith('oidc.') || k.includes('Authentication')).filter(k => sessionStorage.getItem(k) && sessionStorage.getItem(k).trim() !== '')");
            
            _logger.LogInformation($"HasTokensAsync - LocalStorage tokens: {localTokens.Length}, SessionStorage tokens: {sessionTokens.Length}");
            if (localTokens.Length > 0)
            {
                _logger.LogInformation($"LocalStorage token keys: [{string.Join(", ", localTokens)}]");
            }
            if (sessionTokens.Length > 0)
            {
                _logger.LogInformation($"SessionStorage token keys: [{string.Join(", ", sessionTokens)}]");
            }
            
            return localTokens.Length > 0 || sessionTokens.Length > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking for tokens - JS runtime may not be ready");
            return false;
        }
    }

    /// <summary>
    /// Initializes authentication on app startup based on RememberMe setting.
    /// For providers with native remember-me, this is a no-op.
    /// Ensures tokens are available to Blazor while maintaining persistence.
    /// </summary>
    public async Task InitializeAuthenticationAsync()
    {
        var startTime = DateTime.UtcNow;
        try
        {
            _logger.LogInformation($"[{DateTime.UtcNow:HH:mm:ss.fff}] InitializeAuthenticationAsync started");
            
            // Check if we're in an OAuth callback FIRST - if so, don't clear anything
            bool isOAuthCallback = false;
            try
            {
                var currentUrl = await _jsRuntime.InvokeAsync<string>("eval", "window.location.href");
                isOAuthCallback = currentUrl.Contains("/authentication/login-callback") || 
                                 currentUrl.Contains("code=") ||
                                 currentUrl.Contains("state=");
                if (isOAuthCallback)
                {
                    _logger.LogInformation($"[{DateTime.UtcNow:HH:mm:ss.fff}] OAuth callback detected, skipping cache clearing to allow login completion");
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not check URL for OAuth callback");
            }
            
            // For providers with native remember-me, skip client-side token management
            // Only clear stale cache when NOT in OAuth callback
            if (!RequiresClientSideStorage())
            {
                _logger.LogInformation($"[{DateTime.UtcNow:HH:mm:ss.fff}] Provider has native remember-me, skipping client-side token management");
                // Don't clear OIDC data here - it breaks the login flow
                // The OIDC data will be managed by Microsoft's OIDC library
                return;
            }
            
            var rememberMe = await GetRememberMeAsync();
            _logger.LogInformation($"[{DateTime.UtcNow:HH:mm:ss.fff}] GetRememberMeAsync returned: {rememberMe}");
            
            // Check for stale tokens and clean them up (but not during OAuth callback)
            // Note: isOAuthCallback was already checked at the start of this method
            var hasTokens = await HasTokensAsync();
            if (hasTokens && !rememberMe)
            {
                // We have leftover tokens but RememberMe is disabled and we're not in an OAuth flow - clean them up
                _logger.LogInformation($"[{DateTime.UtcNow:HH:mm:ss.fff}] Found stale tokens with RememberMe disabled, cleaning up");
                await ClearTokensAsync();
            }
            else if (rememberMe)
            {
                // If RememberMe is enabled, copy tokens from localStorage to sessionStorage
                // so Blazor can find them, but keep originals in localStorage for persistence
                _logger.LogInformation($"[{DateTime.UtcNow:HH:mm:ss.fff}] RememberMe enabled - copying tokens from localStorage to sessionStorage");
                await CopyUserTokensFromLocalToSessionStorage();
            }
            
            var elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;
            _logger.LogInformation($"[{DateTime.UtcNow:HH:mm:ss.fff}] Authentication initialization completed in {elapsed:0}ms");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"[{DateTime.UtcNow:HH:mm:ss.fff}] Error initializing authentication");
        }
    }

    /// <summary>
    /// Clears any locally cached authentication data (fast auth cache, OIDC tokens, etc.)
    /// Used for providers with native remember-me when we still need to clear local cache on logout.
    /// For Keycloak and similar providers, we must clear sessionStorage OIDC data so the app
    /// doesn't think the user is still logged in after the provider session is cleared.
    /// </summary>
    private async Task ClearLocalCacheAsync()
    {
        try
        {
            _logger.LogInformation("[ClearLocalCacheAsync] Clearing local authentication cache");
            
            // Clear fast auth cache AND OIDC user data from both storages
            // This is critical for providers like Keycloak where the server session is cleared
            // but the browser still has cached OIDC data
            await _jsRuntime.InvokeVoidAsync("eval", @"
                try {
                    // Clear fast auth cache
                    localStorage.removeItem('lz-fast-auth-cache');
                    sessionStorage.removeItem('lz-fast-auth-cache');
                    
                    // Clear OIDC user data from sessionStorage (where Microsoft's OIDC stores it)
                    Object.keys(sessionStorage).filter(k => k.startsWith('oidc.user:') || k.startsWith('oidc.')).forEach(k => {
                        sessionStorage.removeItem(k);
                    });
                    
                    // Clear OIDC user data from localStorage (in case it was stored there)
                    Object.keys(localStorage).filter(k => k.startsWith('oidc.user:') || k.startsWith('oidc.')).forEach(k => {
                        localStorage.removeItem(k);
                    });
                    
                    console.log('[ClearLocalCacheAsync] Cleared all OIDC and fast auth cache');
                } catch(e) {
                    console.error('[ClearLocalCacheAsync] Error:', e);
                }
            ");
            
            _logger.LogInformation("[ClearLocalCacheAsync] Local cache cleared (including OIDC user data)");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ClearLocalCacheAsync] Error clearing local cache");
        }
    }

    /// <summary>
    /// Clears any stale OIDC tokens from localStorage that may have been stored
    /// by a previous provider configuration (e.g., when switching from Cognito to Keycloak).
    /// Only clears tokens from localStorage, not sessionStorage (which Microsoft's OIDC uses).
    /// </summary>
    private async Task ClearStaleOidcTokensAsync()
    {
        try
        {
            _logger.LogInformation("[ClearStaleOidcTokensAsync] Clearing stale OIDC tokens from localStorage");
            
            // Clear only localStorage OIDC tokens (not sessionStorage - that's managed by Microsoft's OIDC)
            await _jsRuntime.InvokeVoidAsync("eval", @"
                try {
                    // Clear oidc.user tokens and rememberMe preference from localStorage
                    Object.keys(localStorage).filter(k => k.startsWith('oidc.user:') || k === 'rememberMe').forEach(k => {
                        localStorage.removeItem(k);
                    });
                } catch(e) {}
            ");
            
            _logger.LogInformation("[ClearStaleOidcTokensAsync] Stale OIDC tokens cleared from localStorage");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ClearStaleOidcTokensAsync] Error clearing stale OIDC tokens");
        }
    }

    /// <summary>
    /// Consolidates all auth data to localStorage and removes duplicates from sessionStorage
    /// </summary>
    private async Task ConsolidateAuthDataToLocalStorage()
    {
        try
        {
            // Get all authentication keys from sessionStorage
            var sessionKeys = await _jsRuntime.InvokeAsync<string[]>("eval", 
                "Object.keys(sessionStorage).filter(k => k.startsWith('oidc.') || k.includes('Authentication'))");

            foreach (var key in sessionKeys)
            {
                var value = await _jsRuntime.InvokeAsync<string>("sessionStorage.getItem", key);
                if (!string.IsNullOrEmpty(value))
                {
                    // Set in localStorage (overwrite if exists)
                    await _jsRuntime.InvokeVoidAsync("localStorage.setItem", key, value);
                    // Remove from sessionStorage to avoid duplicates
                    await _jsRuntime.InvokeVoidAsync("sessionStorage.removeItem", key);
                    _logger.LogInformation($"Moved auth data key '{key}' from sessionStorage to localStorage");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error consolidating auth data to localStorage");
        }
    }

    /// <summary>
    /// Consolidates all auth data to sessionStorage and removes from localStorage
    /// </summary>
    private async Task ConsolidateAuthDataToSessionStorage()
    {
        try
        {
            // Get all authentication keys from localStorage (except rememberMe preference)
            var localKeys = await _jsRuntime.InvokeAsync<string[]>("eval", 
                "Object.keys(localStorage).filter(k => (k.startsWith('oidc.') || k.includes('Authentication')) && k !== 'rememberMe')");

            foreach (var key in localKeys)
            {
                var value = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", key);
                if (!string.IsNullOrEmpty(value))
                {
                    // Set in sessionStorage (overwrite if exists)
                    await _jsRuntime.InvokeVoidAsync("sessionStorage.setItem", key, value);
                    // Remove from localStorage
                    await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", key);
                    _logger.LogInformation($"Moved auth data key '{key}' from localStorage to sessionStorage");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error consolidating auth data to sessionStorage");
        }
    }

    /// <summary>
    /// Checks if there are user tokens (not configuration) in storage
    /// </summary>
    private async Task<bool> HasUserTokensAsync()
    {
        try
        {
            var localUserTokens = await _jsRuntime.InvokeAsync<int>("eval", 
                "Object.keys(localStorage).filter(k => k.startsWith('oidc.user:')).length");
            var sessionUserTokens = await _jsRuntime.InvokeAsync<int>("eval", 
                "Object.keys(sessionStorage).filter(k => k.startsWith('oidc.user:')).length");
            
            _logger.LogDebug($"HasUserTokensAsync - LocalStorage: {localUserTokens}, SessionStorage: {sessionUserTokens}");
            return localUserTokens > 0 || sessionUserTokens > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking for user tokens");
            return false;
        }
    }

    /// <summary>
    /// Moves only user tokens from sessionStorage to localStorage
    /// </summary>
    private async Task MoveUserTokensToLocalStorage()
    {
        try
        {
            var userTokenKeys = await _jsRuntime.InvokeAsync<string[]>("eval", 
                "Object.keys(sessionStorage).filter(k => k.startsWith('oidc.user:'))");

            foreach (var key in userTokenKeys)
            {
                var value = await _jsRuntime.InvokeAsync<string>("sessionStorage.getItem", key);
                if (!string.IsNullOrEmpty(value))
                {
                    await _jsRuntime.InvokeVoidAsync("localStorage.setItem", key, value);
                    await _jsRuntime.InvokeVoidAsync("sessionStorage.removeItem", key);
                    _logger.LogInformation($"Moved user token '{key}' to localStorage");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error moving user tokens to localStorage");
        }
    }

    /// <summary>
    /// Moves only user tokens from localStorage to sessionStorage
    /// </summary>
    private async Task MoveUserTokensToSessionStorage()
    {
        try
        {
            var userTokenKeys = await _jsRuntime.InvokeAsync<string[]>("eval", 
                "Object.keys(localStorage).filter(k => k.startsWith('oidc.user:'))");

            foreach (var key in userTokenKeys)
            {
                var value = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", key);
                if (!string.IsNullOrEmpty(value))
                {
                    await _jsRuntime.InvokeVoidAsync("sessionStorage.setItem", key, value);
                    await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", key);
                    _logger.LogInformation($"Moved user token '{key}' to sessionStorage");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error moving user tokens to sessionStorage");
        }
    }

    /// <summary>
    /// Copies user tokens from localStorage to sessionStorage (keeps both copies)
    /// This allows Blazor to find tokens in sessionStorage while maintaining persistence in localStorage
    /// </summary>
    private async Task CopyUserTokensFromLocalToSessionStorage()
    {
        var startTime = DateTime.UtcNow;
        try
        {
            _logger.LogInformation($"[{DateTime.UtcNow:HH:mm:ss.fff}] Starting CopyUserTokensFromLocalToSessionStorage");
            
            var userTokenKeys = await _jsRuntime.InvokeAsync<string[]>("eval", 
                "Object.keys(localStorage).filter(k => k.startsWith('oidc.user:'))");
            
            _logger.LogInformation($"[{DateTime.UtcNow:HH:mm:ss.fff}] Found {userTokenKeys.Length} user token keys");

            foreach (var key in userTokenKeys)
            {
                var value = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", key);
                if (!string.IsNullOrEmpty(value))
                {
                    await _jsRuntime.InvokeVoidAsync("sessionStorage.setItem", key, value);
                    _logger.LogInformation($"[{DateTime.UtcNow:HH:mm:ss.fff}] Copied user token '{key}' from localStorage to sessionStorage");
                }
            }

            if (userTokenKeys.Length > 0)
            {
                var elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;
                _logger.LogInformation($"[{DateTime.UtcNow:HH:mm:ss.fff}] Copied {userTokenKeys.Length} user token(s) to sessionStorage in {elapsed:0}ms");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"[{DateTime.UtcNow:HH:mm:ss.fff}] Error copying user tokens from localStorage to sessionStorage");
        }
    }

    /// <summary>
    /// Checks if there are OIDC tokens in a specific storage
    /// </summary>
    private async Task<bool> HasTokensInStorageAsync(string storageType)
    {
        try
        {
            var count = await _jsRuntime.InvokeAsync<int>("eval", 
                $"Object.keys({storageType}).filter(k => k.startsWith('oidc.') || k.includes('Authentication')).length");
            _logger.LogInformation($"HasTokensInStorageAsync - {storageType} count: {count}");
            return count > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error checking tokens in {storageType}");
            return false;
        }
    }

    /// <summary>
    /// Gets the ID token from storage if available.
    /// Used for OIDC logout flows that require id_token_hint (e.g., Keycloak).
    /// </summary>
    /// <returns>The ID token string or null if not available</returns>
    public async Task<string?> GetIdTokenAsync()
    {
        try
        {
            _logger.LogInformation("[GetIdTokenAsync] Attempting to retrieve ID token from storage");
            
            // The OIDC user data is stored in a JSON structure under oidc.user:{authority}:{clientId}
            // Try sessionStorage first, then localStorage
            var idToken = await _jsRuntime.InvokeAsync<string?>("eval", @"
                (function() {
                    // Try sessionStorage first
                    for (let key of Object.keys(sessionStorage)) {
                        if (key.startsWith('oidc.user:')) {
                            try {
                                let data = JSON.parse(sessionStorage.getItem(key));
                                if (data && data.id_token) {
                                    return data.id_token;
                                }
                            } catch (e) { }
                        }
                    }
                    // Then try localStorage
                    for (let key of Object.keys(localStorage)) {
                        if (key.startsWith('oidc.user:')) {
                            try {
                                let data = JSON.parse(localStorage.getItem(key));
                                if (data && data.id_token) {
                                    return data.id_token;
                                }
                            } catch (e) { }
                        }
                    }
                    return null;
                })()
            ");

            if (!string.IsNullOrEmpty(idToken))
            {
                _logger.LogInformation("[GetIdTokenAsync] Successfully retrieved ID token (length: {Length})", idToken.Length);
            }
            else
            {
                _logger.LogWarning("[GetIdTokenAsync] No ID token found in storage");
            }

            return idToken;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GetIdTokenAsync] Error retrieving ID token from storage");
            return null;
        }
    }
}