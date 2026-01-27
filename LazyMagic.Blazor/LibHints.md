# LazyMagic.Blazor Library Hints

This document provides guidance for working with the LazyMagic.Blazor library.

## Overview

LazyMagic.Blazor is a Blazor component library providing:
- ReactiveUI-integrated component base classes
- JavaScript interop utilities and module pattern
- Network connectivity monitoring
- Browser fingerprinting and clipboard services
- Static asset management
- Loading and fade UI components

**Target Framework:** net9.0
**Dependencies:** ReactiveUI.Blazor, BlazorPro.BlazorSize, Microsoft.AspNetCore.Components.WebAssembly.Authentication, LazyMagic.Client.Base, LazyMagic.OIDC.Base

---

## Project Structure

```
LazyMagic.Blazor/
├── Components/
│   ├── Base/               # Component base classes
│   │   ├── ILzBaseJSModule.cs
│   │   ├── LzBaseJSModule.cs
│   │   ├── LzComponentBase.cs
│   │   ├── LzComponentBaseInjectViewModel.cs
│   │   ├── LzComponentBaseAssignViewModel.cs
│   │   ├── LzComponentBasePassViewModel.cs
│   │   ├── LzLayoutComponentBase.cs
│   │   └── LzLayoutComponentBase*.cs
│   ├── LzAppLoading.razor       # Animated loading component
│   ├── WindowFade.razor         # Fade effect on window resize
│   ├── RedirectToLogin.razor    # Authentication redirect
│   ├── Msg.cs                   # Localized message component
│   ├── ClipboardService.cs      # Clipboard operations
│   └── BlazorContentAccess.cs   # JS interop content access
├── Config/
│   └── ConfigureLazyMagicBlazorMessages.cs
├── wwwroot/
│   ├── connectivityService.js   # Connectivity detection
│   ├── connectivityManager.js   # Blazor connectivity bridge
│   ├── lzJsUtilities.js         # General JS utilities
│   ├── browserFingerprint.js    # Device fingerprinting
│   ├── blazorContentAccess.js   # Content fetching
│   └── lzAppLoading.js          # Loading animation
├── ConnectivityService.cs       # Network monitoring
├── LzJsUtilities.cs             # JS interop wrapper
├── BrowserFingerprintService.cs # Browser detection
├── BlazorStaticAssets.cs        # Static asset loading
├── BlazorOSAccess.cs            # OS-level file access
└── GlobalUsing.cs               # Common namespace imports
```

---

## Component Base Classes

### LzComponentBase
**Location:** `Components/Base/LzComponentBase.cs`

Base component providing ILzMessages integration for localization.

**Key Properties/Methods:**
```csharp
[Inject]
public virtual ILzMessages? Messages { get; set; }

protected virtual MarkupString Msg(string key, bool ignoreUseInspect = false)
protected virtual string Img(string key, bool ignoreUseInspect = false)
```

### LzComponentBase<T>
**Location:** `Components/Base/LzComponentBase.cs`

Generic component base with ReactiveUI integration for ViewModel binding.

**Implements:** `IViewFor<T>`, `INotifyPropertyChanged`, `ICanActivate`, `IDisposable`

**Key Features:**
- Automatic StateHasChanged on ViewModel property changes
- Automatic StateHasChanged on Messages property changes
- Activation/Deactivation observables
- Proper subscription disposal

**Key Properties:**
| Property | Type | Description |
|----------|------|-------------|
| `ViewModel` | T? | The bound ViewModel (Parameter) |
| `Activated` | IObservable<Unit> | Fires on component initialization |
| `Deactivated` | IObservable<Unit> | Fires on component disposal |

### Component Variants

| Class | Description |
|-------|-------------|
| `LzComponentBaseInjectViewModel<T>` | ViewModel injected via DI |
| `LzComponentBaseAssignViewModel<T>` | ViewModel assigned in OnInitializedAsync (throws if null) |
| `LzComponentBasePassViewModel<T>` | ViewModel passed as Parameter |
| `LzLayoutComponentBase` | Layout version of LzComponentBase |
| `LzLayoutComponentBase<T>` | Layout version with ViewModel binding |
| `LzLayoutComponentBaseInjectViewModel<T>` | Layout with DI-injected ViewModel |
| `LzLayoutComponentBaseAssignViewModel<T>` | Layout with assigned ViewModel |
| `LzLayoutComponentBasePassViewModel<T>` | Layout with passed ViewModel |

