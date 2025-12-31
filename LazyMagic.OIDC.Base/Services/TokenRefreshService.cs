using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace LazyMagic.OIDC.Base.Services;

/// <summary>
/// Service that manages automatic token refresh before expiration
/// </summary>
public interface ITokenRefreshService : IDisposable
{
    /// <summary>
    /// Start monitoring token expiration and automatically refresh when needed
    /// </summary>
    Task StartMonitoringAsync();
    
    /// <summary>
    /// Stop monitoring token expiration
    /// </summary>
    void StopMonitoring();
    
    /// <summary>
    /// Manually trigger a token refresh
    /// </summary>
    Task<bool> RefreshTokensAsync();
    
    /// <summary>
    /// Update the expiration time being monitored
    /// </summary>
    void UpdateTokenExpiration(DateTime expirationTime);
}

public abstract class TokenRefreshServiceBase : ITokenRefreshService
{
    protected readonly ILogger _logger;
    private Timer? _refreshTimer;
    private readonly TimeSpan _refreshBuffer = TimeSpan.FromMinutes(5); // Refresh 5 minutes before expiration
    private DateTime _tokenExpiration = DateTime.MinValue;
    private CancellationTokenSource? _cancellationTokenSource;
    private bool _isRefreshing = false; // Guard against concurrent refresh attempts
    
    protected TokenRefreshServiceBase(ILogger logger)
    {
        _logger = logger;
    }
    
    public async Task StartMonitoringAsync()
    {
        _logger.LogInformation("[TokenRefresh] Starting token expiration monitoring");
        _cancellationTokenSource = new CancellationTokenSource();
        
        // Only fetch expiration if not already set (avoid triggering token requests)
        if (_tokenExpiration == DateTime.MinValue)
        {
            var currentExpiration = await GetCurrentTokenExpirationAsync();
            if (currentExpiration.HasValue)
            {
                UpdateTokenExpiration(currentExpiration.Value);
            }
        }
        else
        {
            _logger.LogInformation("[TokenRefresh] Using already set expiration: {Expiry}", _tokenExpiration);
        }
    }
    
    public void StopMonitoring()
    {
        _logger.LogInformation("[TokenRefresh] Stopping token expiration monitoring");
        _refreshTimer?.Dispose();
        _refreshTimer = null;
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;
        _tokenExpiration = DateTime.MinValue; // Reset so StartMonitoring will fetch fresh expiration
        _isRefreshing = false; // Reset refresh guard
    }
    
    public void UpdateTokenExpiration(DateTime expirationTime)
    {
        // Skip if we're currently refreshing (prevents loops)
        if (_isRefreshing)
        {
            _logger.LogDebug("[TokenRefresh] Skipping UpdateTokenExpiration during active refresh");
            return;
        }

        _tokenExpiration = expirationTime;
        
        // Cancel existing timer
        _refreshTimer?.Dispose();
        _refreshTimer = null;
        
        // Calculate when to refresh (5 minutes before expiration)
        var refreshTime = expirationTime.Subtract(_refreshBuffer);
        var delay = refreshTime - DateTime.UtcNow;
        
        if (delay <= TimeSpan.Zero)
        {
            // Token is already expired or expiring very soon
            // Don't trigger immediate refresh here - let the normal auth flow handle it
            // This prevents infinite loops when tokens can't be refreshed
            _logger.LogWarning("[TokenRefresh] Token expires in less than 5 minutes (at {Expiry}), will refresh on next auth check", 
                expirationTime);
        }
        else
        {
            _logger.LogInformation("[TokenRefresh] Scheduling token refresh for {RefreshTime} ({Delay} from now)", 
                refreshTime, delay);
            
            // Create a new timer for the refresh
            _refreshTimer = new Timer(async _ => 
            {
                if (_cancellationTokenSource?.IsCancellationRequested != true)
                {
                    await RefreshTokensAsync();
                }
            }, null, delay, Timeout.InfiniteTimeSpan);
        }
    }
    
    public async Task<bool> RefreshTokensAsync()
    {
        // Guard against concurrent refresh attempts (prevents loops)
        if (_isRefreshing)
        {
            _logger.LogWarning("[TokenRefresh] Refresh already in progress, skipping");
            return false;
        }

        try
        {
            _isRefreshing = true;
            _logger.LogInformation("[TokenRefresh] Starting token refresh");
            
            // Call platform-specific refresh implementation
            var result = await PerformTokenRefreshAsync();
            
            if (result)
            {
                _logger.LogInformation("[TokenRefresh] Token refresh successful");
                
                // Get the new expiration time and schedule next refresh
                // Note: This may trigger another token request, but we're guarded by _isRefreshing
                var newExpiration = await GetCurrentTokenExpirationAsync();
                if (newExpiration.HasValue)
                {
                    UpdateTokenExpiration(newExpiration.Value);
                }
            }
            else
            {
                _logger.LogWarning("[TokenRefresh] Token refresh failed");
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[TokenRefresh] Exception during token refresh");
            return false;
        }
        finally
        {
            _isRefreshing = false;
        }
    }
    
    /// <summary>
    /// Platform-specific token refresh implementation
    /// </summary>
    protected abstract Task<bool> PerformTokenRefreshAsync();
    
    /// <summary>
    /// Get the current token expiration time
    /// </summary>
    protected abstract Task<DateTime?> GetCurrentTokenExpirationAsync();
    
    public void Dispose()
    {
        StopMonitoring();
    }
}