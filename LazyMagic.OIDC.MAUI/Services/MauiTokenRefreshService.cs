using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using LazyMagic.OIDC.Base.Services;
using Microsoft.Extensions.Logging;

namespace LazyMagic.OIDC.MAUI.Services;

/// <summary>
/// MAUI implementation of token refresh service
/// Uses stored refresh tokens to refresh access tokens before expiration
/// </summary>
public class MauiTokenRefreshService : TokenRefreshServiceBase
{
    private readonly ITokenStorageService _tokenStorage;
    private readonly IOidcConfig _oidcConfig;
    private readonly ILogger<MauiTokenRefreshService> _specificLogger;
    
    public MauiTokenRefreshService(
        ITokenStorageService tokenStorage,
        IOidcConfig oidcConfig,
        ILogger<MauiTokenRefreshService> logger) 
        : base(logger)
    {
        _tokenStorage = tokenStorage;
        _oidcConfig = oidcConfig;
        _specificLogger = logger;
    }
    
    protected override async Task<bool> PerformTokenRefreshAsync()
    {
        try
        {
            _specificLogger.LogInformation("[MauiTokenRefresh] Attempting to refresh access token");
            
            // Get stored refresh token
            var (accessToken, idToken, refreshToken) = await _tokenStorage.GetTokensAsync();
            
            if (string.IsNullOrEmpty(refreshToken))
            {
                _specificLogger.LogWarning("[MauiTokenRefresh] No refresh token available for token refresh");
                return false;
            }
            
            // Get auth configuration for token refresh
            var selectedConfig = _oidcConfig.SelectedAuthConfig;
            if (!_oidcConfig.AuthConfigs.TryGetValue(selectedConfig, out var authConfig))
            {
                _specificLogger.LogError("[MauiTokenRefresh] No auth configuration found for token refresh");
                return false;
            }
            
            // Extract configuration values
            var userPoolClientId = authConfig["userPoolClientId"]?.ToString() ?? authConfig["ClientId"]?.ToString();
            var awsRegion = authConfig["awsRegion"]?.ToString();
            
            var authority = authConfig["HostedUIDomain"]?.ToString() 
                ?? authConfig["cognitoDomain"]?.ToString()
                ?? (!string.IsNullOrEmpty(awsRegion) && !string.IsNullOrEmpty(authConfig["cognitoDomainPrefix"]?.ToString())
                    ? $"https://{authConfig["cognitoDomainPrefix"]}.auth.{awsRegion}.amazoncognito.com"
                    : null);
            
            if (string.IsNullOrEmpty(authority) || string.IsNullOrEmpty(userPoolClientId))
            {
                _specificLogger.LogError("[MauiTokenRefresh] Missing required configuration for token refresh");
                return false;
            }
            
            // Perform token refresh
            var newTokens = await RefreshTokensAsync(authority, userPoolClientId, refreshToken);
            
            if (newTokens != null)
            {
                // Save the new tokens
                await _tokenStorage.SaveTokensAsync(
                    newTokens.AccessToken, 
                    newTokens.IdToken ?? idToken, // Keep existing ID token if new one not provided
                    newTokens.RefreshToken ?? refreshToken); // Keep existing refresh token if new one not provided
                
                _specificLogger.LogInformation("[MauiTokenRefresh] Tokens refreshed successfully, new expiration: {Expiration}", 
                    DateTime.UtcNow.AddSeconds(newTokens.ExpiresIn));
                return true;
            }
            
            return false;
        }
        catch (Exception ex)
        {
            _specificLogger.LogError(ex, "[MauiTokenRefresh] Error during token refresh");
            return false;
        }
    }
    
    protected override async Task<DateTime?> GetCurrentTokenExpirationAsync()
    {
        try
        {
            var (accessToken, idToken, refreshToken) = await _tokenStorage.GetTokensAsync();
            
            if (!string.IsNullOrEmpty(idToken))
            {
                // Parse the ID token to get expiration
                var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
                var token = handler.ReadJwtToken(idToken);
                return token.ValidTo;
            }
            
            return null;
        }
        catch (Exception ex)
        {
            _specificLogger.LogError(ex, "[MauiTokenRefresh] Error getting token expiration");
            return null;
        }
    }
    
    private async Task<TokenRefreshResponse?> RefreshTokensAsync(string authority, string clientId, string refreshToken)
    {
        try
        {
            var tokenUrl = $"{authority}/oauth2/token";
            
            var parameters = new Dictionary<string, string>
            {
                {"grant_type", "refresh_token"},
                {"client_id", clientId},
                {"refresh_token", refreshToken}
            };

            using var httpClient = new HttpClient();
            var content = new FormUrlEncodedContent(parameters);
            
            _specificLogger.LogInformation("[MauiTokenRefresh] Refreshing tokens at: {TokenUrl}", tokenUrl);
            var response = await httpClient.PostAsync(tokenUrl, content);
            
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                
                var tokenResponse = JsonSerializer.Deserialize<TokenRefreshResponse>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                _specificLogger.LogInformation("[MauiTokenRefresh] Successfully refreshed tokens");
                return tokenResponse;
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                _specificLogger.LogError("[MauiTokenRefresh] Token refresh failed: {StatusCode} - {Error}", response.StatusCode, error);
            }
        }
        catch (Exception ex)
        {
            _specificLogger.LogError(ex, "[MauiTokenRefresh] Error during token refresh HTTP request");
        }
        
        return null;
    }
    
    private class TokenRefreshResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = "";
        
        [JsonPropertyName("id_token")]
        public string? IdToken { get; set; }
        
        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; set; }
        
        [JsonPropertyName("token_type")]
        public string TokenType { get; set; } = "";
        
        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
    }
}