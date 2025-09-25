# LazyMagic.Shared

## Overview

LazyMagic.Shared provides core interfaces, models, and utilities shared between client and server components. It defines fundamental contracts for items, authentication, HTTP clients, dependency injection, and multi-tenancy configuration that are used throughout the LazyMagic framework.

## Key Features

- **Core Interfaces**: Fundamental contracts for items, authentication, and HTTP clients
- **DI Marker Interfaces**: Convention-based dependency injection registration
- **Multi-Tenancy Support**: Comprehensive tenancy configuration with packing/unpacking
- **Browser Fingerprinting**: Device and browser identification model
- **Service Extensions**: Utilities for inspecting and debugging DI registrations
- **Authentication Models**: Credential management for AWS SigV4 and JWT tokens

## Core Interfaces

### IItem
Basic interface for all entities in the system:
```csharp
public interface IItem
{
    string Id { get; set; }
}
```

### Dependency Injection Markers
Convention-based DI registration interfaces:
- **ILzSingleton**: Marks classes for singleton registration
- **ILzTransient**: Marks classes for transient registration
- **ILzScoped**: Marks classes for scoped registration

### IAuthProviderCreds
Interface for authentication credential management:
- **SecurityLevel**: 0=insecure, 1=JWT token, 2=AWS SigV4
- **SessionId**: Unique session identifier
- Token management: Identity, Access, and Refresh tokens
- AWS credentials management for SigV4 signing

### ILzHttpClient
HTTP client abstraction for SDK generation:
```csharp
public interface ILzHttpClient : IDisposable
{
    Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage requestMessage,
        HttpCompletionOption httpCompletionOption,
        CancellationToken cancellationToken,
        string? callerMemberName = null);
        
    void Initialize(ILzCurrentSessionAuthProviderCreds currentSession);
}
```

### ILzCurrentSessionAuthProviderCreds
Marker interface combining session management with authentication credentials.

### IRegisterObservables
Interface for classes that need to register reactive observables.

## Core Classes

### Creds
AWS credentials model:
```csharp
public class Creds
{
    public string AccessKey { get; set; }
    public string SecretKey { get; set; }
    public string Token { get; set; }
}
```

### BrowserFingerprint
Comprehensive browser and device information:
- Basic info: Fingerprint hash, browser, OS, resolution
- Device details: Type, vendor, mobile detection
- Environment: Language, timezone, user agent
- Browser flags: Chrome, Firefox, Safari, IE detection

### TenancyConfig
Multi-level tenancy configuration system:

**TenancyConfigBase**
- System, Tenant, and Subtenant keys
- Suffix management (Ss, Ts, Sts) with substitution patterns
- Environment and region configuration

**TenancyConfig**
- Unpacks compressed configuration for runtime use
- Calculates paths for databases and assets
- Supports APIs, Assets, and WebApps behaviors
- Provides tenant isolation at multiple levels

**TenancyConfigPacked**
- Compressed format for storage in limited systems
- Behaviors packed as list of string lists
- Efficient JSON serialization

### ServiceCollectionExtensions
DI debugging and inspection utilities:
- `PrintRegistrationsAsTable`: Console table output
- `GetRegistrationsAsTableString`: String table format
- `PrintRegistrationsGroupedByLifetime`: Group by lifetime
- `PrintRegistrationsAsMarkdownTable`: Markdown format
- `PrintDuplicateRegistrations`: Find duplicate services

## Usage Examples

### Implementing IItem
```csharp
public class Customer : IItem
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
}
```

### Using DI Markers
```csharp
// Automatically registered as singleton
public class AuthService : IAuthService, ILzSingleton
{
    // Implementation
}

// Automatically registered as transient
public class CustomerRepository : ICustomerRepository, ILzTransient
{
    // Implementation
}
```

### Working with Credentials
```csharp
public class MyAuthProvider : IAuthProviderCreds
{
    public int SecurityLevel { get; set; } = 2; // AWS SigV4
    
    public async Task<Creds?> GetCredsAsync()
    {
        return new Creds
        {
            AccessKey = "AKIA...",
            SecretKey = "secret",
            Token = "session-token"
        };
    }
}
```

### Browser Fingerprinting
```csharp
var fingerprint = new BrowserFingerprint
{
    Browser = "Chrome",
    BrowserVersion = "120.0",
    OS = "Windows",
    DeviceType = "desktop",
    IsMobile = false,
    TimeZone = "America/New_York"
};
```

### Tenancy Configuration
```csharp
// Unpack configuration from storage
var packed = JsonConvert.DeserializeObject<TenancyConfigPacked>(json);
var config = new TenancyConfig(packed, "tenant.example.com");

// Access calculated fields
var tenantDb = config.TenantDB;        // "system_tenant"
var tenantAssets = config.TenantAssets; // S3 bucket name
```

### DI Inspection
```csharp
// In Program.cs after building services
var services = builder.Services;

// Print all registrations
services.PrintRegistrationsAsTable();

// Find duplicates
services.PrintDuplicateRegistrations();

// Get markdown for documentation
var markdown = services.GetRegistrationsAsTableString();
```

## Multi-Tenancy Model

The framework supports three levels of tenancy:

1. **System Level**: Shared across all tenants
   - SystemDB: Base database
   - SystemAssets: Shared assets

2. **Tenant Level**: Isolated per tenant
   - TenantDB: Tenant-specific database
   - TenantAssets: Tenant-specific assets

3. **Subtenant Level**: Further isolation
   - SubtenantDB: Subtenant database
   - SubtenantAssets: Subtenant assets

### Suffix Management
Suffixes ensure globally unique names:
- `{Ss}`: System suffix
- `{Ts}`: Tenant suffix (defaults to {Ss})
- `{Sts}`: Subtenant suffix (defaults to {Ts})

## Best Practices

1. **Implement IItem**: All entities should implement IItem for consistency
2. **Use DI Markers**: Leverage marker interfaces for automatic registration
3. **Security Levels**: Choose appropriate security level for APIs
4. **Tenant Isolation**: Use appropriate tenancy level for data
5. **Browser Detection**: Use fingerprinting for device-specific features

## Dependencies

- Newtonsoft.Json for JSON serialization
- Microsoft.Extensions.DependencyInjection for DI extensions
- System.Text.Json for attribute annotations