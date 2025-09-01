
namespace LazyMagic.OIDC.MAUI;

/// <summary>
/// Service for securely storing and retrieving authentication tokens
/// </summary>
public interface ITokenStorageService
{
    Task SaveTokensAsync(string accessToken, string idToken, string refreshToken);
    Task<(string? accessToken, string? idToken, string? refreshToken)> GetTokensAsync();
    Task ClearTokensAsync();
    Task SetLoggedOutAsync(bool loggedOut);
    Task<bool> HasLoggedOutAsync();
}

public class TokenStorageService : ITokenStorageService
{
    private readonly ILogger<TokenStorageService> _logger;
    private const string AccessTokenKey = "aws_access_token";
    private const string IdTokenKey = "aws_id_token";
    private const string RefreshTokenKey = "aws_refresh_token";
    private const string LoggedOutKey = "aws_logged_out";

    public TokenStorageService(ILogger<TokenStorageService> logger)
    {
        _logger = logger;
    }

    public async Task SaveTokensAsync(string accessToken, string idToken, string refreshToken)
    {
        try
        {
            // Use MAUI SecureStorage for sensitive data
            await SecureStorage.SetAsync(AccessTokenKey, accessToken);
            await SecureStorage.SetAsync(IdTokenKey, idToken);
            await SecureStorage.SetAsync(RefreshTokenKey, refreshToken);
            
            _logger.LogInformation("Tokens saved securely");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save tokens");
        }
    }

    public async Task<(string? accessToken, string? idToken, string? refreshToken)> GetTokensAsync()
    {
        try
        {
            var accessToken = await SecureStorage.GetAsync(AccessTokenKey);
            var idToken = await SecureStorage.GetAsync(IdTokenKey);
            var refreshToken = await SecureStorage.GetAsync(RefreshTokenKey);
            
            _logger.LogInformation("Tokens retrieved: AccessToken={HasAccess}, IdToken={HasId}, RefreshToken={HasRefresh}",
                !string.IsNullOrEmpty(accessToken),
                !string.IsNullOrEmpty(idToken),
                !string.IsNullOrEmpty(refreshToken));
            
            return (accessToken, idToken, refreshToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve tokens");
            return (null, null, null);
        }
    }

    public async Task ClearTokensAsync()
    {
        try
        {
            SecureStorage.Remove(AccessTokenKey);
            SecureStorage.Remove(IdTokenKey);
            SecureStorage.Remove(RefreshTokenKey);
            
            _logger.LogInformation("Tokens cleared");
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear tokens");
        }
    }
    
    public async Task SetLoggedOutAsync(bool loggedOut)
    {
        try
        {
            if (loggedOut)
            {
                await SecureStorage.SetAsync(LoggedOutKey, "true");
                _logger.LogInformation("Logged out flag set");
            }
            else
            {
                SecureStorage.Remove(LoggedOutKey);
                _logger.LogInformation("Logged out flag cleared");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set logged out flag");
        }
    }
    
    public async Task<bool> HasLoggedOutAsync()
    {
        try
        {
            var value = await SecureStorage.GetAsync(LoggedOutKey);
            var hasLoggedOut = value == "true";
            _logger.LogInformation("Logged out flag: {HasLoggedOut}", hasLoggedOut);
            return hasLoggedOut;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get logged out flag");
            return false;
        }
    }
}