---

## JavaScript Interop Pattern

### ILzBaseJSModule / LzBaseJSModule
**Location:** `Components/Base/ILzBaseJSModule.cs`, `Components/Base/LzBaseJSModule.cs`

Abstract base class for JavaScript module wrappers with safe invocation and proper disposal.

**Key Properties:**
| Property | Type | Description |
|----------|------|-------------|
| `ModuleFileName` | string | Path to the JS module file (abstract) |
| `Module` | Task<IJSObjectReference> | Lazy-loaded JS module reference |
| `JSRuntime` | IJSRuntime | The JS runtime instance |
| `AsyncDisposed` | bool | Whether the module has been disposed |
| `IsUnsafe` | bool | True if module is disposed or null |

**Key Methods:**
```csharp
void SetJSRuntime(object jsRuntime)
void SetLogger(ILogger logger)
ValueTask InvokeVoidAsync(string identifier, params object[] args)
ValueTask<TValue> InvokeAsync<TValue>(string identifier, params object[] args)
ValueTask InvokeSafeVoidAsync(string identifier, params object[] args)  // Safe, catches exceptions
ValueTask<TValue> InvokeSafeAsync<TValue>(string identifier, params object[] args)  // Safe, returns default
ValueTask OnModuleLoaded(IJSObjectReference jsObjectReference)  // Override for post-load setup
```

**Usage Pattern:**
```csharp
public class MyJsService : LzBaseJSModule
{
    public override string ModuleFileName => "./_content/MyPackage/myScript.js";

    public async ValueTask DoSomething()
        => await InvokeSafeVoidAsync("doSomething");
}
```

---

## Services

### ConnectivityService
**Location:** `ConnectivityService.cs`

Monitors network connectivity with JavaScript-based detection.

**Implements:** `IConnectivityService`, `IAsyncDisposable`

**Key Properties:**
| Property | Type | Description |
|----------|------|-------------|
| `IsOnline` | bool | Current connectivity status |
| `IsPollingEnabled` | bool | Whether periodic polling is active |

**Key Methods:**
```csharp
Task InitializeAsync(IJSRuntime jsRuntime)
Task<bool> CheckInternetConnectivityAsync()
Task<bool> ShouldMakeNetworkRequestAsync()
Task SetPollingEnabledAsync(bool enabled)

[JSInvokable]
Task OnConnectivityChanged(bool isOnline)  // Called from JS
```

**JavaScript Integration:**
- `connectivityService.js` - Core connectivity detection logic
- `connectivityManager.js` - Blazor bridge with global functions

### LzJsUtilities
**Location:** `LzJsUtilities.cs`

Comprehensive JavaScript interop utility service.

**Implements:** `ILzJsUtilities`

**Categories of Methods:**

**Asset Management:**
```csharp
ValueTask CheckForNewAssetData()
ValueTask Reload()
ValueTask<int> GetMemory()
```

**Local Storage:**
```csharp
ValueTask SetItem(string key, string value)
ValueTask<string> GetItem(string key)
ValueTask RemoveItem(string key)
```

**Cookie Management:**
```csharp
ValueTask SetCookie(string name, string value, CookieOptions options = null)
ValueTask<string> GetCookie(string name)
ValueTask DeleteCookie(string name, CookieOptions options = null)
ValueTask<bool> CookieExists(string name)
ValueTask<Dictionary<string, string>> GetAllCookies()
ValueTask ClearAllCookies()
ValueTask SetJSONCookie<T>(string name, T obj, CookieOptions options = null)
ValueTask<T> GetJSONCookie<T>(string name)
```

**Fast Auth Cache (PWA):**
```csharp
ValueTask<bool> InitializeFastAuth()
ValueTask<string> GetCachedAuthStateAsync()
ValueTask SetCachedAuthStateAsync(string json, int timeoutMinutes)
ValueTask ClearAuthCacheAsync()
ValueTask<bool> IsAuthCacheValidAsync()
ValueTask<string> GetStoredTokensAsync()
```

