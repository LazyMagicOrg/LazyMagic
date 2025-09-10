using Microsoft.JSInterop;
using System.IO;

namespace LazyMagic.BlazorSvg
{
    // This class provides JavaScript functionality where the
    // associated JavaScript module is loaded on demand when first needed.
    //
    // This class can be registered as scoped DI service and then injected into Blazor
    // components for use.

    public class SvgViewerJS : IAsyncDisposable
    {
        private readonly Lazy<Task<IJSObjectReference>> moduleTask;
        private DotNetObjectReference<SvgViewerJS> dotNetObjectReference;
        private string? containerId;

        public SvgViewerJS(IJSRuntime jsRuntime)
        {
            moduleTask = new(() => jsRuntime.InvokeAsync<IJSObjectReference>(
                "import", "./_content/LazyMagic.BlazorSvg/SvgViewer.js").AsTask());
            dotNetObjectReference = DotNetObjectReference.Create(this);
        }
        
        public async ValueTask<string> InitAsync(string containerId)
        {
            this.containerId = containerId;
            var module = await moduleTask.Value;
            var instanceId = await module.InvokeAsync<string>("initAsync", containerId, dotNetObjectReference);
            return instanceId;
        }
        public async ValueTask DisposeAsync()
        {
            if (moduleTask.IsValueCreated && containerId != null)
            {
                var module = await moduleTask.Value;
                await module.InvokeVoidAsync("disposeInstance", containerId);
                await module.DisposeAsync();
            }
        }
        
        public async ValueTask LoadSvgAsync(string svgUrl)
        {
            if (containerId == null) throw new InvalidOperationException("InitAsync must be called first");
            var module = await moduleTask.Value;
            await module.InvokeVoidAsync("loadSvgAsync", containerId, svgUrl);
        }
        
        public async ValueTask<bool> SelectPath(string pathId)
        {
            if (containerId == null) throw new InvalidOperationException("InitAsync must be called first");
            var module = await moduleTask.Value;
            var result = await module.InvokeAsync<bool>("selectPath", containerId, pathId);
            return result;
        }
        
        public async ValueTask<bool> SelectPaths(List<string> paths)
        {
            if (containerId == null) throw new InvalidOperationException("InitAsync must be called first");
            var module = await moduleTask.Value;
            var result = await module.InvokeAsync<bool>("selectPaths", containerId, paths);
            return result;
        }
        
        public async ValueTask<bool> UnselectPath(string pathId)
        {
            if (containerId == null) throw new InvalidOperationException("InitAsync must be called first");
            var module = await moduleTask.Value;
            var result = await module.InvokeAsync<bool>("unselectPath", containerId, pathId);
            return result;
        }
        
        public async Task UnselectAllPaths()
        {
            if (containerId == null) throw new InvalidOperationException("InitAsync must be called first");
            var module = await moduleTask.Value;
            await module.InvokeVoidAsync("unselectAllPaths", containerId);
        }
        [JSInvokable]
        public void OnPathSelected(string pathId) => PathSelectedEvent?.Invoke(pathId);
        public event PathSelectedEventHandler? PathSelectedEvent;
        public delegate void PathSelectedEventHandler(string pathId);
        [JSInvokable]
        public void OnPathUnselected(string pathId) => PathUnselectedEvent?.Invoke(pathId);
        public event PathUnselectedEventHandler? PathUnselectedEvent;
        public delegate void PathUnselectedEventHandler(string pathId);
        [JSInvokable]
        public void OnPathsChanged(List<string> pathIds) => PathsChangedEvent?.Invoke(pathIds);
        public event PathsChangedEventHandler? PathsChangedEvent;
        public delegate void PathsChangedEventHandler(List<string> pathIds);

    }

}