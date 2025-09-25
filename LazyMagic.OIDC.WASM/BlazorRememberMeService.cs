namespace LazyMagic.OIDC.WASM;

/// <summary>
/// Service to manage Remember Me functionality for token persistence
/// </summary>
public class BlazorRememberMeService : IRememberMeService
{
    private readonly IJSRuntime _jsRuntime;
    private readonly ILogger<BlazorRememberMeService> _logger;
    private const string REMEMBER_ME_KEY = "rememberMe";
    private const string REMEMBER_ME_PREFERENCE_KEY = "rememberMePreference";

    public BlazorRememberMeService(
        IJSRuntime jsRuntime, 
        ILogger<BlazorRememberMeService> logger)
    {
        _jsRuntime = jsRuntime;
        _logger = logger;
    }

    /// <summary>
    /// Gets the current Remember Me setting
    /// </summary>
    public async Task<bool> GetRememberMeAsync()
    {
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
    /// Sets the Remember Me setting and manages token storage accordingly
    /// </summary>
    public async Task SetRememberMeAsync(bool rememberMe)
    {
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
    /// Clears all authentication tokens based on Remember Me setting
    /// </summary>
    public async Task ClearTokensAsync()
    {
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
    /// Initializes authentication on app startup based on RememberMe setting
    /// Ensures tokens are available to Blazor while maintaining persistence
    /// </summary>
    public async Task InitializeAuthenticationAsync()
    {
        var startTime = DateTime.UtcNow;
        try
        {
            _logger.LogInformation($"[{DateTime.UtcNow:HH:mm:ss.fff}] InitializeAuthenticationAsync started");
            
            var rememberMe = await GetRememberMeAsync();
            _logger.LogInformation($"[{DateTime.UtcNow:HH:mm:ss.fff}] GetRememberMeAsync returned: {rememberMe}");
            
            // Check for stale tokens and clean them up (but not during OAuth callback)
            var hasTokens = await HasTokensAsync();
            if (hasTokens && !rememberMe)
            {
                // Check if we're in an OAuth callback - if so, preserve tokens needed for completion
                try
                {
                    var currentUrl = await _jsRuntime.InvokeAsync<string>("eval", "window.location.href");
                    var isOAuthCallback = currentUrl.Contains("/authentication/login-callback") || 
                                         currentUrl.Contains("code=") ||
                                         currentUrl.Contains("state=");
                    
                    if (!isOAuthCallback)
                    {
                        // We have leftover tokens but RememberMe is disabled and we're not in an OAuth flow - clean them up
                        _logger.LogInformation($"[{DateTime.UtcNow:HH:mm:ss.fff}] Found stale tokens with RememberMe disabled, cleaning up");
                        await ClearTokensAsync();
                    }
                    else
                    {
                        _logger.LogInformation($"[{DateTime.UtcNow:HH:mm:ss.fff}] OAuth callback detected, preserving tokens for completion");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not check URL for OAuth callback, skipping token cleanup");
                }
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
}