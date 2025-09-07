# LazyMagic OIDC Authentication System

## Overview

The LazyMagic OIDC system provides a unified authentication layer across MAUI and Blazor WebAssembly platforms using OpenID Connect (OIDC) and OAuth 2.0 protocols. It supports AWS Cognito as the primary identity provider with extensibility for other OIDC providers.

## Architecture

### Core Components

```
LazyMagic.OIDC.Base/
├── Interfaces/
│   ├── IOIDCService.cs          # Core authentication service interface
│   └── IDynamicConfigurationProvider.cs
├── Config/
│   ├── LazyOidcConfig.cs        # Lazy-loading configuration service
│   └── DynamicConfigurationProvider.cs
└── Models/
    ├── OidcOptionsConfiguration.cs  # Configuration transformation
    └── OIDCAuthenticationState.cs

LazyMagic.OIDC.MAUI/
├── OIDC/
│   └── MauiOIDCService.cs       # MAUI implementation using WebAuthenticator
└── Config/
    └── ConfigureLazyMagicOIDCMAUI.cs

LazyMagic.OIDC.WASM/
├── OIDC/
│   ├── BlazorOIDCService.cs     # WASM implementation using Blazor Auth
│   ├── DynamicOidcConfigHolder.cs
│   └── DynamicOidcPostConfigureOptions.cs
└── Config/
    └── ConfigureLazyMagicOIDCWASM.cs
```

## Configuration Format

### New Format (Recommended)
```json
{
  "authConfigs": {
    "consumerauth": {
      "ClientId": "3i0pdkk1edav4m5gu9nd5j0lm6",
      "HostedUIDomain": "https://example.auth.region.amazoncognito.com",
      "MetadataUrl": "https://cognito-idp.region.amazonaws.com/poolId/.well-known/openid-configuration",
      "IssuerUrl": "https://cognito-idp.region.amazonaws.com/poolId",
      "AuthorityDomain": "https://cognito-idp.region.amazonaws.com/poolId",
      "awsRegion": "us-west-2",
      "userPoolName": "consumerauth"
    }
  }
}
```

### Configuration Fields Explained

| Field | Purpose | Used By |
|-------|---------|---------|
| **ClientId** | OAuth client identifier | Both platforms |
| **HostedUIDomain** | User login interface URL | Authority for OAuth flows |
| **MetadataUrl** | OIDC discovery document | Token validation |

## Platform Implementations

### MAUI Implementation

MAUI uses `WebAuthenticator` for OAuth flows with a custom URI scheme:

```csharp
// Authentication flow
1. Build OAuth URL using HostedUIDomain
2. Launch WebAuthenticator with custom scheme callback
3. Exchange authorization code for tokens
4. Store tokens securely using ITokenStorageService
5. Validate and parse JWT tokens
```

**Key Features:**
- Custom URI scheme: `awsloginmaui://auth-callback`
- Secure token storage using platform-specific secure storage
- Manual OAuth URL construction
- Direct token exchange with Cognito

### WASM (Blazor WebAssembly) Implementation

WASM uses Microsoft's Blazor authentication framework:

```csharp
// Authentication flow
1. Load configuration dynamically
2. Apply to DynamicOidcConfigHolder
3. Post-configure Blazor OIDC options
4. Blazor handles OAuth flow automatically
5. Tokens stored in browser localStorage
```

**Key Features:**
- HTTPS redirect: `https://app.com/AuthPage/login-callback`
- Browser-based token storage
- Framework-managed OAuth flow
- Automatic token refresh

## Configuration Loading Flow

### 1. Startup Configuration
```csharp
// MAUI Program.cs
services.AddLazyMagicOIDCMAUI();
await ConfigureLazyMagicOIDCMAUI.LoadConfiguration(app);

// WASM Program.cs
builder.Services.AddLazyMagicOIDCWASM();
await ConfigureLazyMagicOIDCWASM.LoadConfiguration(host);
```

### 2. Dynamic Configuration Loading
```
1. LazyOidcConfig fetches config from {AssetsUrl}/config
2. Config JSON parsed and auth configs extracted
3. Selected auth config determined from ILzHost.AuthConfigName
4. OidcOptionsConfiguration.FromAuthConfig() transforms raw config
5. Platform-specific application:
   - MAUI: Direct usage in MauiOIDCService
   - WASM: Applied via DynamicOidcConfigHolder
```

### 3. Configuration Transformation
```csharp
// Raw config → Standard OIDC fields
OidcOptionsConfiguration.FromAuthConfig(authConfig, baseAddress)
  ├── Authority = HostedUIDomain (for OAuth)
  ├── MetadataUrl = MetadataUrl (for discovery)
  ├── ClientId = ClientId
  ├── RedirectUri = {baseAddress}/AuthPage/login-callback
  ├── PostLogoutRedirectUri = {baseAddress}
  └── DefaultScopes = ["openid", "profile", "email"]
```

## AWS Cognito Specifics

### Domain Types

1. **Hosted UI Domain**
   - Format: `https://{prefix}.auth.{region}.amazoncognito.com`
   - Purpose: User authentication UI
   - Endpoints: `/oauth2/authorize`, `/oauth2/token`, `/logout`

