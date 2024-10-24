using Microsoft.JSInterop;

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

        public SvgViewerJS(IJSRuntime jsRuntime)
        {
            moduleTask = new(() => jsRuntime.InvokeAsync<IJSObjectReference>(
                "import", "./_content/LazyMagic.BlazorSvg/SvgViewer.js").AsTask());
            dotNetObjectReference = DotNetObjectReference.Create(this);
        }
        public async ValueTask InitAsync()
        {
            var module = await moduleTask.Value;
            await module.InvokeVoidAsync("initAsync", dotNetObjectReference);
        }
        public async ValueTask DisposeAsync()
        {
            if (moduleTask.IsValueCreated)
            {
                var module = await moduleTask.Value;
                await module.DisposeAsync();
            }
        }
        public async ValueTask<string> LoadSvgAsync(string svgUrl)
        {
            var module = await moduleTask.Value;
            var result = await module.InvokeAsync<string>("loadSvgAsync",svgUrl);
            return "";
        }
        public async ValueTask<bool> SelectPath(string pathId)
        {
            var module = await moduleTask.Value;
            var result = await module.InvokeAsync<bool>("selectPath", pathId);
            return result;
        }   
        public async ValueTask<bool> UnselectPath(string pathId)
        {
            var module = await moduleTask.Value;
            var result = await module.InvokeAsync<bool>("unselectPath", pathId);
            return result;
        }
        public async Task UnselectAllPaths()
        {
            var module = await moduleTask.Value;
            await module.InvokeVoidAsync("unselectAllPaths");
        }
        [JSInvokable]
        public void OnPathSelected(string pathId) => PathSelectedEvent?.Invoke(pathId);
        public event PathSelectedEventHandler? PathSelectedEvent;
        public delegate void PathSelectedEventHandler(string pathId);
        [JSInvokable]
        public void OnPathUnselected(string pathId) => PathUnselectedEvent?.Invoke(pathId);
        public event PathUnselectedEventHandler? PathUnselectedEvent;
        public delegate void PathUnselectedEventHandler(string pathId);
    }

}