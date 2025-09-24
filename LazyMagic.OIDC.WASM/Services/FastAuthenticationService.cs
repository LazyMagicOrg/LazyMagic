using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using System.Text.Json;
using LazyMagic.Blazor;

namespace LazyMagic.OIDC.WASM.Services;

public interface IFastAuthenticationService
{
    Task<AuthenticationState> GetFastAuthenticationStateAsync();
    Task InvalidateCacheAsync();
    Task<bool> InitializeAsync();
    Task CacheAuthenticationStateAsync(AuthenticationState state, int cacheTimeoutMinutes = 5);
    Task<bool> IsCacheValidAsync();
}

public class FastAuthenticationService : IFastAuthenticationService
{
    private readonly ILzJsUtilities _jsUtilities;
    private readonly AuthenticationStateProvider _microsoftProvider;
    private readonly ILogger<FastAuthenticationService>? _logger;
    private bool _isInitialized;

    public FastAuthenticationService(
        ILzJsUtilities jsUtilities,
        AuthenticationStateProvider microsoftProvider,
        ILogger<FastAuthenticationService>? logger = null)
    {
        _jsUtilities = jsUtilities;
        _microsoftProvider = microsoftProvider;
        _logger = logger;
    }

    public async Task<bool> InitializeAsync()
    {
        try
        {
            if (_isInitialized)
                return true;

            _logger?.LogInformation("[FastAuth] Initializing fast authentication service...");
            
            var success = await _jsUtilities.InitializeFastAuth();
            if (success)
            {
                _isInitialized = true;
                _logger?.LogInformation("[FastAuth] Fast authentication service initialized successfully");
            }
            else
            {
                _logger?.LogWarning("[FastAuth] Failed to initialize fast authentication service");
            }
            
            return success;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[FastAuth] Error initializing fast authentication service");
            return false;
        }
    }

    public async Task<AuthenticationState> GetFastAuthenticationStateAsync()
    {
        try
        {
            // Ensure we're initialized
            await InitializeAsync();

            // First try to get cached state
            var cachedJson = await _jsUtilities.GetCachedAuthStateAsync();
            if (!string.IsNullOrEmpty(cachedJson))
            {
                var cachedState = DeserializeAuthState(cachedJson);
                if (cachedState != null)
                {
                    _logger?.LogDebug("[FastAuth] Returning cached authentication state");
                    return cachedState;
                }
            }

            _logger?.LogDebug("[FastAuth] No valid cache, checking stored tokens for fast parsing...");
            
            // Try to quickly parse tokens from browser storage
            var storedTokensJson = await _jsUtilities.GetStoredTokensAsync();
            if (!string.IsNullOrEmpty(storedTokensJson))
            {
                var quickState = await TryParseQuickAuthStateFromTokens(storedTokensJson);
                if (quickState != null)
                {
                    _logger?.LogDebug("[FastAuth] Created quick auth state from stored tokens");
                    
                    // Cache this state for future calls
                    await CacheAuthenticationStateAsync(quickState);
                    return quickState;
                }
            }

            _logger?.LogDebug("[FastAuth] No quick state available, falling back to Microsoft provider");
            
            // Fallback to Microsoft's provider (this may be slow)
            var state = await _microsoftProvider.GetAuthenticationStateAsync();
            
            // Cache the result for next time
            await CacheAuthenticationStateAsync(state);
            
            return state;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[FastAuth] Error getting fast authentication state");
            
            // Return safe unauthenticated state
            return new AuthenticationState(new ClaimsPrincipal());
        }
    }

