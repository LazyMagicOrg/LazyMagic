using Microsoft.Extensions.Primitives;
namespace LazyMagic.Service.Authorization;
/// <summary>
/// This abstract class performs common housekeeping tasks for 
/// controllers. You must implement at least:
/// LoadPermissionsAsync() - initializes defaultPerm, adminPerm, methodPermissions
/// 
/// The virtual method GetCallerInfoAsync() is called with each endpoint call to allow 
/// you to implement various strategies for updating user permissions. GetCallerInfoAsync()
/// calls various helper methods. Here is the outline of that process:
/// Endpoint() 
///   callerInfo = GetCallerInfoAsync(request)
///     tenantKey = GetTenantKeyAsync(request)
///     table = GetTenantTableAsync(tenantKey)
///     (lzUserId, userName) = GetUserInfo(request)
///     List<string> permissions = GetUserPermissionsAsync(lzUserId, userName, tenantKey)
///     return new CallerInfo(lzUserId, userName, table, permissions)
///   hasPermission = HasPermissionAsync(methodName, callerInfo.permissions) 
///   
/// Once we have the CallerInfo object, we pass it along to repository methods. The 
/// table property in the CallerInfo object is used to determine which DynamoDB table 
/// the repository will access. Note that some endpoints may make multiple repository 
/// calls using different tables or just a table different from the default tenant table. 
/// In those cases, override the endpoint method to update the table property before 
/// you make the repository call.
/// </summary>
public abstract class LzAuthorization : ILzAuthorization
{
    protected List<string> defaultPerm = new();
    protected List<string> adminPerm = new();
    protected bool authenticate = true;
    protected Dictionary<string, List<string>> methodPermissions = new();
    protected string dynamoDbTable = string.Empty;
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
    /// - Setting the dynamo table the app uses. We do this here in case we 
    ///   need to switch among different dynamo table by "tenancy".
    /// - Getting the AWS UserId 
    /// - Checking Permissions 
    /// </summary>
    /// <param name="request"></param>
    /// <returns>CallerInfo</returns>
    public virtual async Task<ICallerInfo> GetCallerInfoAsync(HttpRequest request, [CallerMemberName] string endpointName = "")
    {
        try
        {
            // TentantKey Header 
            string tenantKey = await GetTenantKeyAsync(request);

            // TenantKey Header - Used to get tenant table
            string table = await GetTenantTableAsync(tenantKey);

            string tenantConfigBucket = await GetTenantConfigBucketAsync(request, tenantKey);

            // Authorization Header  - used to get user identity
            (string lzUserId, string userName) = GetUserInfo(request);

            // Get Permissions 
            var permissions = await GetUserPermissionsAsync(lzUserId, userName, table);

            // Check if user has permission for endpoint 
            if (!await HasPermissionAsync(endpointName, permissions))
                throw new Exception($"User {userName} does not have permission to access method {endpointName}");

            // User info 
            return new CallerInfo()
            {
                LzUserId = lzUserId,
                UserName = userName,
                Table = table,
                TenantConfigBucket = tenantConfigBucket,
                Permissions = permissions,
                Tenancy = tenantKey 
            };
        }
        catch (Exception)
        {
            throw new Exception("Could not get caller info.");
        }
    }
    public virtual async Task<string> GetTenantKeyAsync(HttpRequest request)
    {
        await Task.Delay(0);

        // First look for TenantKey header
        StringValues value;
        if (request.Headers.TryGetValue("tenantKey", out value))
            return value!;
        return "";
    }

    public virtual async Task<string> GetTenantConfigBucketAsync(HttpRequest request, string tnenatKey)
    {
        await Task.Delay(0);

        // First look for TenantKey header
        StringValues value;
        if (request.Headers.TryGetValue("TenantConfigBucket", out value))
        {
            var newValue = value.ToString();
            if (newValue.Contains("{tenantKey}"))
                newValue = newValue.Replace("{tenantKey}", tnenatKey);
            return newValue!;
        }

        return "";
    }
    public virtual async Task<string> GetTenantTableAsync(string tenantKey)
    {
        await Task.Delay(0);
        // Override this method to return the table for a tenantKey if its not the same as the tenantKey
        return tenantKey; 
    }
    // Extract user identity information
    public virtual (string lzUserId, string userName) GetUserInfo(HttpRequest request)
    {
        if (!authenticate)
            return ("", "");

        var foundAuthHeader = request.Headers.TryGetValue("Authorization", out Microsoft.Extensions.Primitives.StringValues authHeader);
        // The original ApiGateway does not pass the Authorization header through to the 
        // Lambda. Our LzHttpClient adds it's own header, LzIdentity, so we have the 
        // information we need.
        if (!foundAuthHeader || authHeader[0]!.ToString().StartsWith("AWS4-HMAC-SHA256 Credential="))
            foundAuthHeader = request.Headers.TryGetValue("LzIdentity", out authHeader);
        if (foundAuthHeader)
            return GetUserInfo(authHeader);

        throw new Exception("No Authorization or LzIdentity header");
    }
    public virtual (string lzUserId, string userName) GetUserInfo(string? header)
    {
        if(header != null)
        {
            var handler = new JwtSecurityTokenHandler();
            if (handler.CanReadToken(header))
            {
                var jwtToken = handler.ReadJwtToken(header);
                var userIdClaim = jwtToken?.Claims.Where(x => x.Type.Equals("sub")).FirstOrDefault();
                var lzUserId = userIdClaim?.Value ?? string.Empty;
                var userNameClaim = jwtToken?.Claims.Where(x => x.Type.Equals("cognito:username")).FirstOrDefault();
                var userName = userNameClaim?.Value ?? string.Empty;
                return(lzUserId, userName);
            }
            throw new Exception("Could not read token in Authorization or LzIdentity header.");
        }
        throw new Exception("No Authorization or LzIdentity header");
    }
    protected virtual async Task<List<string>> GetUserPermissionsAsync(string lzUserId, string userName, string table)
    {
        await Task.Delay(0);
        return new List<string>();
    }
    protected virtual async Task LoadPermissionsAsync()
    {
        await Task.Delay(0);
    }
    
}
