# BlazorTest.WASM - Non-Microsoft Services Documentation

This document provides a detailed breakdown of all non-Microsoft services injected at various module levels in the BlazorTest.WASM application.

## Service Registration Hierarchy

### 1. Program.cs (Top Level)
Direct registrations in the main program:

| Service Interface | Implementation | Lifetime | Description |
|-------------------|----------------|----------|-------------|
| `HttpClient` | `HttpClient` | Singleton | HTTP client configured with assets base URL |
| `IStaticAssets` | `BlazorStaticAssets` | Singleton | Static asset management for multi-tenant scenarios |
| `ILzHost` | `LzHost` | Singleton | Environment configuration managing API URLs, platform detection |

### 2. ConfigApp.AddApp()
Orchestrates modular registration by calling:
- `AddBlazorUI()` - UI and authentication services
- `AddViewModels()` - ViewModels and support services

### 3. ConfigBlazorUI.AddBlazorUI()
Registers authentication pipeline via `AddLazyMagicAuthCognito()`:

#### LazyMagic.Client.Auth.Cognito Services
| Service Interface | Implementation | Lifetime | Description |
|-------------------|----------------|----------|-------------|
| `IAuthProvider` | `AuthProviderCognito` | Transient | AWS Cognito authentication provider |

#### LazyMagic.Client.Auth Services (via AddLazyMagicAuth)
| Service Interface | Implementation | Lifetime | Description |
|-------------------|----------------|----------|-------------|
| `IAuthProcess` | `AuthProcess` | Transient | Authentication workflow orchestration |
| `ILoginFormat` | `LoginFormat` | Transient | Login input validation and formatting |
| `IEmailFormat` | `EmailFormat` | Transient | Email validation and formatting |
| `IPhoneFormat` | `PhoneFormat` | Transient | Phone number validation and formatting |
| `ICodeFormat` | `CodeFormat` | Transient | Verification code formatting |
| `IPasswordFormat` | `PasswordFormat` | Transient | Password validation and strength checking |

### 4. ConfigViewModels.AddViewModels()
ViewModels and core client services:

| Service Interface | Implementation | Lifetime | Description |
|-------------------|----------------|----------|-------------|
| `ILzMessages` | `LzMessages` | Singleton | Localization and messaging system |
| `ILzClientConfig` | `LzClientConfig` | Singleton | Client configuration with dynamic loading |
| `ILzHttpClient` | `LzHttpClient` | Singleton | HTTP client with auth support (SigV4 signing) |
| `ISessionsViewModel` | `SessionsViewModel` | Singleton | Session lifecycle management |

Also calls:
- `AddLazyMagicBlazorAuth()` - Blazor-specific auth models
- `ViewModelsRegisterFactories.ViewModelsRegister()` - Auto-generated factory registrations

### 5. LazyMagic.Blazor.Auth Services (via AddLazyMagicBlazorAuth)
Calls `AddLazyMagicBlazor()` which registers:

#### LazyMagic.Blazor Core Services
| Service Interface | Implementation | Lifetime | Description |
|-------------------|----------------|----------|-------------|
| `IConnectivityService` | `ConnectivityService` | Singleton | Network connectivity monitoring |
| `IInternetConnectivitySvc` | `ConnectivityService` | Singleton | Alias for IConnectivityService |
| `IOSAccess` | `BlazorOSAccess` | Singleton | Platform-specific functionality |
| `ILzMessages` | `LzMessages` | Singleton | Message localization (if not already registered) |
| `ILzClientConfig` | `LzClientConfig` | Singleton | Client config (if not already registered) |
| `ClipboardService` | `ClipboardService` | Scoped | Clipboard operations |
| `IResizeListener` | `ResizeListener` | Scoped | Window resize event handling |
| `BrowserFingerprintService` | `BrowserFingerprintService` | Singleton | Browser fingerprinting for analytics |
| `ILzJsUtilities` | `LzJsUtilities` | Singleton | JavaScript interop utilities |

Also configures:
- MediaQueryService - CSS media query monitoring
- ResizeListener - Additional resize listener configuration

## Service Dependencies

### Authentication Flow
```
IAuthProcess 
  └── IAuthProvider (AuthProviderCognito)
       └── AWS Cognito SDK
```

### HTTP Communication
```
ILzHttpClient
  ├── ISessionsViewModel (for auth tokens)
  └── HttpClient (for actual requests)
```

### Configuration Management
```
ILzClientConfig
  ├── ILzHost (for API URLs)
  └── IStaticAssets (for config files)
```

### UI Services
```
Blazor Components
  ├── ILzJsUtilities (JS interop)
  ├── IConnectivityService (network status)
  ├── IOSAccess (platform features)
  └── ILzMessages (localization)
```

## Factory-Generated Services

The `ViewModelsRegisterFactories` class auto-registers factories for ViewModels marked with `[Factory]` attribute:

- `ISessionViewModelFactory` - Creates `SessionViewModel` instances
- Additional factories for any ViewModels marked with `[Factory]`

## Key Integration Points

1. **Authentication**: AWS Cognito integration through `AuthProviderCognito`
2. **HTTP**: All HTTP requests flow through `ILzHttpClient` for auth token injection
3. **Configuration**: Dynamic configuration loaded via `ILzClientConfig`
4. **Localization**: Multi-language support through `ILzMessages`
5. **Platform**: Platform-specific features abstracted via `IOSAccess`

## Service Lifetime Notes

- **Singleton**: Shared across entire application lifetime
- **Scoped**: New instance per Blazor circuit/connection
- **Transient**: New instance per request

Most UI services are Singleton for performance, while auth services are Transient for security isolation.