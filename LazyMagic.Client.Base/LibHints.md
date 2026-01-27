# LazyMagic.Client.Base Library Hints

This document provides guidance for working with the LazyMagic.Client.Base library.

## Overview

LazyMagic.Client.Base is a foundational library for client applications in the LazyMagic ecosystem. It provides:
- Host configuration management
- OIDC authentication configuration
- Multi-language/multi-tenancy message localization
- JavaScript interop utilities
- Network connectivity monitoring

**Target Framework:** net8.0
**Dependencies:** ReactiveUI, ReactiveUI.Fody, Microsoft.Extensions.Logging, LazyMagic.Shared

---

## Project Structure

```
LazyMagic.Client.Base/
├── ClientConfig/           # OIDC and application configuration
├── Messages/               # Localization system
├── Utilities/              # JS interop, notifications, static assets
├── IConnectivityService.cs # Network monitoring
├── Creds.cs                # AWS credential storage
└── GlobalUsing.cs          # Common namespace imports
```

---

## Key Interfaces & Classes

### Host Configuration

#### ILzHost / LzHost
**Location:** `Utilities/LzHost.cs`

Stores host information for the application. Configured by `indexinit.js` and `Program.cs`.

**Key Properties:**
| Property | Type | Description |
|----------|------|-------------|
| `AppPath` | string | App path on cloud (e.g., "/myapp") |
| `AppUrl` | string | URL application is served from |
| `RemoteApiUrl` | string | Cloud service API URL |
| `LocalApiUrl` | string | Localhost API URL |
| `AssetsUrl` | string | Static assets URL |
| `AuthConfigName` | string? | Selected auth configuration name |
| `ClientId` | string? | Optional OIDC client ID override |
| `IsMAUI` | bool | Running as MAUI app |
| `IsWASM` | bool | Running as WASM (computed: !IsMAUI) |
| `UseLocalhostApi` | bool | Use localhost instead of remote API |

**Key Methods:**
```csharp
string GetApiUrl(string path)    // Returns full API URL based on UseLocalhostApi
string GetAssetsUrl(string path) // Returns full asset URL
```

---

### OIDC Configuration

#### IOidcConfig / OidcConfig
**Location:** `ClientConfig/IOidcConfig.cs`, `ClientConfig/OidcConfig.cs`

Base OIDC configuration supporting multiple providers (Cognito, Auth0, Okta, Azure AD, Keycloak).

**Key Properties:**
| Property | Type | Description |
|----------|------|-------------|
| `AuthConfigs` | Dictionary<string, JObject> | Named auth configurations |
| `EventsApis` | Dictionary<string, JObject> | WebSocket event API configs |
| `SelectedAuthConfig` | string | Currently selected config name |
| `IsConfigured` | bool | True if AuthConfigs has entries |

**Why JObject?** Flexibility to support different OIDC providers without strongly-typed configs.

#### ILzClientConfig / LzClientConfig
**Location:** `ClientConfig/ILzClientConfig.cs`, `ClientConfig/LzClientConfig.cs`

Extended configuration with multi-tenancy support. Inherits from IOidcConfig.

**Additional Properties:**
| Property | Type | Description |
|----------|------|-------------|
| `TenancyConfig` | JObject | Merged tenancy configuration |
| `TenantKey` | string | Tenant identifier |
| `Region` | string | AWS region |
| `Configured` | bool | Configuration loaded successfully |
| `ConfigError` | string | Error message if failed |

**Configuration Loading (InitializeAsync):**
Loads and merges configuration from multiple levels (lowest to highest precedence):
1. `{hostUrl}config` - Auth config
2. `{hostUrl}system/base/AdminApp/config.json` - System base
3. `{hostUrl}tenancy/base/System/config.json` - Tenancy base
4. `{hostUrl}subtenancy/base/System/config.json` - Sub-tenancy (if 3+ domain parts)

---

### Message Localization System

#### ILzMessages / LzMessages
**Location:** `Messages/ILzMessages.cs`, `Messages/LzMessages.cs`

Multi-language, multi-tenancy message management with unit conversion support.

**Key Properties:**
| Property | Type | Description |
|----------|------|-------------|
| `Culture` | string | Current culture (e.g., "en-US") |
| `Units` | LzMessageUnits | Imperial or Metric |
| `MessageSet` | LzMessageSet | Current active message set |
| `UseInspect` | bool | Enable editing inspection mode |
| `RefreshCount` | int | Incremented to trigger UI refresh |

**Key Methods:**
```csharp
Task SetMessageSetAsync(string culture, LzMessageUnits units)  // Load message set
string Msg(string key)                                          // Get localized message
string Img(string key)                                          // Get image URL
MsgItemsModel MsgItemsModel(string key)                        // Get editable model
Task SaveMessageSetsAsync()                                     // Save dirty messages
```

#### LzMessageSet
**Location:** `Messages/LzMessageSet.cs`

Single culture/unit message set with variable substitution and unit conversion.

**Message Processing Features:**

1. **Variable Substitution:** `__keyName__` replaced with message value
2. **Unit Conversion:** `@Unit(value, unit)` or `@Unit(value, unit, precision, targetUnit)`
3. **Unit Selection:** `@UnitS(imperialText, metricText)`

**Supported Units:**
- Length: in, ft, yd, mi ↔ mm, cm, m, km
- Weight: oz, lb ↔ g, kg
- Area: sq in, sq ft ↔ sq cm, sq m

**Example:**
```
// In message file:
"room_size": "Room is @Unit(10, ft) x @Unit(12, ft)"

// When Units = Metric, outputs:
"Room is 3.1 m x 3.7 m"
```

