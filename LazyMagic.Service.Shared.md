# LazyMagic.Service.Shared

## Overview

LazyMagic.Service.Shared provides core interfaces and classes shared across server-side components. It defines the fundamental contracts for repository operations, caller context management, multi-tenancy configuration, and document handling in the LazyMagic framework.

## Key Features

- **Repository Pattern**: Standard interface for CRUD operations with multiple query methods
- **Caller Context**: Comprehensive caller information for multi-tenant operations
- **Tenancy Configuration**: Flexible multi-level tenancy support (System, Tenant, Subtenant)
- **Document Envelope**: Interface for wrapping entities with metadata
- **Permission Management**: Built-in permission tracking in caller context

## Core Interfaces and Classes

### ICallerInfo / CallerInfo

Provides comprehensive context about the caller making a request:

**Identity Information**
- `TenantId`: Tenant identifier
- `LzUserId`: LazyMagic user identifier
- `UserName`: Human-readable username
- `SessionId`: Current session identifier

**Tenancy Hierarchy**
- `System`: System-level identifier
- `Tenant`: Tenant-level identifier
- `Subtenant`: Sub-tenant identifier

**Resource Locations**
- `SystemDB`, `TenantDB`, `SubtenantDB`: Database identifiers
- `SystemAssets`, `TenantAssets`, `SubtenantAssets`: Asset storage locations
- `DefaultTenant`, `DefaultDB`, `DefaultAssets`: Default resource locations

**Security**
- `Permissions`: List of granted permissions
- `Headers`: Additional context headers (e.g., AWS Region, UserPoolId)

### IDocumentRepo&lt;T&gt;

Standard repository interface for document operations:

**CRUD Operations**
- `CreateAsync`: Create new document
- `ReadAsync`: Read document by ID
- `UpdateAsync`: Update existing document (with optimistic locking support)
- `UpdateCreateAsync`: Update or create if not exists
- `DeleteAsync`: Delete document by ID

**Query Operations**
- `ListAsync`: List all or by index
- `ListBeginsWithAsync`: Prefix matching
- `ListBetweenAsync`: Range queries
- `ListGreaterThanAsync` / `ListGreaterThanOrEqualAsync`: Greater than queries
- `ListLessThanAsync` / `ListLessThanOrEqualAsync`: Less than queries

### ITenancyConfig

Configuration interface for multi-tenant applications:

**Tenancy Keys**
- `SystemKey`, `TenantKey`, `SubtenantKey`: Unique identifiers
- `Ss`, `Ts`, `Sts`: Short identifiers
- `Env`: Environment identifier
- `Region`: Deployment region

**Resource Collections**
- `Apis`: List of API configurations
- `Assets`: Asset storage configurations
- `WebApps`: Web application configurations

**Calculated Fields**
- Computed paths for resources based on tenancy level
- Methods for packing/unpacking configuration

### IDocumentEnvelope&lt;T&gt;

Marker interface for document envelope implementations. Used to wrap entities with metadata such as:
- Timestamps
- Version information
- Indexing data
- Soft delete flags

## Usage Examples

### Implementing a Repository
```csharp
public class UserRepository : IDocumentRepo<User>
{
    public async Task<ActionResult<User>> CreateAsync(
        ICallerInfo callerInfo, 
        User user)
    {
        // Use caller info for multi-tenant data isolation
        var database = GetDatabase(callerInfo.TenantDB);
        
        // Check permissions
        if (!callerInfo.Permissions.Contains("user:create"))
            return new ForbidResult();
            
        // Create user in tenant-specific storage
        return await database.CreateAsync(user);
    }
}
```

### Using CallerInfo
```csharp
var callerInfo = new CallerInfo
{
    TenantId = "tenant-123",
    LzUserId = "user-456",
    UserName = "john.doe@example.com",
    Permissions = new List<string> { "read", "write" },
    Headers = new Dictionary<string, string>
    {
        ["Region"] = "us-east-1",
        ["UserPoolId"] = "us-east-1_ABC123"
    }
};

await repository.CreateAsync(callerInfo, newItem);
```

### Query Operations
```csharp
// List all items
var allItems = await repository.ListAsync(callerInfo);

// Query by index
var emailResults = await repository.ListAsync(
    callerInfo, 
    "EmailIndex", 
    "user@example.com"
);

// Range query
var dateResults = await repository.ListBetweenAsync(
    callerInfo,
    "DateIndex",
    "2023-01-01",
    "2023-12-31"
);

// Prefix search
var nameResults = await repository.ListBeginsWithAsync(
    callerInfo,
    "NameIndex",
    "John"
);
```

### Multi-Tenancy Configuration
```csharp
public class TenancyConfig : ITenancyConfig
{
    // Implementation calculates paths based on tenancy level
    public string TenantDB => $"{TenantKey}-db";
    public string TenantAssets => $"{TenantKey}-assets";
    
    public void SetCalculatedFields()
    {
        // Calculate derived fields based on keys
    }
}
```

## Key Patterns

### Multi-Level Tenancy
The framework supports three levels of tenancy:
1. **System**: Shared across all tenants
2. **Tenant**: Isolated per tenant
3. **Subtenant**: Further isolation within tenants

### Permission-Based Access
CallerInfo carries permissions that repositories can check:
```csharp
if (!callerInfo.Permissions.Contains("admin"))
    return new UnauthorizedResult();
```

### Flexible Querying
Repository interface supports various query patterns:
- Exact match
- Prefix matching
- Range queries
- Comparison operators

### Resource Isolation
Resources (databases, assets) are isolated by tenancy level, with calculated paths ensuring proper separation.

## Best Practices

1. **Always validate CallerInfo**: Ensure caller has necessary permissions
2. **Use appropriate tenancy level**: Choose System, Tenant, or Subtenant based on data scope
3. **Implement all repository methods**: Provide consistent behavior across implementations
4. **Handle missing data gracefully**: Return appropriate ActionResults
5. **Log security-relevant operations**: Track who accessed what data

## Integration Points

- **LazyMagic.Service.DynamoDBRepo**: Concrete implementation of IDocumentRepo
- **LazyMagic.Shared**: Shared models and interfaces
- **Authentication Services**: Populate CallerInfo from auth tokens
- **Authorization Services**: Validate permissions in CallerInfo

## Dependencies

- Microsoft.AspNetCore.Mvc.Core for ActionResult types
- LazyMagic.Shared for IItem interface and shared models