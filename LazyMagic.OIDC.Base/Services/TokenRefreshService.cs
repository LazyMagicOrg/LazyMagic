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
    
    protected TokenRefreshServiceBase(ILogger logger)
    {
        _logger = logger;
    }
    
    public async Task StartMonitoringAsync()
    {
        _logger.LogInformation("[TokenRefresh] Starting token expiration monitoring");
        _cancellationTokenSource = new CancellationTokenSource();
        
        // Check current token expiration
        var currentExpiration = await GetCurrentTokenExpirationAsync();
        if (currentExpiration.HasValue)
        {
            UpdateTokenExpiration(currentExpiration.Value);
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
    }
    
    public void UpdateTokenExpiration(DateTime expirationTime)
    {
        _tokenExpiration = expirationTime;
        
        // Cancel existing timer
        _refreshTimer?.Dispose();
        
        // Calculate when to refresh (5 minutes before expiration)
        var refreshTime = expirationTime.Subtract(_refreshBuffer);
        var delay = refreshTime - DateTime.UtcNow;
        
        if (delay <= TimeSpan.Zero)
        {
            _logger.LogWarning("[TokenRefresh] Token expires in less than 5 minutes, refreshing immediately");
            _ = Task.Run(async () => await RefreshTokensAsync());
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
        try
        {
            _logger.LogInformation("[TokenRefresh] Starting token refresh");
            
            // Call platform-specific refresh implementation
            var result = await PerformTokenRefreshAsync();
            
            if (result)
            {
                _logger.LogInformation("[TokenRefresh] Token refresh successful");
                
                // Get the new expiration time and schedule next refresh
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