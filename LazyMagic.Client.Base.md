# LazyMagic.Client.Base

## Overview

LazyMagic.Client.Base is a foundational client-side library that provides core infrastructure for building multi-platform Blazor applications. It offers configuration management, multi-language support with unit conversion, JavaScript interop abstraction, and platform-specific implementations for WASM, MAUI, and Android platforms.

## Key Features

- **Multi-Language Localization**: Comprehensive message system with culture-specific loading and Imperial/Metric unit conversion
- **Platform Abstraction**: Unified interfaces for different deployment targets (WASM, MAUI, Android)
- **Configuration Management**: Hierarchical configuration system with tenant customization support
- **JavaScript Interop**: Type-safe abstraction for browser operations and cookie management
- **Reactive Programming**: Integration with ReactiveUI for automatic UI updates
- **AWS Cognito Integration**: Built-in support for AWS Cognito configuration

## Core Classes

### Configuration Management

**ILzClientConfig / LzClientConfig**
- Manages application configuration including authentication and tenancy settings
- Loads configuration from remote JSON files
- Supports multi-tenant configuration with override capabilities
- Handles AWS Cognito configuration settings

**ILzHost / LzHost**
- Central configuration for hosting environments
- Manages URLs for APIs, assets, and WebSocket connections
- Platform detection (IsWASM, IsMAUI, IsAndroid, IsLocal)
- Automatic localhost vs remote API switching

### Localization System

**ILzMessages / LzMessages**
- Multi-language, multi-tenancy message management
- Culture-specific message loading from JSON files
- Imperial/Metric unit conversion in messages
- Real-time message editing capabilities

**LzMessageSet**
- Manages messages for specific culture and unit system
- Variable substitution using `__variable__` syntax
- Unit conversion functions: `@Unit()` and `@UnitS()`
- Lazy loading for performance

**Supporting Classes**
- `MessageDoc` - Container for message files with metadata
- `MsgItem` - Individual message with editability flags
- `MsgItemModel` / `MsgItemsModel` - ReactiveUI models for message editing
- `LzMessageSetSelector` - Culture and units selection helper

### Platform Abstraction

**ILzJsUtilities**
- JavaScript interop abstraction
- Comprehensive cookie management with `CookieOptions`
- Local storage operations
- Image processing (base64 conversion, downsizing)
- Web sharing capabilities
- Asset and service worker management

**IOSAccess**
- File system abstraction for local and cloud storage
- Support for local file operations and S3
- HTTP read capabilities

**IStaticAssets**
- Static asset reading abstraction
- Specialized methods for configuration files

### Base Classes

**NotifyBase**
- Lightweight INotifyPropertyChanged implementation
- Alternative to ReactiveUI for minimal overhead scenarios
- Property change callbacks with CallerMemberName support

## Usage Examples

```csharp
// Configure services
services.AddSingleton<ILzHost, LzHost>();
services.AddSingleton<ILzClientConfig, LzClientConfig>();
services.AddSingleton<ILzMessages, LzMessages>();

// Platform detection
if (LzHost.IsWASM)
{
    // WASM-specific code
}

// Message localization
var messages = serviceProvider.GetService<ILzMessages>();
var welcomeMsg = messages.Msg("Welcome");
var distanceMsg = messages.Msg("Distance", "10"); // With unit conversion

// Cookie management
var jsUtils = serviceProvider.GetService<ILzJsUtilities>();
await jsUtils.SetCookie("theme", "dark", new CookieOptions 
{ 
    Secure = true, 
    SameSite = SameSiteMode.Strict 
});

// Configuration loading
var config = serviceProvider.GetService<ILzClientConfig>();
await config.ConfigureAsync();
var authConfig = config.AuthConfig;
```

## Message System Features

### Variable Substitution
```json
{
  "WelcomeUser": "Welcome __username__!"
}
```

### Unit Conversion
```json
{
  "Distance": "The distance is @Unit(km,mi,__value__)"
}
```

### Tenant Customization
Messages are loaded with precedence:
1. Sub-tenancy specific
2. Tenancy specific  
3. System default

## Configuration Hierarchy

1. **System Configuration**: Base configuration for all tenants
2. **Tenancy Configuration**: Tenant-specific overrides
3. **Sub-Tenancy Configuration**: Further customization per sub-tenant

## Key Patterns

1. **Interface-Based Design**: All major components have interfaces for testability
2. **Async/Await**: Modern async patterns for file and network operations
3. **Error Handling**: Comprehensive error handling with console logging
4. **Dependency Injection**: Designed for DI container registration
5. **Reactive Updates**: Property change notifications with ReactiveUI

## Dependencies

- ReactiveUI & ReactiveUI.Fody for reactive programming
- Microsoft.Extensions.Logging for logging
- Newtonsoft.Json for JSON operations
- LazyMagic.Shared for shared components