**Image & Sharing:**
```csharp
ValueTask<string> GetBase64Image(object elementRef)
ValueTask<string> GetBase64ImageDownsized(object elementRef)
ValueTask<bool> SharePng(string title, string text, string pngData, string? textData)
ValueTask<bool> ShareText(string title, string text)
```

**Observable Properties:**
| Property | Type | Description |
|----------|------|-------------|
| `CheckingAssetData` | bool | Asset check in progress |
| `UpdatingAssetData` | bool | Asset update in progress |
| `UpdatingServiceWorker` | bool | SW update in progress |
| `CacheMiss` | string | Last cache miss URL |

### BrowserFingerprintService
**Location:** `BrowserFingerprintService.cs`

Gathers browser and device information for identification.

**Key Methods:**
```csharp
ValueTask<BrowserFingerprint> GetFingerprintAsync()  // Get all info at once
ValueTask<string> GetUserAgentAsync()
ValueTask<string> GetDeviceAsync()
ValueTask<string> GetDeviceTypeAsync()
ValueTask<string> GetDeviceVendorAsync()
ValueTask<bool> IsMobileAsync()
ValueTask<bool> IsChromeAsync()
ValueTask<bool> IsFirefoxAsync()
ValueTask<bool> IsSafariAsync()
ValueTask<bool> IsIEAsync()
ValueTask<string> GetLanguageAsync()
ValueTask<string> GetTimeZoneAsync()
ValueTask<string> GetScreenPrintAsync()
```

### ClipboardService
**Location:** `Components/ClipboardService.cs`

Simple clipboard read/write operations.

```csharp
ValueTask<string> ReadTextAsync()
ValueTask WriteTextAsync(string text)
```

### BlazorStaticAssets
**Location:** `BlazorStaticAssets.cs`

**Implements:** `IStaticAssets`

HTTP-based static asset loading for Blazor WASM.

```csharp
Task<string> ReadAuthConfigAsync(string url)
Task<string> ReadTenancyConfigAsync(string url)
Task<string> ReadContentAsync(string url)
Task<string> HttpReadAsync(string url)
```

### BlazorOSAccess
**Location:** `BlazorOSAccess.cs`

**Implements:** `IOSAccess`

OS-level file access abstraction (partially implemented for Blazor, supports MAUI hybrid).

---

## UI Components

### LzAppLoading
**Location:** `Components/LzAppLoading.razor`

Animated loading progress indicator with SVG circles.

**Parameters:**
| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `AnimationDurationSeconds` | double | 2.0 | Animation duration |
| `LoadingMessage` | string | "Loading..." | Text displayed |

**Public Methods:**
```csharp
Task SetPercentage(double percentage)
Task AnimateToCompletion()
void AccelerateCompletion(double accelerationFactor)
```

### WindowFade
**Location:** `Components/WindowFade.razor`

Wrapper component that fades content during window resize to prevent visual glitches.

**Parameters:**
| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `ChildContent` | RenderFragment? | null | Content to wrap |
| `FadeDuration` | int | 250 | Fade duration in ms |

**Usage:**
```razor
<WindowFade FadeDuration="300">
    <YourContent />
</WindowFade>
```

### RedirectToLogin
**Location:** `Components/RedirectToLogin.razor`

Simple component that redirects to the login page on initialization.

### Msg
**Location:** `Components/Msg.cs`

Displays localized messages using ILzMessages.

**Parameters:**
| Parameter | Type | Description |
|-----------|------|-------------|
| `Key` | string | Message key to display |

**Usage:**
```razor
<Msg Key="welcome_message" />
```

---

## Configuration

### ConfigureLazyMagicBlazorMessages
**Location:** `Config/ConfigureLazyMagicBlazorMessages.cs`

Configures default message files for the localization system.

```csharp
ILzMessages lzMessages = // ...
lzMessages.AddLazyMagicBlazorMessages();
// Adds: system/{culture}/System/AuthMessages.json
// Adds: system/{culture}/System/BaseMessages.json
```

---

## Usage Patterns

### Basic Component with ViewModel Injection