---

### Connectivity Monitoring

#### IConnectivityService
**Location:** `IConnectivityService.cs`

Network connectivity awareness for online/offline handling.

**Key Properties:**
| Property | Type | Description |
|----------|------|-------------|
| `IsOnline` | bool | Current connectivity status |
| `IsPollingEnabled` | bool | Polling active |

**Key Methods:**
```csharp
Task<bool> CheckInternetConnectivityAsync()
Task<bool> ShouldMakeNetworkRequestAsync()
Task SetPollingEnabledAsync(bool enabled)
void OnConnectivityChanged(bool isOnline)
```

---

### JavaScript Interop

#### ILzJsUtilities
**Location:** `Utilities/ILzJsUtilities.cs`

Comprehensive JS interop for browser APIs.

**Categories:**

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
ValueTask SetCookie(string key, string value, CookieOptions options)
ValueTask<string> GetCookie(string key)
ValueTask DeleteCookie(string key, CookieOptions options)
ValueTask SetJSONCookie<T>(string name, T obj, CookieOptions options)
ValueTask<T> GetJSONCookie<T>(string name)
```

**Fast Auth Cache (PWA):**
```csharp
ValueTask<bool> InitializeFastAuth()
ValueTask<string> GetCachedAuthStateAsync()
ValueTask SetCachedAuthStateAsync(string json, int timeoutMinutes)
ValueTask ClearAuthCacheAsync()
```

**Sharing (Web Share API):**
```csharp
ValueTask<bool> SharePng(string title, string text, byte[] pngData, string textData)
ValueTask<bool> ShareText(string title, string text)
```

---

### Static Assets

#### IStaticAssets
**Location:** `Utilities/IStaticAssets.cs`

Abstraction for loading static assets from various sources (HTTP, local, S3, service worker).

```csharp
Task<string> ReadAuthConfigAsync(string filepath)
Task<string> ReadTenancyConfigAsync(string filepath)
Task<string> ReadContentAsync(string filepath)
```

---

### Base Classes

#### NotifyBase
**Location:** `Utilities/NotifyBase.cs`

Lightweight INotifyPropertyChanged implementation without ReactiveUI dependency.

```csharp
bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = "")
bool SetProperty<T>(ref T storage, T value, Action onChanged, [CallerMemberName] string propertyName = "")
void RaisePropertyChanged([CallerMemberName] string propertyName = "")
```

---

## Usage Patterns

### Typical Initialization (Program.cs)

```csharp
// 1. Register ILzHost
builder.Services.TryAddScoped<ILzHost>(sp => new LzHost(
    appPath: appConfig["appPath"],
    appUrl: appConfig["appUrl"],
    remoteApiUrl: appConfig["remoteApiUrl"],
    localApiUrl: appConfig["localApiUrl"],
    assetsUrl: appConfig["assetsUrl"],
    authConfigName: appConfig["authConfigName"],
    clientId: appConfig["clientId"],  // Optional override
    isMAUI: false,
    useLocalhostApi: useLocalhostApi
));

// 2. Register IStaticAssets
builder.Services.TryAddScoped<IStaticAssets>(sp => new BlazorStaticAssets(...));

// 3. Register ILzMessages (typically auto-registered)
builder.Services.TryAddScoped<ILzMessages, LzMessages>();
```

### Using Messages in Components

```csharp
@inject ILzMessages Messages

@code {
    protected override async Task OnInitializedAsync()
    {
        Messages.MessageFiles = new List<string> {
            "system/{culture}/System/BaseMessages.json",
            "system/{culture}/System/AppMessages.json"
        };
        await Messages.SetMessageSetAsync("en-US", LzMessageUnits.Imperial);
    }
}

<p>@Messages.Msg("welcome_message")</p>
<p>@Messages.Msg("room_dimensions")</p>  <!-- With unit conversion -->
```

### Using Connectivity Service

```csharp
@inject IConnectivityService Connectivity

<button disabled="@(!Connectivity.IsOnline)" @onclick="CallApi">
    @Messages.Msg("submit_button")
</button>

@code {
    private async Task CallApi()
    {
        if (await Connectivity.ShouldMakeNetworkRequestAsync())
        {
            // Make API call
        }
    }
}
```

### Loading Configuration

```csharp
var config = new LzClientConfig(host, httpClient);
await config.InitializeAsync(host.RemoteApiUrl);

// Access auth configs
var authConfig = config.GetCurrentAuthConfig();

// Access tenancy config
var tenantKey = config.TenantKey;
```

---

## Key Design Decisions

1. **JObject for OIDC Config** - Supports multiple providers without strongly-typed configs
2. **Multi-Level Config Merging** - System → Tenancy → SubTenancy hierarchy
3. **Lazy Unit Loading** - Only processes requested units on-demand
4. **Message File Precedence** - Later files override earlier files for same keys
5. **NotifyBase vs ReactiveObject** - Lightweight option without full ReactiveUI dependency
6. **Async-First** - All I/O uses async/await with ValueTask optimization

---

## Common Pitfalls

1. **indexinit.js must copy new properties** - When adding properties to appConfig.js, also add them to window.appConfig in indexinit.js
2. **Message files must be accessible** - Ensure static assets are properly served
3. **Culture placeholders** - Use `{culture}` in MessageFiles paths
4. **Unit conversion patterns** - Must match exactly: `@Unit(value, unit)` or `@UnitS(imperial, metric)`
5. **Dirty tracking** - Call SaveMessageSetsAsync() to persist edited messages
