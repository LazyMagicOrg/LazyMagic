namespace LazyMagic.Blazor;

public interface IConnectivityService
{
    event Action<bool>? ConnectivityChanged;
    bool IsOnline { get; }
    Task<bool> CheckConnectivityAsync();
    Task<bool> ShouldMakeNetworkRequestAsync();
    Task InitializeAsync();
    ValueTask DisposeAsync();
}

public class ConnectivityService : IConnectivityService, IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private DotNetObjectReference<ConnectivityService>? _objRef;
    private bool _isOnline = true;
    private bool _isInitialized = false;

    public ConnectivityService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public event Action<bool>? ConnectivityChanged;
    
    public bool IsOnline => _isOnline;

    public async Task InitializeAsync()
    {
        if (_isInitialized) return;

        try
        {
            _objRef = DotNetObjectReference.Create(this);
            await _jsRuntime.InvokeVoidAsync("initializeConnectivity", _objRef);
            _isInitialized = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to initialize connectivity service: {ex.Message}");
        }
    }

    public async Task<bool> CheckConnectivityAsync()
    {
        try
        {
            if (!_isInitialized) await InitializeAsync();
            return await _jsRuntime.InvokeAsync<bool>("getConnectivityStatus");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to check connectivity: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> ShouldMakeNetworkRequestAsync()
    {
        try
        {
            if (!_isInitialized) await InitializeAsync();
            return await _jsRuntime.InvokeAsync<bool>("shouldMakeNetworkRequest");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to check if should make network request: {ex.Message}");
            return false;
        }
    }

    [JSInvokable]
    public async Task OnConnectivityChanged(bool isOnline)
    {
        var wasOnline = _isOnline;
        _isOnline = isOnline;
        
        if (wasOnline != isOnline)
        {
            Console.WriteLine($"Connectivity changed: {(isOnline ? "Online" : "Offline")}");
            ConnectivityChanged?.Invoke(isOnline);
        }
        
        await Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (_isInitialized)
        {
            try
            {
                await _jsRuntime.InvokeVoidAsync("disposeConnectivity");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error disposing connectivity service: {ex.Message}");
            }
        }

        _objRef?.Dispose();
        _objRef = null;
        _isInitialized = false;
    }
}