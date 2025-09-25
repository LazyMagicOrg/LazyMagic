using Microsoft.Extensions.Primitives;
namespace LazyMagic.Service.Authorization;
/// <summary>
/// This abstract class performs common housekeeping tasks for 
/// controllers. You must implement at least:
/// LoadPermissionsAsync() - initializes defaultPerm, adminPerm, methodPermissions
/// 
/// The virtual method GetCallerInfoAsync() is called with each endpoint call to allow 
/// you to implement various strategies for updating user permissions and getting 
/// the tenant config information and default Tenant, default DB, and default Assets.
///   
/// Once we have the CallerInfo object, we pass it along to repository methods.
/// Note: If you are  using DynamoDB for your database and the LazyMagic.Repository.DynamoDB
/// libary, then the DefaultDB is a DynamoDB table name.
/// </summary>
public abstract class LzAuthorization : ILzAuthorization
{
    protected bool authenticate = true;
    protected Dictionary<string, List<string>> methodPermissions = new();
    protected bool permissionsLoaded;

    /// <summary>
    /// This method looks up the methodName and compares the required permissions (if any)
    /// for that method against the list of permissions passed in userPermissions. If at
    /// least one common permission is found the routine returns true.
    /// </summary>
    /// <param name="methodName"></param>
    /// <param name="userPermissions"></param>
    /// <returns>Task<bool></bool></returns>
    public virtual async Task<bool> HasPermissionAsync(string methodName, List<string> userPermissions)
    {
        if (!permissionsLoaded)
        {
            await LoadPermissionsAsync();
            permissionsLoaded = true;
        }
        if (methodPermissions.TryGetValue(methodName, out List<string>? permissions)) 
        {
            var commonElements = userPermissions.Intersect(permissions);
            return commonElements.Any();
        }
        return false;
    }
    /// <summary>
    /// This method is called by the generated Controller implementation. 
    /// - Setting the dynamo defaultTable the app uses. We do this here in case we 
    ///   need to switch among different dynamo defaultTable by "tenancy".
    /// - Getting the AWS UserId 
    /// - Checking Permissions 
    /// </summary>
    /// <param name="request"></param>
    /// <returns>CallerInfo</returns>
    public virtual async Task<ICallerInfo> GetCallerInfoAsync(HttpRequest request, [CallerMemberName] string endpointName = "")
    {
        try
        {
            // Grab the non-tenancy request information
            (string lzUserId, string userName) = GetUserInfo(request);
            var headers = GetLzHeaders(request);
            string sessionId = GetSessionId(request);
            var callerInfo = new CallerInfo
            {
                LzUserId = lzUserId,
                UserName = userName,
                Headers = headers,
                SessionId = sessionId,
                // Tenant Config -- properties set by AddConfigAsync
                // Permissions = permissions - property set by GetUserPermissionsAsync
            };

            // Add the tenant config properties. This can be passed in the
            // request headers or loaded from a persistent store based
            // on the request header host.
            await AddConfigAsync(request, callerInfo);

            var permissions = await GetUserPermissionsAsync(lzUserId, userName, callerInfo.DefaultTenant!);

            // Check if user has permission for endpoint 
            if (!await HasPermissionAsync(endpointName, permissions))
                throw new Exception($"User {userName} does not have permission to access method {endpointName}");

            callerInfo.Permissions = permissions;

            return callerInfo;
        }
        catch (Exception)
        {
            throw new Exception("Could not get caller info.");
        }
    }

    protected virtual Dictionary<string, string> GetLzHeaders(HttpRequest request)
    {
        // Adds deployment platform specific headers to the CallerInfo.Headers dictionary.
        // Example: lz-aws-* headers for AWS deployments. The values of these headers 
        // are required by deployment platform specific code in the repository layer.
        // These headers are usually added by the Container,  a reverse proxy like a
        // CloudFront function, or in the dev WebApi request pipeline.
        return request.Headers
            .Where(header => header.Key.StartsWith("lz-", StringComparison.OrdinalIgnoreCase) && !header.Key.StartsWith("lz-config"))
            .ToDictionary(
                header => header.Key,
                header => header.Value.ToString()
            );
    }


