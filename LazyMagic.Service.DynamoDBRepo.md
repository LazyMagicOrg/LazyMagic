# LazyMagic.Service.DynamoDBRepo

## Overview

LazyMagic.Service.DynamoDBRepo provides a high-level abstraction for Amazon DynamoDB operations, implementing a repository pattern for CRUDL (Create, Read, Update, Delete, List) operations. It follows DynamoDB best practices with single-table design, supporting multi-tenancy, optimistic locking, notifications, and flexible querying with secondary indices.

## Key Features

- **Single Table Design**: Implements AWS-recommended single table pattern for DynamoDB
- **Envelope Pattern**: Wraps entities with metadata for versioning, timestamps, and indexing
- **Secondary Indices**: Support for up to 5 secondary indices (SK1-SK5) for flexible querying
- **Optimistic Locking**: Built-in concurrency control using UpdateUtcTick timestamps
- **Multi-Tenancy**: Table-level isolation for different tenants
- **Soft Delete**: Optional TTL-based soft delete with automatic cleanup
- **Notifications**: Pluggable notification system for real-time updates
- **Query Operations**: Rich query API supporting various operators and pagination

## Core Classes

### Repository Classes

**IDYDBRepository&lt;T&gt;**
- Simple interface extending `IDocumentRepo<T>`
- Constrains T to classes implementing `IItem`

**DYDBRepository&lt;T&gt;**
- Abstract base class implementing core repository functionality
- Maps CRUDL operations to DynamoDB low-level API
- Handles envelope pattern, optimistic locking, and notifications

### Supporting Classes

**PartialContentObjectResult&lt;T&gt;**
- Custom ActionResult for HTTP 206 (Partial Content) responses
- Used when query results exceed 5MB limit

**QueryHelper**
- Utility for converting string operators to DynamoDB QueryOperator enums
- Supports: Equals, BeginsWith, LessThan, GreaterThan, Between, etc.

## DynamoDB Schema Conventions

### Primary Key Structure
- **PK (Partition Key)**: Entity type name with colon suffix (e.g., "Customer:")
- **SK (Sort Key)**: Entity instance identifier with colon suffix (e.g., "123:")

### Envelope Fields
- **Data**: Serialized JSON of the actual entity
- **SK1-SK5**: Secondary index fields for additional query capabilities
- **CreateUtcTick**: Creation timestamp
- **UpdateUtcTick**: Update timestamp for optimistic locking
- **IsDeleted**: Soft delete flag
- **TTL**: Time-to-live for automatic deletion
- **Topics**: Notification topics array
- **TypeName**: Entity type and version

## Usage Examples

### Implementing a Repository
```csharp
public class CustomerRepository : DYDBRepository<Customer>
{
    public CustomerRepository(IAmazonDynamoDB dynamoDbClient) 
        : base(dynamoDbClient)
    {
    }
    
    protected override void AssignEntityAttributes(
        Customer item, 
        Dictionary<string, AttributeValue> attributes)
    {
        // Assign secondary indices
        attributes["SK1"] = new AttributeValue { S = item.Email };
        attributes["SK2"] = new AttributeValue { S = item.City };
    }
    
    protected override List<string> AssignTopics(Customer item)
    {
        return new List<string> { $"customer:{item.Id}" };
    }
}
```

### CRUD Operations
```csharp
// Create
var result = await repository.CreateAsync(callerInfo, customer);

// Read
var result = await repository.ReadAsync(callerInfo, "Customer:", "123:");

// Update with optimistic locking
var result = await repository.UpdateAsync(callerInfo, customer);

// Delete (soft delete if configured)
var result = await repository.DeleteAsync(callerInfo, "Customer:", "123:");
```

### Query Operations
```csharp
// Query by email (using SK1 index)
var queryRequest = new QueryRequest
{
    IndexName = "SK1",
    IndexValue = "user@example.com",
    QueryOperator = "Equals"
};
var results = await repository.ListAsync(callerInfo, queryRequest);

// Range query
var queryRequest = new QueryRequest
{
    IndexName = "SK2",
    IndexValue = "2023-01-01",
    IndexValue2 = "2023-12-31",
    QueryOperator = "Between"
};
var results = await repository.ListAsync(callerInfo, queryRequest);
```

## Advanced Features

### Optimistic Locking
```csharp
// Update will fail if record was modified since last read
var result = await repository.UpdateAsync(callerInfo, customer);
if (result is ConflictObjectResult)
{
    // Handle concurrent modification
}

// Force update bypasses optimistic locking
var result = await repository.UpdateAsync(callerInfo, customer, force: true);
```

### Soft Delete with TTL
```csharp
public class MyRepository : DYDBRepository<MyEntity>
{
    public MyRepository()
    {
        UseSoftDelete = true;
        SoftDeleteTTLDays = 30; // Keep for 30 days
    }
}
```

### Multi-Tenancy
```csharp
var callerInfo = new CallerInfo
{
    TenantId = "tenant-123",
    TableLevel = TableLevel.Tenant // Use tenant-specific table
};
```

### Notifications
```csharp
protected override List<string> AssignTopics(Order order)
{
    return new List<string> 
    { 
        $"order:{order.Id}",
        $"customer:{order.CustomerId}"
    };
}

protected override async Task WriteNotificationAsync(
    ICallerInfo callerInfo, 
    LzNotification notification)
{
    // Custom notification implementation
    await NotificationService.PublishAsync(notification);
}
```

## Query Operators

- **Equals**: Exact match
- **BeginsWith**: Prefix matching
- **LessThan**: Values less than specified
- **LessThanOrEqual**: Values less than or equal
- **GreaterThan**: Values greater than specified
- **GreaterThanOrEqual**: Values greater than or equal
- **Between**: Range queries (requires two values)

## Best Practices

1. **Entity Design**: Design entities with query patterns in mind
2. **Index Selection**: Choose SK1-SK5 assignments based on access patterns
3. **Batch Operations**: Use batch operations for bulk processing
4. **Error Handling**: Handle specific DynamoDB exceptions appropriately
5. **TTL Usage**: Use TTL for temporary data and audit trails
6. **Monitoring**: Monitor consumed capacity and throttling

## Response Handling

The repository returns ASP.NET Core ActionResults:
- **200 OK**: Successful operation
- **201 Created**: New item created
- **206 Partial Content**: Query results exceed 5MB
- **400 Bad Request**: Invalid request or item already exists
- **404 Not Found**: Item not found
- **409 Conflict**: Optimistic locking failure

## Configuration

### Table Structure
Tables follow the naming convention: `{prefix}-{entityType}`
- System level: `system-{entityType}`
- Tenant level: `{tenantId}-{entityType}`
- Local level: `local-{entityType}`

### Required IAM Permissions
```json
{
    "Version": "2012-10-17",
    "Statement": [{
        "Effect": "Allow",
        "Action": [
            "dynamodb:PutItem",
            "dynamodb:GetItem",
            "dynamodb:UpdateItem",
            "dynamodb:DeleteItem",
            "dynamodb:Query",
            "dynamodb:Scan"
        ],
        "Resource": "arn:aws:dynamodb:*:*:table/*"
    }]
}
```

## Dependencies

- AWSSDK.DynamoDBv2 for DynamoDB operations
- Newtonsoft.Json for JSON serialization
- Microsoft.AspNetCore.Mvc for ActionResult support
- LazyMagic.Service.Shared for shared interfaces