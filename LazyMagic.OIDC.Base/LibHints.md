# LazyMagic.OIDC.Base Library Hints

This document provides guidance for working with the LazyMagic.OIDC.Base library.

## Overview

LazyMagic.OIDC.Base is a platform-agnostic OIDC authentication library that provides:
- Multi-provider OIDC support (Cognito, Auth0, Okta, Azure AD, Keycloak)
- OpenID Connect discovery document handling
- Dynamic configuration loading from server endpoints
- Token refresh management with automatic renewal
- Remember-me functionality with client-side token storage
- Provider-specific logout URL construction
- UI-agnostic ViewModel base classes for authentication

**Target Framework:** net9.0
**Dependencies:** Microsoft.Extensions.Logging, Newtonsoft.Json, LazyMagic.Client.Base

---

## Project Structure

```
LazyMagic.OIDC.Base/
├── Interfaces/
│   ├── IOIDCService.cs              # Core OIDC service interface
│   ├── IDynamicConfigurationProvider.cs  # Dynamic config access
│   ├── IRememberMeService.cs        # Token persistence
│   └── IProfileManagementService.cs # Profile management
├── Config/
│   ├── LazyOidcConfig.cs            # Lazy-loading OIDC config
│   ├── DynamicConfigurationProvider.cs  # Provider-specific config
│   └── ConfigureLazyMagicOIDCBase.cs    # DI registration
├── Services/
│   ├── OpenIdDiscoveryService.cs    # Discovery document fetching
│   ├── TokenRefreshService.cs       # Automatic token renewal
│   ├── DynamicOidcConfigurationService.cs  # Config loading utility
│   └── BearerTokenHandler.cs        # HTTP bearer token handler
├── Models/
│   ├── OidcOptionsConfiguration.cs  # OIDC options model
│   └── UserProfile.cs               # User profile model
├── ViewModels/
│   ├── ViewModelBase.cs             # Auth-aware ViewModel base
│   ├── UserProfileViewModel.cs      # Example profile ViewModel
│   └── RelayCommand.cs              # ICommand implementation
└── GlobalUsing.cs                   # Common namespace imports
```

---

## Core Interfaces

### IOIDCService
**Location:** `Interfaces/IOIDCService.cs`

Main OIDC service interface abstracting authentication operations.

**Key Methods:**
```csharp
Task<OIDCAuthenticationState> GetAuthenticationStateAsync()
Task<ClaimsPrincipal?> GetCurrentUserAsync()
Task<bool> IsAuthenticatedAsync()
Task<string?> GetAccessTokenAsync()
Task<IEnumerable<Claim>> GetUserClaimsAsync()
Task<string?> GetClaimValueAsync(string claimType)
Task<bool> IsInRoleAsync(string role)
Task<bool> LoginAsync()
Task LogoutAsync()
```

**Events:**
| Event | Type | Description |
|-------|------|-------------|
| `AuthenticationStateChanged` | EventHandler<OIDCAuthenticationStateChangedEventArgs> | Fires on auth state change |
| `OnAuthenticationRequested` | Action<string>? | Optional UI handling for auth requests |

**OIDCAuthenticationState:**
```csharp
public class OIDCAuthenticationState
{
    public bool IsAuthenticated { get; set; }
    public string? UserName { get; set; }
    public string? Email { get; set; }
    public DateTime? TokenExpiry { get; set; }
    public Dictionary<string, string>? Claims { get; set; }
}
```

---

### IDynamicConfigurationProvider
**Location:** `Interfaces/IDynamicConfigurationProvider.cs`

Platform-agnostic interface for accessing dynamic OIDC configuration with multi-provider support.

**Configuration Methods:**
```csharp
string? GetAuthority()           // OIDC Authority URL (issuer)
string? GetClientId()            // Client ID (with override support)
string? GetMetadataUrl()         // .well-known/openid-configuration URL
string? GetLogoutEndpoint()      // Provider logout endpoint
string? GetProviderType()        // "cognito", "auth0", "okta", "azuread", "keycloak"
string? GetValue(string key)     // Generic config value access
```

**Discovery Document Methods:**
```csharp
Task InitializeDiscoveryAsync()           // Pre-fetch discovery document
Task<string?> GetEndSessionEndpointAsync() // Get end_session_endpoint
```