```csharp
@inherits LzComponentBaseInjectViewModel<MyViewModel>

<div>@ViewModel?.Name</div>
<button @onclick="() => ViewModel?.DoSomething()">Click</button>
```

### Component with Passed ViewModel

```csharp
@inherits LzComponentBasePassViewModel<MyViewModel>

<div>@ViewModel?.Status</div>

@code {
    // ViewModel is passed as a Parameter from parent
}
```

### Component with Assigned ViewModel

```csharp
@inherits LzComponentBaseAssignViewModel<MyViewModel>

@code {
    [Inject] private IMyViewModelFactory Factory { get; set; }

    protected override async Task OnInitializedAsync()
    {
        ViewModel = Factory.Create();  // Must assign before base call
        await base.OnInitializedAsync();
    }
}
```

### Using ConnectivityService

```csharp
@inject IConnectivityService Connectivity
@inject IJSRuntime JS

@code {
    protected override async Task OnInitializedAsync()
    {
        await Connectivity.InitializeAsync(JS);
    }

    private async Task MakeApiCall()
    {
        if (await Connectivity.ShouldMakeNetworkRequestAsync())
        {
            // Safe to make network request
        }
    }
}
```

### Using LzJsUtilities

```csharp
@inject ILzJsUtilities JsUtils
@inject IJSRuntime JS

@code {
    protected override async Task OnInitializedAsync()
    {
        JsUtils.SetJSRuntime(JS);
        await JsUtils.Initialize();
    }

    private async Task SaveToStorage()
    {
        await JsUtils.SetItem("key", "value");
        await JsUtils.SetCookie("session", "data", new CookieOptions { Expires = 7 });
    }
}
```

### Creating Custom JS Module Service

```csharp
public class MyCustomJsService : LzBaseJSModule
{
    public override string ModuleFileName => "./_content/MyPackage/myModule.js";

    public MyCustomJsService(ILogger<MyCustomJsService>? logger = null)
    {
        _logger = logger;
    }

    public async ValueTask<string> DoCustomWork(string input)
        => await InvokeSafeAsync<string>("customFunction", input);

    protected override ValueTask OnModuleLoaded(IJSObjectReference jsObjectReference)
    {
        // Post-load initialization
        return base.OnModuleLoaded(jsObjectReference);
    }
}
```

---

## Key Design Decisions

1. **ReactiveUI Integration** - Component base classes integrate deeply with ReactiveUI for automatic UI updates on property changes
2. **Safe JS Invocation** - InvokeSafeAsync/InvokeSafeVoidAsync catch common exceptions (JSDisconnectedException, ObjectDisposedException, TaskCanceledException)
3. **Lazy Module Loading** - JS modules are loaded on first access via the Module property
4. **Connectivity Polling** - Uses periodic HEAD requests to detect true connectivity (not just navigator.onLine)
5. **Proper Disposal** - All services implement IAsyncDisposable with proper cleanup of JS resources
6. **MAUI Hybrid Support** - BlazorOSAccess and BlazorContentAccess support both WASM and MAUI hybrid scenarios

---

## Common Pitfalls

1. **SetJSRuntime must be called first** - LzBaseJSModule requires SetJSRuntime() before any JS calls
2. **InitializeAsync for ConnectivityService** - Must call InitializeAsync before using connectivity methods
3. **Subscription disposal** - Component base classes handle this, but custom subscriptions need manual disposal
4. **AfterRender for ReactiveUI** - ReactiveUI subscriptions are set up in OnAfterRender to avoid conflicts with certain JS frameworks
5. **Module path format** - Use `./_content/PackageName/file.js` format for static web assets
6. **StateHasChanged from subscriptions** - Always wrap in InvokeAsync when calling from ReactiveUI subscriptions

---

## JavaScript Files Reference

| File | Purpose |
|------|---------|
| `connectivityService.js` | Core connectivity detection with polling |
| `connectivityManager.js` | Blazor interop bridge for connectivity |
| `lzJsUtilities.js` | General utilities (storage, cookies, sharing) |
| `browserFingerprint.js` | Browser/device detection |
| `blazorContentAccess.js` | Content fetching via fetch() |
| `lzAppLoading.js` | Loading animation control |
| `lzauth.js` | Authentication utilities |
