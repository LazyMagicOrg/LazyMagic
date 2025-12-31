namespace LazyMagic.OIDC.MAUI;

/// <summary>
/// MAUI implementation of RememberMe service using Secure Storage
/// </summary>
public class MauiRememberMeService : IRememberMeService
{
    private readonly ILogger<MauiRememberMeService> _logger;
    private readonly ITokenStorageService _tokenStorage;
    private const string RememberMeKey = "rememberMe";
    private const string TokenKey = "authToken";

    public MauiRememberMeService(
        ILogger<MauiRememberMeService> logger,
        ITokenStorageService tokenStorage)
    {
        _logger = logger;
        _tokenStorage = tokenStorage;
    }

    public async Task<bool> GetRememberMeAsync()
    {
        try
        {
            var value = await SecureStorage.GetAsync(RememberMeKey);
            return value == "true";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting RememberMe preference");
            return false;
        }
    }

    public async Task SetRememberMeAsync(bool rememberMe)
    {
        try
        {
            if (rememberMe)
            {
                await SecureStorage.SetAsync(RememberMeKey, "true");
            }
            else
            {
                SecureStorage.Remove(RememberMeKey);
                await ClearTokensAsync();
            }
            
            _logger.LogInformation($"RememberMe set to: {rememberMe}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting RememberMe preference");
        }
    }

    public Task ClearTokensAsync()
    {
        try
        {
            SecureStorage.Remove(TokenKey);
            SecureStorage.Remove(RememberMeKey);
            _logger.LogInformation("Tokens cleared");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing tokens");
        }
        
        return Task.CompletedTask;
    }

    public async Task<bool> HasTokensAsync()
    {
        try
        {
            var token = await SecureStorage.GetAsync(TokenKey);
            return !string.IsNullOrEmpty(token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking for tokens");
            return false;
        }
    }

    public async Task InitializeAuthenticationAsync()
    {
        try
        {
            if (await GetRememberMeAsync() && await HasTokensAsync())
            {
                // In a real app, you would validate the token and restore the session
                _logger.LogInformation("Initializing authentication from stored tokens");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing authentication");
        }
    }

    /// <summary>
    /// Gets the ID token from secure storage if available.
    /// Used for OIDC logout flows that require id_token_hint (e.g., Keycloak).
    /// </summary>
    /// <returns>The ID token string or null if not available</returns>
    public async Task<string?> GetIdTokenAsync()
    {
        try
        {
            _logger.LogInformation("[GetIdTokenAsync] Attempting to retrieve ID token from secure storage");
            
            var (_, idToken, _) = await _tokenStorage.GetTokensAsync();
            
            if (!string.IsNullOrEmpty(idToken))
            {
                _logger.LogInformation("[GetIdTokenAsync] Successfully retrieved ID token (length: {Length})", idToken.Length);
            }
            else
            {
                _logger.LogWarning("[GetIdTokenAsync] No ID token found in secure storage");
            }
            
            return idToken;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GetIdTokenAsync] Error retrieving ID token from secure storage");
            return null;
        }
    }
}