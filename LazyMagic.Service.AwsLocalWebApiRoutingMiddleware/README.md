# LazyMagic.Service.AwsLocalWebApiRoutingMiddleware

## Overview

This middleware mimics the behavior of CloudFront request functions for local development. It reads configuration from `systemconfig.yaml` and AWS CloudFront Key-Value Store to add the necessary headers to HTTP requests, enabling local testing with the same behavior as production.

## Purpose

In production, CloudFront Functions add headers to requests before routing them to API Gateway or AppRunner. During local development, this middleware replicates that functionality by:

1. Reading system configuration from `systemconfig.yaml`
2. Fetching tenant configuration from AWS CloudFront Key-Value Store
3. Adding required headers to each request
4. Adjusting request paths to match local routing

## Headers Added

The middleware adds the following headers to each request:

| Header | Source | Purpose |
|--------|--------|---------|
| `lz-aws-kvsarn` | CloudFront KVS ARN | References the Key-Value Store containing tenant configs |
| `lz-tenantid` | `systemconfig.yaml` `DefaultTenant` | Default tenant identifier |
| `lz-config` | CloudFront KVS | Packed JSON tenant configuration |
| `lz-authname` | `systemconfig.yaml` `DefaultAuthname` | Authenticator name for multi-tenant routing |

## Configuration

### systemconfig.yaml

The middleware reads configuration from `../../systemconfig.yaml` relative to the application directory:

```yaml
SystemKey: myapp
DefaultTenant: tenant1
DefaultAuthname: tenantauth  # Optional, defaults to "tenantauth"
Region: us-west-2
Profile: default
```

### Required Fields

- **SystemKey**: Unique identifier for your system (used to locate CloudFront KVS)
- **DefaultTenant**: Default tenant ID to use for local requests

### Optional Fields

- **DefaultAuthname**: Authenticator name for multi-tenant AppSync Events routing
  - Defaults to `"tenantauth"` if not specified
  - Common values: `"tenantauth"`, `"consumerauth"`, `"partnerauth"`
  - Used by `AppSyncWsEventPublisher` to route events to correct AppSync Events API

## Multi-Tenant AppSync Events Support

The `lz-authname` header enables multi-tenant AppSync Events routing in local development:

### How It Works

1. **Middleware reads `DefaultAuthname`** from `systemconfig.yaml`
2. **Adds `lz-authname` header** to all requests
3. **LzAuthorization populates `ICallerInfo.Authname`** from header
4. **ChatManagerService passes `callerInfo`** to event publishers
5. **AppSyncWsEventPublisher routes events** to correct AppSync API based on `Authname`

### Example Flow

```
Local Request
  ↓
AwsLocalWebApiRoutingMiddleware
  ↓ (adds lz-authname: tenantauth)
LzAuthorization.AddConfigAsync()
  ↓ (sets callerInfo.Authname = "tenantauth")
ChatEventPublisher
  ↓ (forwards callerInfo)
AppSyncWsEventPublisher.ResolveEventsApiConfig("tenantauth")
  ↓ (resolves to AWS:AppSync:tenantauthEventsApi)
AppSync Events API (tenantauth)
```

### Testing Different Authenticators

To test consumer authentication locally:

```yaml
# systemconfig.yaml
DefaultAuthname: consumerauth
```

This routes all local events to the `consumerauthEventsApi` instead of `tenantauthEventsApi`.

## Path Adjustment

The middleware removes the first path segment to align with local routing:

**Production Path:** `/api/chat/123`
**CloudFront Routing:** Uses `/api` to route to specific API Gateway
**Local Path:** `/chat/123` (first segment removed)

This allows the local development server to aggregate all APIs under a single host.

## Usage

### Registration

In `Startup.cs`:

```csharp
public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
{
    // Must be added BEFORE UseRouting() to modify request before routing
    app.UseAwsLocalWebApiRoutingMiddleware();

    app.UseRouting();
    app.UseCors();
    app.UseEndpoints(endpoints =>
    {
        endpoints.MapControllers();
    });
}
```