**Logout URL Building:**
```csharp
string? BuildLogoutUrl(string postLogoutRedirectUri, string? idTokenHint = null)
Task<string?> BuildLogoutUrlAsync(string postLogoutRedirectUri, string? idTokenHint = null)
```

**Remember-Me Support:**
```csharp
bool HasNativeRememberMe()           // Provider has built-in remember-me
bool RequiresClientSideTokenStorage() // App must manage token persistence
bool IsPostLogoutRedirectEnabled()   // Whether to redirect after logout
```

---

### IRememberMeService
**Location:** `Interfaces/IRememberMeService.cs`

Manages Remember Me functionality and token persistence.

**Key Methods:**
```csharp
Task<bool> GetRememberMeAsync()
Task SetRememberMeAsync(bool rememberMe)
Task ClearTokensAsync()
Task<bool> HasTokensAsync()
Task InitializeAuthenticationAsync()  // Initialize on app startup
Task<string?> GetIdTokenAsync()       // For logout flows requiring id_token_hint
```

---

### ITokenRefreshService
**Location:** `Services/TokenRefreshService.cs`

Manages automatic token refresh before expiration.

**Key Methods:**
```csharp
Task StartMonitoringAsync()                    // Start monitoring expiration
void StopMonitoring()                          // Stop monitoring
Task<bool> RefreshTokensAsync()                // Manual refresh trigger
void UpdateTokenExpiration(DateTime expirationTime)  // Update expiration time
```

---

## Configuration Classes

### DynamicConfigurationProvider
**Location:** `Config/DynamicConfigurationProvider.cs`

Provides configuration values from dynamically loaded config with provider-specific handling.

**Provider Type Detection:**
Infers provider type from:
1. Explicit `providerType` field in config
2. Authority URL patterns (amazoncognito.com, auth0.com, etc.)
3. MetadataUrl patterns (/realms/ for Keycloak)
4. Cognito-specific fields (HostedUIDomain, userPoolClientId)

**Client ID Resolution:**
1. First checks `ILzHost.ClientId` for client-specified override
2. Falls back to config: `ClientId` → `userPoolClientId` → `clientId`

**Logout URL Patterns by Provider:**

| Provider | URL Pattern |
|----------|-------------|
| Cognito | `{endpoint}?client_id={clientId}&logout_uri={redirectUri}` |
| Auth0 | `{endpoint}/v2/logout?client_id={clientId}&returnTo={redirectUri}` |
| Okta | `{endpoint}?post_logout_redirect_uri={redirectUri}&id_token_hint={token}` |
| Azure AD | `{endpoint}?post_logout_redirect_uri={redirectUri}&id_token_hint={token}` |
| Keycloak | `{endpoint}?id_token_hint={token}&post_logout_redirect_uri={redirectUri}&client_id={clientId}` |
| Generic OIDC | `{endpoint}?post_logout_redirect_uri={redirectUri}&id_token_hint={token}&client_id={clientId}` |

**Remember-Me Native Support:**

| Provider | Native Support |
|----------|----------------|
| Keycloak | Yes |
| Auth0 | Yes |
| Okta | Yes |
| Azure AD | Yes |
| Cognito | No |

---

### OpenIdDiscoveryService
**Location:** `Services/OpenIdDiscoveryService.cs`

Fetches and caches OpenID Connect discovery documents.

**Features:**
- 1-hour cache expiration
- Thread-safe with SemaphoreSlim
- 10-second request timeout
- Creates own HttpClient to avoid DI scoping issues

**OpenIdDiscoveryDocument Properties:**
```csharp
string? Issuer
string? AuthorizationEndpoint
string? TokenEndpoint
string? UserinfoEndpoint
string? EndSessionEndpoint
string? JwksUri
string? RevocationEndpoint
List<string>? ScopesSupported
List<string>? ResponseTypesSupported
List<string>? GrantTypesSupported
// ... and more standard OIDC fields
```

---

### TokenRefreshServiceBase
**Location:** `Services/TokenRefreshService.cs`

Abstract base class for automatic token renewal with timer-based refresh.