    protected virtual Task AddConfigAsync(HttpRequest request, ICallerInfo callerInfo)
    {
        // By default we assume the request contains an lz-config header with the tenancy information.
        // You can override this method to implement a different strategy for getting the tenancy information.
        // For example, you could use the request host to look up the tenancy information in a persistent store.
        // We use the lz-config header as a default because the AWS CloudFront function already pulled the 
        // config information from the AWS CF KVS and added it as the value for the lz-config header.
        // The local WebApi request pipeline does the same.
        var configJson = request.Headers["lz-config"];
        var tenantId = request.Headers["lz-tenantid"]; // usually the host: tenant.tld or subtenant.tenant.tld 
        var tenancyConfig = new TenancyConfig(configJson!, tenantId!);

        // CallerInfo contains tenancy information potentially useful to the repository layer. For instance,
        // the DefaultDB is the DynamoDB table name for the default tenant.
        callerInfo.TenantId = tenancyConfig.Id;
        callerInfo.System = tenancyConfig.System;
        callerInfo.Tenant = tenancyConfig.Tenant;
        callerInfo.Subtenant = tenancyConfig.Subtenant;
        callerInfo.SystemDB = tenancyConfig.SystemDB;
        callerInfo.TenantDB = tenancyConfig.TenantDB;
        callerInfo.SubtenantDB = tenancyConfig.SubtenantDB;
        callerInfo.SystemAssets = tenancyConfig.SystemAssets;
        callerInfo.TenantAssets = tenancyConfig.TenantAssets;
        callerInfo.SubtenantAssets = tenancyConfig.SubtenantAssets;
        callerInfo.DefaultTenant = tenancyConfig.DefaultTenant;
        callerInfo.DefaultDB = tenancyConfig.DefaultDB;
        callerInfo.DefaultAssets = tenancyConfig.DefaultAssets;

        return Task.CompletedTask;
    }

    // Extract SessionId information
    public virtual string GetSessionId(HttpRequest request)
    {
        var foundSessionIdhHeader = request.Headers.TryGetValue("SessionId", out Microsoft.Extensions.Primitives.StringValues sessionIdHeader);
        if (foundSessionIdhHeader)
            return sessionIdHeader[0]!.ToString();
        return "";
    }

    // Extract user identity information
    public virtual (string lzUserId, string userName) GetUserInfo(HttpRequest request)
    {
        if (!authenticate)
            return ("", "");

        var foundAuthHeader = request.Headers.TryGetValue("Authorization", out Microsoft.Extensions.Primitives.StringValues authHeader);
        // When the Authorization header doesn't contain an identity token, we look for the lz-config-identity header.
        // You can add a lz-config-identity header in the client code, in a reverse proxy, or in the container depending 
        // on your deployment platform strategy.
        if (!foundAuthHeader || authHeader[0]!.ToString().StartsWith("AWS4-HMAC-SHA256 Credential="))
            foundAuthHeader = request.Headers.TryGetValue("lz-config-identity", out authHeader);
        if (foundAuthHeader)
            return GetUserInfo(authHeader);

        throw new Exception("No Authorization or lz-config-identity header");
    }
    public virtual (string lzUserId, string userName) GetUserInfo(string? header)
    {
        if(header != null)
        {
            var handler = new JwtSecurityTokenHandler();
            if(header.StartsWith("Bearer "))
                header = header["Bearer ".Length..].Trim();
            if (handler.CanReadToken(header))
            {
                var jwtToken = handler.ReadJwtToken(header);
                var userIdClaim = jwtToken?.Claims.Where(x => x.Type.Equals("sub")).FirstOrDefault();
                var lzUserId = userIdClaim?.Value ?? string.Empty;
                var userNameClaim = jwtToken?.Claims.Where(x => x.Type.Equals("username")).FirstOrDefault();
                var userName = userNameClaim?.Value ?? string.Empty;
                return(lzUserId, userName);
            }
            throw new Exception("Could not read token in Authorization or lz-config-identity header.");
        }
        throw new Exception("No Authorization or lz-config-identity header");
    }
    protected virtual async Task<List<string>> GetUserPermissionsAsync(string lzUserId, string userName, string tenancy)
    {
        await Task.Delay(0);
        return new List<string>();
    }
    protected virtual async Task LoadPermissionsAsync()
    {
        await Task.Delay(0);
    }
    
}
