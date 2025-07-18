using Microsoft.JSInterop;

namespace LazyMagic.Blazor;

/// <summary>
/// This class provides general JavaScript utilities that can be used in Blazor applications.
/// This class can be registered as scoped DI service and then injected into Blazor
/// components for use.
/// </summary>
public class LzJsUtilities : LzBaseJSModule
{
    // ModuleFileName is the path to the JS file that will be loaded by the Blazor app.
    public override string ModuleFileName => $"./_content/LazyMagic.Blazor/lzJsUtilities.js";




}