**Features:**
- 5-minute buffer before expiration
- Guard against concurrent refresh attempts
- Timer-based automatic refresh scheduling

**Abstract Methods to Implement:**
```csharp
protected abstract Task<bool> PerformTokenRefreshAsync()
protected abstract Task<DateTime?> GetCurrentTokenExpirationAsync()
```

---

### OidcOptionsConfiguration
**Location:** `Models/OidcOptionsConfiguration.cs`

OIDC options model with factory method for config parsing.

**Key Method:**
```csharp
public static OidcOptionsConfiguration FromAuthConfig(JObject authConfig, string? clientIdOverride = null)
```

**Supports:**
- New format: `HostedUIDomain`, `MetadataUrl`, `ClientId`
- Old Cognito format: `awsRegion`, `userPoolId`, `userPoolClientId`, `cognitoDomain`

---

## ViewModel Base Classes

### ViewModelBase
**Location:** `ViewModels/ViewModelBase.cs`

Base ViewModel class with OIDC authentication support, UI-agnostic.

**Key Properties:**
| Property | Type | Description |
|----------|------|-------------|
| `AuthenticationState` | OIDCAuthenticationState? | Current auth state |
| `IsBusy` | bool | Operation in progress |
| `ErrorMessage` | string? | Error message for display |
| `IsAuthenticated` | bool | Convenience property |
| `UserName` | string? | Current user name |
| `UserEmail` | string? | Current user email |

**Key Methods:**
```csharp
protected async Task RefreshAuthenticationStateAsync()
protected async Task<string?> GetAccessTokenAsync()
protected async Task ExecuteWithAuthAsync(Func<Task> action, string? errorMessage = null)
protected async Task<T?> ExecuteWithAuthAsync<T>(Func<Task<T>> func, string? errorMessage = null)
protected virtual void OnAuthenticationStateChangedOverride()  // Override in derived classes
```

**Features:**
- Auto-subscribes to `AuthenticationStateChanged` event
- Implements `INotifyPropertyChanged`
- `IDisposable` with proper event unsubscription

### UserProfileViewModel
**Location:** `ViewModels/UserProfileViewModel.cs`

Example ViewModel demonstrating authentication integration.

**Commands:**
| Command | Description |
|---------|-------------|
| `LoadProfileCommand` | Load user profile |
| `SaveProfileCommand` | Save profile changes |
| `StartEditCommand` | Enter edit mode |
| `CancelEditCommand` | Cancel editing |

### RelayCommand
**Location:** `ViewModels/RelayCommand.cs`

Simple async-capable ICommand implementation.

```csharp
public RelayCommand(Func<Task> executeAsync, Func<bool>? canExecute = null)
public void RaiseCanExecuteChanged()  // Manually trigger CanExecuteChanged
```

---

## Services

### BearerTokenHandler
**Location:** `Services/BearerTokenHandler.cs`

HTTP DelegatingHandler that automatically adds Bearer tokens to requests.

```csharp
public class BearerTokenHandler : DelegatingHandler, IAuthenticationHandler
{
    public BearerTokenHandler(IOIDCService tokenService)
    public HttpMessageHandler CreateHandler()
}
```

**Usage:**
```csharp
services.AddTransient<IAuthenticationHandler, BearerTokenHandler>();
```

---

## DI Registration

### ConfigureLazyMagicOIDCBase
**Location:** `Config/ConfigureLazyMagicOIDCBase.cs`

Extension method for registering OIDC.Base services.

```csharp
public static IServiceCollection AddLazyMagicOIDCBase(this IServiceCollection services)
{
    services.TryAddScoped<DynamicOidcConfigurationService>();
    services.TryAddScoped<IAuthenticationHandler, BearerTokenHandler>();
    services.TryAddSingleton<IOpenIdDiscoveryService, OpenIdDiscoveryService>();
    return services;
}
```

---

## Usage Patterns

### Initializing OIDC Configuration

```csharp
// In Program.cs
builder.Services.AddLazyMagicOIDCBase();
builder.Services.TryAddScoped<IDynamicConfigurationProvider, DynamicConfigurationProvider>();

// Initialize discovery document during startup
var configProvider = services.GetRequiredService<IDynamicConfigurationProvider>();
await configProvider.InitializeDiscoveryAsync();
```

