namespace LazyMagic.Blazor;

public class BlazorContentAccess : IAsyncDisposable
{
    private readonly ILogger<BlazorContentAccess>? _logger;
    
    public BlazorContentAccess(IJSRuntime jsRuntime, ILogger<BlazorContentAccess>? logger = null)
    {
        _logger = logger;
        try
        {
            moduleTask = new(() => jsRuntime.InvokeAsync<IJSObjectReference>(
                "import", "./_content/LazyMagic.Blazor/blazorContentAccess.js").AsTask());
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[Constructor][{Timestamp}] Error initializing module: {ErrorMessage}", DateTime.UtcNow.ToString("HH:mm:ss.fff"), ex.Message);
        }
    }
    private readonly Lazy<Task<IJSObjectReference>> moduleTask;

    public async ValueTask<string> GetBlazorContentAsync(string contentName)
    {
        try
        {
            var jsRuntime = await moduleTask.Value;
            //await Initialize(); 
            return await jsRuntime.InvokeAsync<string>("getBlazorContent", contentName);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[GetBlazorContentAsync][{Timestamp}] Error getting content '{ContentName}': {ErrorMessage}", DateTime.UtcNow.ToString("HH:mm:ss.fff"), contentName, ex.Message);
            return string.Empty;
        }   

    }
    public async ValueTask DisposeAsync()
    {
        if (moduleTask.IsValueCreated)
        {
            var module = await moduleTask.Value;
            await module.DisposeAsync();
        }
    }
}
