using System;
using System.Threading.Tasks;
using LazyMagic.OIDC.Base.Services;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.Extensions.Logging;

namespace LazyMagic.OIDC.WASM.Services;

/// <summary>
/// WASM implementation of token refresh service
/// </summary>
public class WasmTokenRefreshService : TokenRefreshServiceBase
{
    private readonly IAccessTokenProvider _tokenProvider;
    private readonly ILogger<WasmTokenRefreshService> _specificLogger;
    
    public WasmTokenRefreshService(
        IAccessTokenProvider tokenProvider,
        ILogger<WasmTokenRefreshService> logger) 
        : base(logger)
    {
        _tokenProvider = tokenProvider;
        _specificLogger = logger;
    }
    
    protected override async Task<bool> PerformTokenRefreshAsync()
    {
        try
        {
            _specificLogger.LogInformation("[WasmTokenRefresh] Attempting to refresh access token");
            
            // Request a new access token - this will trigger refresh if needed
            var tokenResult = await _tokenProvider.RequestAccessToken();
            
            if (tokenResult.Status == AccessTokenResultStatus.Success)
            {
                if (tokenResult.TryGetToken(out var token))
                {
                    _specificLogger.LogInformation("[WasmTokenRefresh] Token refreshed successfully, new expiration: {Expiration}", 
                        token.Expires);
                    return true;
                }
            }
            else if (tokenResult.Status == AccessTokenResultStatus.RequiresRedirect)
            {
                _specificLogger.LogWarning("[WasmTokenRefresh] Token refresh requires user interaction (re-login)");
                // In WASM, we can't force a redirect from a background service
                // The next user action that requires authentication will trigger the redirect
            }
            
            return false;
        }
        catch (Exception ex)
        {
            _specificLogger.LogError(ex, "[WasmTokenRefresh] Error during token refresh");
            return false;
        }
    }
    
    protected override async Task<DateTime?> GetCurrentTokenExpirationAsync()
    {
        try
        {
            var tokenResult = await _tokenProvider.RequestAccessToken();
            
            if (tokenResult.Status == AccessTokenResultStatus.Success && 
                tokenResult.TryGetToken(out var token))
            {
                return token.Expires.UtcDateTime;
            }
            
            return null;
        }
        catch (Exception ex)
        {
            _specificLogger.LogError(ex, "[WasmTokenRefresh] Error getting token expiration");
            return null;
        }
    }
}