2. **Issuer Domain**
   - Format: `https://cognito-idp.{region}.amazonaws.com/{poolId}`
   - Purpose: Token validation
   - Endpoints: `/.well-known/openid-configuration`, `/.well-known/jwks.json`

### Why Both Are Needed

AWS Cognito splits OAuth and OIDC discovery across two domains:
- **OAuth flows** → Hosted UI Domain
- **Token validation** → Issuer Domain

Most OIDC providers serve both from a single domain, but Cognito requires both to be configured.

## Service Registration

### MAUI Services
```csharp
services.TryAddSingleton<IOIDCService, MauiOIDCService>();
services.TryAddSingleton<IOidcConfig, LazyOidcConfig>();
services.TryAddSingleton<IDynamicConfigurationProvider, DynamicConfigurationProvider>();
```

### WASM Services
```csharp
services.TryAddScoped<IOIDCService, BlazorOIDCService>();  // Scoped for Blazor
services.TryAddSingleton<IOidcConfig, LazyOidcConfig>();
services.TryAddSingleton<DynamicOidcConfigHolder>();
services.TryAddSingleton<IDynamicConfigurationProvider, DynamicConfigurationProvider>();
```

## Common Operations

### Login
```csharp
// Both platforms
await oidcService.LoginAsync();

// MAUI: Launches WebAuthenticator
// WASM: Navigates to Blazor auth endpoint
```

### Get Access Token
```csharp
var accessToken = await oidcService.GetAccessTokenAsync();
```

### Get ID Token
```csharp
var idToken = await oidcService.GetIdentityTokenAsync();
```

### Logout
```csharp
await oidcService.LogoutAsync();
```

## Troubleshooting

### Common Issues

1. **"Network Error" on WASM login**
   - Check if configuration loaded properly in browser console
   - Verify PostConfigure shows correct Authority and ClientId
   - Ensure config keys match (case-sensitive)

2. **Configuration not loading**
   - Check {AssetsUrl}/config endpoint is accessible
   - Verify JSON format is valid
   - Check for case sensitivity in auth config names

3. **MAUI custom scheme not working**
   - Ensure `awsloginmaui://` scheme is registered in platform config
   - Check Android/iOS manifest files for scheme registration

4. **Token validation failures**
   - Verify MetadataUrl points to correct issuer
   - Check ClientId matches Cognito app client
   - Ensure scopes are configured correctly

### Debug Logging

Enable detailed logging to diagnose issues:

```csharp
// Check configuration loading
[LazyOidcConfig] Loading OIDC configuration from {url}
[LazyOidcConfig] Configuration details:
  Selected AuthConfig: {name}
  - Authority: {authority}
  - ClientId: {clientId}

// Check post-configuration (WASM)
[DynamicOidcPostConfigureOptions] PostConfigure called
[DynamicOidcPostConfigureOptions] Final OIDC options - Authority: {authority}

// Check authentication flow
[BlazorOIDCService] Initiating Blazor WebAssembly login
[MauiOIDCService] Building auth URL for selected config: {config}
```

## Extending the System

### Adding New OIDC Providers

1. Update `OidcOptionsConfiguration.FromAuthConfig()` to handle provider-specific fields
2. Modify `DynamicConfigurationProvider.GetProviderType()` to detect the provider
3. Update `BuildLogoutUrl()` for provider-specific logout URLs

### Custom Token Storage

Implement `ITokenStorageService` for custom token storage:

```csharp
public interface ITokenStorageService
{
    Task<string?> GetAccessTokenAsync();
    Task<string?> GetIdTokenAsync();
    Task SaveTokensAsync(string? accessToken, string? idToken);
    Task ClearTokensAsync();
}
```

## Security Considerations

1. **Token Storage**
   - MAUI: Uses secure platform storage
   - WASM: Uses browser localStorage (less secure)

2. **Redirect URIs**
   - Must be whitelisted in Cognito app client
   - HTTPS required for production WASM apps

3. **Token Refresh**
   - WASM: Handled by Blazor framework
   - MAUI: Manual refresh implementation needed

4. **PKCE (Proof Key for Code Exchange)**
   - Automatically handled by both platforms
   - Required for public clients

## Migration Guide

### From Old Config Format to New Format

Old format:
```json
{
  "userPoolId": "us-west-2_xxx",
  "userPoolClientId": "xxx",
  "awsRegion": "us-west-2",
  "cognitoDomainPrefix": "myapp"
}
```

New format:
```json
{
  "ClientId": "xxx",
  "HostedUIDomain": "https://myapp.auth.us-west-2.amazoncognito.com",
  "MetadataUrl": "https://cognito-idp.us-west-2.amazonaws.com/us-west-2_xxx/.well-known/openid-configuration",
  "IssuerUrl": "https://cognito-idp.us-west-2.amazonaws.com/us-west-2_xxx",
  "awsRegion": "us-west-2"
}
```

The system maintains backward compatibility with the old format while recommending migration to the new explicit URL format.