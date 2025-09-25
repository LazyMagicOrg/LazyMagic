using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.JSInterop;

namespace LazyMagic.Blazorise.Console;

public class LzConsoleInterop : IAsyncDisposable
{
    private readonly Lazy<Task<IJSObjectReference>> moduleTask;
    private readonly IJSRuntime _jsRuntime;
    private DotNetObjectReference<LzConsoleInterop>? dotNetRef;

    public event Action<string>? ConsoleEvent;

    public LzConsoleInterop(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
        moduleTask = new(() => _jsRuntime.InvokeAsync<IJSObjectReference>(
            "import", "./_content/LzAppConsole/lzconsole.js").AsTask());
    }

    public async ValueTask InitAsync(string selector = "#lzConsoleContainer")
    {
        var module = await moduleTask.Value;
        dotNetRef = DotNetObjectReference.Create(this);

        await module.InvokeVoidAsync("initConsole", selector);
        await module.InvokeVoidAsync("setDotNetRef", dotNetRef);
    }

    [JSInvokable]
    public void OnConsoleEvent(string message)
    {
        ConsoleEvent?.Invoke(message);
    }

    public async Task<List<ConsoleLogEntry>> GetStoredLogsAsync()
    {
        var module = await moduleTask.Value;
        return await module.InvokeAsync<List<ConsoleLogEntry>>("getAllLogs");
    }

    public async Task ClearStoredLogsAsync()
    {
        var module = await moduleTask.Value;
        await module.InvokeVoidAsync("clearLogs");
    }

    public async ValueTask DisposeAsync()
    {
        if (moduleTask.IsValueCreated)
        {
            var module = await moduleTask.Value;
            await module.DisposeAsync();
        }

        dotNetRef?.Dispose();
    }
    
    public async Task ClearLogs()
    {
        var module = await moduleTask.Value;
        await module.InvokeVoidAsync("clearLogs");
    }


}