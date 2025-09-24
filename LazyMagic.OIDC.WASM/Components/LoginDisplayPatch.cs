using Microsoft.AspNetCore.Components;
using LazyMagic.OIDC.WASM.Services;

namespace LazyMagic.OIDC.WASM.Components;

/// <summary>
/// Extension methods to add fast authentication capabilities to existing LoginDisplay components
/// This allows existing components to get fast auth benefits with minimal changes
/// </summary>
public static class LoginDisplayExtensions
{
    /// <summary>
    /// Fast authentication state check that can replace slow IOIDCService calls
    /// Returns immediately with cached state, validates in background if needed
    /// </summary>
    public static async Task<(bool isAuthenticated, string? userName)> GetFastAuthStateAsync(
        this IOIDCService oidcService, 
        IFastAuthenticationService fastAuth,
        ILogger? logger = null)
    {
        try
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            // Get fast cached result
            var fastState = await fastAuth.GetFastAuthenticationStateAsync();
            var isAuthenticated = fastState.User?.Identity?.IsAuthenticated ?? false;
            var userName = fastState.User?.Identity?.Name;
            
            stopwatch.Stop();
            logger?.LogDebug("[FastAuthPatch] Fast auth completed in {Time}ms", stopwatch.ElapsedMilliseconds);
            
            // If we got an unauthenticated result from cache, validate in background
            // This ensures accuracy while maintaining speed
            if (!isAuthenticated)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        logger?.LogDebug("[FastAuthPatch] Running background validation...");
                        var fullAuthState = await oidcService.GetAuthenticationStateAsync();
                        
                        // If full check differs, we should update the cache
                        if (fullAuthState.IsAuthenticated != isAuthenticated)
                        {
                            logger?.LogInformation("[FastAuthPatch] Background validation found authenticated state, updating cache");
                            
                            // Convert IOIDCService result to AuthenticationState and cache it
                            var claims = await oidcService.GetUserClaimsAsync();
                            var identity = new System.Security.Claims.ClaimsIdentity(claims, "oidc");
                            var authState = new Microsoft.AspNetCore.Components.Authorization.AuthenticationState(
                                new System.Security.Claims.ClaimsPrincipal(identity));
                            
                            await fastAuth.CacheAuthenticationStateAsync(authState);
                        }
                    }
                    catch (Exception ex)
                    {
                        logger?.LogWarning(ex, "[FastAuthPatch] Background validation failed");
                    }
                });
            }
            
            return (isAuthenticated, userName);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "[FastAuthPatch] Fast auth failed, falling back to standard");
            
            // Fallback to standard service
            var standardState = await oidcService.GetAuthenticationStateAsync();
            return (standardState.IsAuthenticated, standardState.UserName);
        }
    }
}