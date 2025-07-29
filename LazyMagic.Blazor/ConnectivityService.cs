namespace LazyMagic.Blazor;

public class ConnectivityService : NotifyBase, IConnectivityService
{
    private IJSRuntime? _jsRuntime;
    private DotNetObjectReference<ConnectivityService>? _objRef;
    private bool _isOnline = true;
    private bool _isInitialized = false;

    public ConnectivityService()
    {
        _objRef = DotNetObjectReference.Create(this);
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
            await _jsRuntime.InvokeVoidAsync("initializeConnectivity", _objRef);
            _isInitialized = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to initialize connectivity service: {ex.Message}");
        }
    }
    public async Task<bool> CheckInternetConnectivityAsync()
    {
        if (!_isInitialized)
        {
            throw new InvalidOperationException("JSRuntime must be set before calling InitializeAsync.");
        }
        try
        {
            return await _jsRuntime!.InvokeAsync<bool>("getConnectivityStatus");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to check connectivity: {ex.Message}");
            return false;
        }
    }
    public async Task<bool> ShouldMakeNetworkRequestAsync()
    {
        if (!_isInitialized)
        {
            throw new InvalidOperationException("JSRuntime must be set before calling InitializeAsync.");
        }
        try
        {
            return await _jsRuntime!.InvokeAsync<bool>("shouldMakeNetworkRequest");
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
        IsOnline = isOnline;
        await Task.CompletedTask;
    }
    public async void Dispose()
    {

        if (_isInitialized && _jsRuntime != null)
        {
            await _jsRuntime.InvokeVoidAsync("disposeConnectivity");
        }
        _objRef?.Dispose();
        _objRef = null;
        GC.SuppressFinalize(this);
    }
    public async ValueTask DisposeAsync()
    {
        if (_isInitialized && _jsRuntime != null)
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