### Using ViewModelBase

```csharp
public class MyViewModel : ViewModelBase
{
    public MyViewModel(IOIDCService authService, ILogger<MyViewModel> logger)
        : base(authService, logger)
    {
    }

    public async Task LoadDataAsync()
    {
        await ExecuteWithAuthAsync(async () =>
        {
            var token = await GetAccessTokenAsync();
            // Use token for API calls
        }, "Failed to load data");
    }

    protected override void OnAuthenticationStateChangedOverride()
    {
        // React to auth state changes
        if (IsAuthenticated)
        {
            _ = LoadDataAsync();
        }
    }
}
```

### Building Logout URLs

```csharp
var configProvider = services.GetRequiredService<IDynamicConfigurationProvider>();

// Async version (recommended - ensures discovery document is fetched)
var logoutUrl = await configProvider.BuildLogoutUrlAsync(
    postLogoutRedirectUri: "https://myapp.com/",
    idTokenHint: idToken);  // Required for Keycloak

// Sync version (uses cached endpoint)
var logoutUrl = configProvider.BuildLogoutUrl(
    postLogoutRedirectUri: "https://myapp.com/");
```

### Client-Specified ClientId Override

```csharp
// In appConfig.js
export const appConfig = {
    clientId: "my-custom-client-id",  // Optional override
    authConfigName: "default",
    // ...
};

// In indexinit.js
window.appConfig = {
    clientId: appConfig.clientId,  // Pass through to ILzHost
    // ...
};

// DynamicConfigurationProvider checks ILzHost.ClientId first
// Falls back to server config if not specified
```

### Handling Remember-Me

```csharp
var configProvider = services.GetRequiredService<IDynamicConfigurationProvider>();

if (configProvider.RequiresClientSideTokenStorage())
{
    // Cognito - app must manage token storage
    var rememberMeService = services.GetRequiredService<IRememberMeService>();
    await rememberMeService.SetRememberMeAsync(true);
}
else
{
    // Keycloak, Auth0, etc. - provider handles session persistence
    // Remember-me is managed by provider's login UI
}
```

---

## Key Design Decisions

1. **Multi-Provider Support** - Abstracts provider-specific differences behind common interfaces
2. **Discovery Document Caching** - 1-hour cache to minimize network calls while staying current
3. **Client-Specified ClientId** - Allows clients to override server-provided clientId for flexibility
4. **UI-Agnostic ViewModels** - ViewModelBase has no UI framework dependencies
5. **Provider Type Inference** - Automatically detects provider from config URLs/fields
6. **Native vs Client-Side Remember-Me** - Distinguishes providers with built-in session persistence
7. **Async Logout URL Building** - Ensures discovery document is fetched for end_session_endpoint

---

## Common Pitfalls

1. **InitializeDiscoveryAsync Required** - Call during startup for best logout URL construction
2. **IdTokenHint for Keycloak** - Keycloak requires id_token_hint for silent logout
3. **Post-Logout Redirect Registration** - Redirect URI must be registered with provider
4. **HttpClient Scoping** - OpenIdDiscoveryService creates own HttpClient (singleton service)
5. **Dispose ViewModels** - Call Dispose to unsubscribe from auth state events
6. **Config Field Case Sensitivity** - Code handles multiple casing variants (ClientId, clientId)

---

## Provider-Specific Notes

### AWS Cognito
- No native remember-me support - app must manage token storage
- Uses `logout_uri` parameter instead of `post_logout_redirect_uri`
- Requires `client_id` in logout URL
- Supports both new config format (HostedUIDomain) and old format (cognitoDomain)

### Keycloak
- Has native remember-me checkbox in login UI
- Requires `id_token_hint` for silent logout without confirmation
- Uses standard `post_logout_redirect_uri` parameter
- Post-logout redirect URI must be in "Valid post logout redirect URIs" client setting

### Auth0
- Has native "Remember this device" support
- Uses `/v2/logout` endpoint with `returnTo` parameter
- Requires `client_id` in logout URL

### Okta / Azure AD
- Have native "Keep me signed in" / "Stay signed in" support
- Use standard OIDC parameters
- `id_token_hint` is optional but recommended
