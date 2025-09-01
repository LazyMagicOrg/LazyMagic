namespace LazyMagic.OIDC.MAUI;

/// <summary>
/// MAUI implementation of RememberMe service using Secure Storage
/// </summary>
public class MauiRememberMeService : IRememberMeService
{
    private readonly ILogger<MauiRememberMeService> _logger;
    private const string RememberMeKey = "rememberMe";
    private const string TokenKey = "authToken";

    public MauiRememberMeService(ILogger<MauiRememberMeService> logger)
    {
        _logger = logger;
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
}