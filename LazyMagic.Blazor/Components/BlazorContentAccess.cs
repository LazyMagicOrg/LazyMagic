

namespace LazyMagic.Blazor;

public class BlazorContentAccess : IAsyncDisposable
{
    public BlazorContentAccess(IJSRuntime jsRuntime)
    {
        try
        {
            moduleTask = new(() => jsRuntime.InvokeAsync<IJSObjectReference>(
                "import", "./_content/LazyMagic.Blazor/blazorContentAccess.js").AsTask());
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
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
