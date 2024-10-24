
namespace LazyMagic.Blazor;

public abstract class LzBaseJSModule : ILzBaseJSModule, INotifyPropertyChanged, IAsyncDisposable
{
    protected IJSRuntime jsRuntime;
    private Task<IJSObjectReference>? moduleTask;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Returns true if module was already being destroyed.
    /// </summary>
    protected bool IsUnsafe => AsyncDisposed || moduleTask is null;

    /// <summary>
    /// Indicates if the component is already fully disposed (asynchronously).
    /// </summary>
    protected bool AsyncDisposed { get; private set; }

    /// <inheritdoc/>
    public Task<IJSObjectReference> Module => GetModule();

    /// <inheritdoc/>
    public abstract string ModuleFileName { get; }

    /// <summary>
    /// Gets the JavaScript runtime instance.
    /// </summary>
    public IJSRuntime JSRuntime => jsRuntime;

    public virtual void SetJSRuntime(object jsRuntime)
    {
        this.jsRuntime = (JSRuntime)jsRuntime;
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// Invokes the module instance javascript function.
    /// </summary>
    /// <param name="identifier"></param>
    /// <param name="args"></param>
    /// <returns></returns>
    protected async ValueTask InvokeVoidAsync(string identifier, params object[] args)
    {
        var moduleInstance = await Module;

        await moduleInstance.InvokeVoidAsync(identifier, args);
    }

    /// <summary>
    /// Invokes the module instance javascript function.
    /// </summary>
    /// <param name="identifier"></param>
    /// <param name="args"></param>
    /// <returns></returns>
    protected async ValueTask<TValue> InvokeAsync<TValue>(string identifier, params object[] args)
    {
        var moduleInstance = await Module;

        return await moduleInstance.InvokeAsync<TValue>(identifier, args);
    }

    /// <inheritdoc/>
    public virtual async ValueTask DisposeAsync()
    {
        await DisposeAsync(true);
    }

    /// <summary>
    /// Releases the unmanaged resources used by the module and optionally releases the managed resources.
    /// </summary>
    /// <param name="disposing">True if the component is in the disposing process.</param>
    protected virtual async ValueTask DisposeAsync(bool disposing)
    {
        try
        {
            if (!AsyncDisposed)
            {
                AsyncDisposed = true;

                if (disposing)
                {
                    if (moduleTask is not null)
                    {
                        var moduleInstance = await moduleTask;


                        var disposeModule = async (IAsyncDisposable disposable) =>
                        {
                            var disposableTask = disposable.DisposeAsync();
                            try
                            {
                                await disposableTask;
                            }
                            catch when (disposableTask.IsCanceled)
                            {
                            }
                            catch (Microsoft.JSInterop.JSDisconnectedException)
                            {
                            }

                            moduleTask = null;
                        };

                        await disposeModule(moduleInstance);
                    }
                }
            }
        }
        catch (Exception exc)
        {
            await Task.FromException(exc);
        }
    }


    /// <summary>
    /// Save invocation on the JavaScript <see cref="Module"/>.
    /// </summary>
    /// <param name="identifier">An identifier for the function to invoke. For example, the value <c>"someScope.someFunction"</c> will invoke the function <c>someScope.someFunction</c> on the target instance.</param>
    /// <param name="args">JSON-serializable arguments.</param>
    protected async ValueTask InvokeSafeVoidAsync(string identifier, params object[] args)
    {
        try
        {
            var module = await Module;

            if (AsyncDisposed)
            {
                return;
            }

            await module.InvokeVoidAsync(identifier, args);
        }
        catch (Exception exc) when (exc is JSDisconnectedException or ObjectDisposedException or TaskCanceledException)
        {
            Console.WriteLine($"InvokeSafeVoidAsync expected error: {exc.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"InvokeSafeVoidAsync unexpected error: {ex.Message}");
        }

    }

    /// <summary>
    /// Save invocation on the JavaScript <see cref="Module"/>.
    /// </summary>
    /// <typeparam name="TValue">The JSON-serializable return type.</typeparam>
    /// <param name="identifier">An identifier for the function to invoke. For example, the value <c>"someScope.someFunction"</c> will invoke the function <c>someScope.someFunction</c> on the target instance.</param>
    /// <param name="args">JSON-serializable arguments.</param>
    /// <returns>An instance of <typeparamref name="TValue"/> obtained by JSON-deserializing the return value.</returns>
    protected async ValueTask<TValue> InvokeSafeAsync<TValue>(string identifier, params object[] args)
    {
        try
        {
            var module = await Module;

            if (AsyncDisposed)
            {
                return default;
            }

            return await module.InvokeAsync<TValue>(identifier, args);
        }
        catch (Exception exc) when (exc is JSDisconnectedException or ObjectDisposedException or TaskCanceledException)
        {
            return default;
        }
    }

    private Task<IJSObjectReference> GetModule()
    {
        if (jsRuntime is null)
            throw new InvalidOperationException("SetJSRuntime must be called before using this object.");

        return moduleTask ??= InitializeModule();

        async Task<IJSObjectReference> InitializeModule()
        {
            try
            {
                var jsObjectReference = await jsRuntime.InvokeAsync<IJSObjectReference>("import", ModuleFileName);

                await OnModuleLoaded(jsObjectReference).ConfigureAwait(false);

                return jsObjectReference;
            }
            catch (Exception exc)
            {
                Console.WriteLine($"InitializeModule, {exc.Message}", exc);
                throw;
            }

        }
    }

    /// <summary>
    /// Called after the JS <see cref="Module"/> has been loaded.
    /// </summary>
    /// <param name="jsObjectReference">The loaded JS module reference.</param>
    protected virtual ValueTask OnModuleLoaded(IJSObjectReference jsObjectReference)
        => ValueTask.CompletedTask;



}