    public async Task CacheAuthenticationStateAsync(AuthenticationState state, int cacheTimeoutMinutes = 5)
    {
        try
        {
            var stateJson = SerializeAuthState(state);
            await _jsUtilities.SetCachedAuthStateAsync(stateJson, cacheTimeoutMinutes);
            _logger?.LogDebug("[FastAuth] Authentication state cached successfully");
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "[FastAuth] Failed to cache authentication state");
        }
    }

    public async Task InvalidateCacheAsync()
    {
        try
        {
            await _jsUtilities.ClearAuthCacheAsync();
            _logger?.LogDebug("[FastAuth] Authentication cache invalidated");
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "[FastAuth] Failed to invalidate cache");
        }
    }

    public async Task<bool> IsCacheValidAsync()
    {
        try
        {
            return await _jsUtilities.IsAuthCacheValidAsync();
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "[FastAuth] Error checking cache validity");
            return false;
        }
    }

    private async Task<AuthenticationState?> TryParseQuickAuthStateFromTokens(string storedTokensJson)
    {
        try
        {
            var tokens = JsonSerializer.Deserialize<Dictionary<string, string>>(storedTokensJson);
            if (tokens == null || !tokens.Any())
                return null;

            // Look for OIDC user data first
            if (tokens.TryGetValue("localStorage.oidc.user", out var oidcUserJson))
            {
                return await ParseOidcUserData(oidcUserJson);
            }

            // Look for Microsoft's cached auth settings
            if (tokens.TryGetValue("localStorage.Microsoft.AspNetCore.Components.WebAssembly.Authentication.CachedAuthSettings", out var cachedAuthJson))
            {
                return await ParseMicrosoftCachedAuth(cachedAuthJson);
            }

            // If we have individual tokens, try to create a basic authenticated state
            if (tokens.Any(kvp => kvp.Key.Contains("access_token") || kvp.Key.Contains("id_token")))
            {
                _logger?.LogDebug("[FastAuth] Found authentication tokens, creating basic authenticated state");
                
                // Create a minimal authenticated identity
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Authentication, "true"),
                    new Claim("auth_time", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString())
                };
                
                var identity = new ClaimsIdentity(claims, "oidc");
                return new AuthenticationState(new ClaimsPrincipal(identity));
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "[FastAuth] Error parsing quick auth state from tokens");
            return null;
        }
    }

    private async Task<AuthenticationState?> ParseOidcUserData(string oidcUserJson)
    {
        try
        {
            var oidcUser = JsonSerializer.Deserialize<JsonElement>(oidcUserJson);
            if (oidcUser.TryGetProperty("profile", out var profile))
            {
                var claims = new List<Claim>();
                
                foreach (var prop in profile.EnumerateObject())
                {
                    var value = prop.Value.ValueKind == JsonValueKind.String 
                        ? prop.Value.GetString()
                        : prop.Value.ToString();
                        
                    if (!string.IsNullOrEmpty(value))
                    {
                        claims.Add(new Claim(prop.Name, value));
                    }
                }
                
                if (claims.Any())
                {
                    var identity = new ClaimsIdentity(claims, "oidc");
                    return new AuthenticationState(new ClaimsPrincipal(identity));
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "[FastAuth] Error parsing OIDC user data");
        }
        
        return null;
    }

    private async Task<AuthenticationState?> ParseMicrosoftCachedAuth(string cachedAuthJson)
    {
        try
        {
            var cachedAuth = JsonSerializer.Deserialize<JsonElement>(cachedAuthJson);
            if (cachedAuth.TryGetProperty("user", out var user) && 
                user.TryGetProperty("profile", out var profile))
            {
                var claims = new List<Claim>();
                
                foreach (var prop in profile.EnumerateObject())
                {
                    var value = prop.Value.ValueKind == JsonValueKind.String 
                        ? prop.Value.GetString()
                        : prop.Value.ToString();
                        
                    if (!string.IsNullOrEmpty(value))
                    {
                        claims.Add(new Claim(prop.Name, value));
                    }
                }
                
                if (claims.Any())
                {
                    var identity = new ClaimsIdentity(claims, "oidc");
                    return new AuthenticationState(new ClaimsPrincipal(identity));
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "[FastAuth] Error parsing Microsoft cached auth");
        }
        
        return null;
    }

    private string SerializeAuthState(AuthenticationState state)
    {
        var authData = new
        {
            IsAuthenticated = state.User.Identity?.IsAuthenticated ?? false,
            Name = state.User.Identity?.Name,
            AuthenticationType = state.User.Identity?.AuthenticationType,
            Claims = state.User.Claims?.Select(c => new { c.Type, c.Value }).ToArray() ?? Array.Empty<object>(),
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };
        
        return JsonSerializer.Serialize(authData);
    }

    private AuthenticationState? DeserializeAuthState(string json)
    {
        try
        {
            var authData = JsonSerializer.Deserialize<JsonElement>(json);
            
            if (!authData.TryGetProperty("IsAuthenticated", out var isAuthProp))
                return null;
                
            var isAuthenticated = isAuthProp.GetBoolean();
            
            if (!isAuthenticated)
            {
                return new AuthenticationState(new ClaimsPrincipal());
            }
            
            var claims = new List<Claim>();
            
            if (authData.TryGetProperty("Claims", out var claimsArray))
            {
                foreach (var claimElement in claimsArray.EnumerateArray())
                {
                    if (claimElement.TryGetProperty("Type", out var typeProp) &&
                        claimElement.TryGetProperty("Value", out var valueProp))
                    {
                        var type = typeProp.GetString();
                        var value = valueProp.GetString();
                        
                        if (!string.IsNullOrEmpty(type) && !string.IsNullOrEmpty(value))
                        {
                            claims.Add(new Claim(type, value));
                        }
                    }
                }
            }
            
            string? authType = null;
            if (authData.TryGetProperty("AuthenticationType", out var authTypeProp))
            {
                authType = authTypeProp.GetString();
            }
            
            var identity = new ClaimsIdentity(claims, authType ?? "oidc");
            return new AuthenticationState(new ClaimsPrincipal(identity));
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "[FastAuth] Error deserializing auth state");
            return null;
        }
    }
}