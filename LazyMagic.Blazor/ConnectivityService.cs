namespace LazyMagic.Blazor;

public class ConnectivityService : NotifyBase, IConnectivityService, IAsyncDisposable
{
    private IJSRuntime? _jsRuntime;
    private DotNetObjectReference<ConnectivityService>? _objRef;
    private bool _isOnline = true;
    private bool _isInitialized = false;
    private bool _disposed = false;
    private ILzHost _host;
    private readonly ILogger<ConnectivityService>? _logger;
    
    public ConnectivityService(ILzHost host, ILogger<ConnectivityService>? logger = null)
    {
        _objRef = DotNetObjectReference.Create(this);
        _host = host;
        _logger = logger;
    }
    public bool IsOnline
    {
        get => _isOnline;
        protected set => SetProperty(ref _isOnline, value);
    }
    public async Task InitializeAsync(IJSRuntime jsRuntime)
    {
        if (_isInitialized) return;

        if(jsRuntime == null)
        {
            throw new InvalidOperationException("JSRuntime must be set before calling InitializeAsync.");
        }
        _jsRuntime = jsRuntime;
        try
        {
            await _jsRuntime.InvokeVoidAsync("import", "./_content/LazyMagic.Blazor/connectivityManager.js");
            await _jsRuntime.InvokeVoidAsync("initializeConnectivity", _objRef, _host.AssetsUrl);
            _isInitialized = true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[InitializeAsync][{Timestamp}] Failed to initialize connectivity service: {ErrorMessage}", DateTime.UtcNow.ToString("HH:mm:ss.fff"), ex.Message);
        }
    }
    public async Task<bool> CheckInternetConnectivityAsync()
    {
        if (!_isInitialized || _jsRuntime == null)
        {
            throw new InvalidOperationException("Service must be initialized before calling this method.");
        }
        
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ConnectivityService));
        }
        
        try
        {
            return await _jsRuntime.InvokeAsync<bool>("getConnectivityStatus");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[CheckInternetConnectivityAsync][{Timestamp}] Failed to check connectivity: {ErrorMessage}", DateTime.UtcNow.ToString("HH:mm:ss.fff"), ex.Message);
            return false;
        }
    }
    public async Task<bool> ShouldMakeNetworkRequestAsync()
    {
        if (!_isInitialized || _jsRuntime == null)
        {
            throw new InvalidOperationException("Service must be initialized before calling this method.");
        }
        
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ConnectivityService));
        }
        
        try
        {
            return await _jsRuntime.InvokeAsync<bool>("shouldMakeNetworkRequest");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[ShouldMakeNetworkRequestAsync][{Timestamp}] Failed to check if should make network request: {ErrorMessage}", DateTime.UtcNow.ToString("HH:mm:ss.fff"), ex.Message);
            return false;
        }
    }
    [JSInvokable]
    public async Task OnConnectivityChanged(bool isOnline)
    {
        IsOnline = isOnline;
        await Task.CompletedTask;
    }
    public void Dispose()
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
    
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;
            
        _disposed = true;
        
        if (_isInitialized && _jsRuntime != null)
        {
            try
            {
                await _jsRuntime.InvokeVoidAsync("disposeConnectivity");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[DisposeAsync][{Timestamp}] Error disposing connectivity service: {ErrorMessage}", DateTime.UtcNow.ToString("HH:mm:ss.fff"), ex.Message);
            }
        }
        
        _objRef?.Dispose();
        _objRef = null;
        _isInitialized = false;
        
        GC.SuppressFinalize(this);
    }
}