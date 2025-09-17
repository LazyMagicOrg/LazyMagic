using System.Runtime.CompilerServices;

namespace LazyMagic.Blazor;


public class LzJsUtilities : LzBaseJSModule, ILzJsUtilities
{
    private DotNetObjectReference<LzJsUtilities> viewerInstance;

    
    // ModuleFileName is the path to the JS file that will be loaded by the Blazor app.
    public override string ModuleFileName => $"./_content/LazyMagic.Blazor/lzJsUtilities.js";
    
    public LzJsUtilities(ILogger<LzJsUtilities>? logger = null)
    {
        _logger = logger;
    }

    private bool _checkingAssetData;
    public bool CheckingAssetData
    {
        get => _checkingAssetData;
        set => this.RaiseAndSetIfChanged(ref _checkingAssetData, value);
    }
    private bool _updatingAssetData;
    public bool UpdatingAssetData
    {
        get => _updatingAssetData;
        set => this.RaiseAndSetIfChanged(ref _updatingAssetData, value);
    }
    private bool _updatingServiceWorker;
    public bool UpdatingServiceWorker
    {
        get => _updatingServiceWorker;
        set => this.RaiseAndSetIfChanged(ref _updatingServiceWorker, value);
    }
    private string _cacheMiss = "";
    public string CacheMiss
    {
        get => _cacheMiss;
        set => this.RaiseAndSetIfChanged(ref _cacheMiss, value);
    }
    public override void SetJSRuntime(object jsRuntime)
    {
        base.SetJSRuntime(jsRuntime);
        viewerInstance = DotNetObjectReference.Create(this);
    }
    
    public override void SetLogger(ILogger logger)
    {
        base.SetLogger(logger);
        _logger = logger as ILogger<LzJsUtilities>;
    }

    public virtual async ValueTask Initialize()
        => await InvokeSafeVoidAsync("initialize", viewerInstance);
    public virtual async ValueTask CheckForNewAssetData()
        => await InvokeSafeVoidAsync("checkForNewAssetData");
    public async ValueTask Reload()
        => await InvokeSafeVoidAsync("reload");
    public virtual async ValueTask<int> GetMemory()
        => await InvokeSafeAsync<int>("getMemory");
    public virtual ValueTask SetPointerCapture(object elementRef, long pointerId)
        => InvokeSafeVoidAsync("setPointerCapture", (ElementReference)elementRef, pointerId);
    public virtual async ValueTask<string> GetBase64Image(object img)
        => await InvokeSafeAsync<string>("getBase64Image", (ElementReference)img);
    public virtual async ValueTask<string> GetBase64ImageDownsized(object img)
        => await InvokeSafeAsync<string>("getBase64ImageDownsized", (ElementReference)img);
    public virtual async ValueTask<bool> SharePng(string title, string text, string pngData, string? textData = null)
        => await InvokeSafeAsync<bool>("sharePng", title, text, pngData, textData);
    public virtual async ValueTask<bool> ShareText(string title, string text)
        => await InvokeSafeAsync<bool>("shareText", title, text);
    public async ValueTask SetItem(string key, string value)
        => await InvokeSafeVoidAsync("localStorageSet", key, value);
    public async ValueTask<string> GetItem(string key)
        => await InvokeSafeAsync<string>("localStorageGetItem", key);
    public async ValueTask RemoveItem(string key)
        => await InvokeSafeVoidAsync("localStorageRemoveItem", key);


    // Cookie Management Methods
    public virtual async ValueTask SetCookie(string name, string value, CookieOptions options = null)
    {
        if (options == null)
        {
            await InvokeSafeVoidAsync("setCookie", name, value);
        }
        else
        {
            await InvokeSafeVoidAsync("setCookie", name, value, options);
        }
    }

    public virtual async ValueTask<string> GetCookie(string name)
        => await InvokeSafeAsync<string>("getCookie", name);

    public virtual async ValueTask DeleteCookie(string name, CookieOptions options = null)
    {
        if (options == null)
        {
            await InvokeSafeVoidAsync("deleteCookie", name);
        }
        else
        {
            await InvokeSafeVoidAsync("deleteCookie", name, options);
        }
    }

    public virtual async ValueTask<bool> CookieExists(string name)
        => await InvokeSafeAsync<bool>("cookieExists", name);

    public virtual async ValueTask<Dictionary<string, string>> GetAllCookies()
        => await InvokeSafeAsync<Dictionary<string, string>>("getAllCookies");

    public virtual async ValueTask ClearAllCookies()
        => await InvokeSafeVoidAsync("clearAllCookies");

    public virtual async ValueTask SetJSONCookie<T>(string name, T obj, CookieOptions options = null)
    {
        if (options == null)
        {
            await InvokeSafeVoidAsync("setJSONCookie", name, obj);
        }
        else
        {
            await InvokeSafeVoidAsync("setJSONCookie", name, obj, options);
        }
    }

    public virtual async ValueTask<T> GetJSONCookie<T>(string name)
        => await InvokeSafeAsync<T>("getJSONCookie", name);

    // Callbacks. ie. [JSInvokable]
    [JSInvokable]
    public void AssetDataCheckStarted()
    {
        _logger?.LogInformation("[AssetDataCheckStarted][{Timestamp}] Asset data check started", DateTime.UtcNow.ToString("HH:mm:ss.fff"));
        CheckingAssetData = true;
    }
    [JSInvokable]
    public void AssetDataCheckComplete()
    {
        _logger?.LogInformation("[AssetDataCheckComplete][{Timestamp}] Asset data check complete", DateTime.UtcNow.ToString("HH:mm:ss.fff"));
        CheckingAssetData = false;
    }
    [JSInvokable]
    public void AssetDataUpdateStarted()
    {
        _logger?.LogInformation("[AssetDataUpdateStarted][{Timestamp}] Asset data update started", DateTime.UtcNow.ToString("HH:mm:ss.fff"));
        UpdatingAssetData = true;
    }
    [JSInvokable]
    public void AssetDataUpdateComplete()
    {
        _logger?.LogInformation("[AssetDataUpdateComplete][{Timestamp}] Asset data update complete", DateTime.UtcNow.ToString("HH:mm:ss.fff"));
        UpdatingAssetData = false;
    }
    [JSInvokable]
    public void ServiceWorkerUpdateStarted()
    {
        _logger?.LogInformation("[ServiceWorkerUpdateStarted][{Timestamp}] Service worker update started", DateTime.UtcNow.ToString("HH:mm:ss.fff"));
        UpdatingServiceWorker = true;
    }
    [JSInvokable]
    public void ServiceWorkerUpdateComplete()
    {
        _logger?.LogInformation("[ServiceWorkerUpdateComplete][{Timestamp}] Service worker update complete", DateTime.UtcNow.ToString("HH:mm:ss.fff"));
        UpdatingServiceWorker = false;
    }
    [JSInvokable]
    public void CacheMissAction(string url)
    {
        _logger?.LogWarning("[CacheMissAction][{Timestamp}] Cache miss for URL: {Url}", DateTime.UtcNow.ToString("HH:mm:ss.fff"), url);
        CacheMiss = url;
    }

    [JSInvokable]
    public void MessageSelected(string key, string value)
    {
        _logger?.LogInformation("[MessageSelected][{Timestamp}] Message selected: {Key} = {Value}", DateTime.UtcNow.ToString("HH:mm:ss.fff"), key, value);
    }

    protected bool RaiseAndSetIfChanged<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