### Dependencies

The middleware requires:
- `IAmazonCloudFrontKeyValueStore` - For fetching tenant configs
- `IAmazonCloudFront` - For listing Key-Value Stores
- `ILogger<AwsLocalWebApiRoutingMiddleware>` - For logging

These should be registered in `ConfigureServices`:

```csharp
services.AddAWSService<IAmazonCloudFrontKeyValueStore>();
services.AddAWSService<IAmazonCloudFront>();
```

## Initialization

The middleware performs synchronous initialization in its constructor:

1. **Reads systemconfig.yaml** - Gets SystemKey, DefaultTenant, DefaultAuthname
2. **Finds CloudFront KVS ARN** - Locates `{SystemKey}---kvs` Key-Value Store
3. **Loads tenant config** - Fetches configuration for default tenant

**Note**: Initialization failures will throw exceptions and prevent application startup.

## Error Handling

The middleware throws exceptions for:
- Missing `systemconfig.yaml`
- Missing required fields (SystemKey, DefaultTenant)
- CloudFront KVS not found
- Unable to fetch tenant configuration

These errors are intentional to fail fast during development setup.

## Logging

The middleware logs at various levels:

- **Debug**: Header additions (`lz-authname` header added)
- **Info**: Normal operations
- **Warning**: Configuration issues
- **Error**: Failures accessing CloudFront services

## Production Behavior

This middleware is **only used in local development**. In production:

- CloudFront Functions add headers before routing
- AppRunner containers receive pre-configured headers
- No middleware initialization overhead
- No dependency on CloudFront KVS access during request processing

## Comparison: Local vs Production

| Aspect | Local (Middleware) | Production (CloudFront) |
|--------|-------------------|------------------------|
| Header Source | systemconfig.yaml + KVS | CloudFront Function + KVS |
| `lz-authname` | Read from DefaultAuthname | Set by authentication scheme |
| Path Adjustment | Middleware removes segment | CloudFront routes by segment |
| Initialization | Constructor (startup) | Per-request (cached) |
| Configuration | Static (restart required) | Dynamic (KVS updates) |

## Troubleshooting

### Duplicate Header Values

**Issue**: `lz-authname` appears as `"tenantauth,tenantauth"` in logs

**Cause**: Header added multiple times during request pipeline

**Solution**: The middleware now checks if header exists before adding:
```csharp
if (!string.IsNullOrEmpty(_defaultAuthname) && !context.Request.Headers.ContainsKey("lz-authname"))
{
    context.Request.Headers.Append("lz-authname", _defaultAuthname);
}
```

**Additional Fix**: `LzAuthorization` reads only first value:
```csharp
var authname = request.Headers["lz-authname"].FirstOrDefault();
```

This prevents comma-separated duplicates when multiple middleware components set the same header.

## Version History

- **v1.1.1** (2025-01-22): Fixed header duplication by checking existence before adding and reading first value only
- **v1.1** (2025-01-22): Added `lz-authname` header support for multi-tenant AppSync Events routing
- **v1.0**: Initial implementation with tenant configuration headers

## Related Components

- **LzAuthorization**: Reads `lz-authname` header and populates `ICallerInfo.Authname`
- **AppSyncWsEventPublisher**: Uses `ICallerInfo.Authname` to route events to correct AppSync API
- **ChatManagerService**: Passes `callerInfo` to event publishers
- **AppHost Authentication Middleware**: Production equivalent that sets `lz-authname` based on JWT validation

## References

- CloudFront Key-Value Store: https://docs.aws.amazon.com/AmazonCloudFront/latest/DeveloperGuide/kvs.html
- Multi-Authenticator Strategy: `/MagicPets/Service/AuthSummary.md`
- AppSync Events Routing: `/MagicPets/Service/ChatEvents.md`
