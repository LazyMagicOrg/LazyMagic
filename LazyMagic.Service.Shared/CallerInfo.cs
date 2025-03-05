namespace LazyMagic.Service.Shared;

public class CallerInfo : ICallerInfo
{
    public string? TenantId { get; set; }
    public string? LzUserId { get; set; }
    public string? UserName { get; set; }
    public string? SessionId { get; set; }

    // Tenancies
    public string? System { get; set; } 
    public string? Tenant { get; set; }
    public string? Subtenant { get; set; }

    // Databases    
    public string? SystemDB { get; set; }
    public string? TenantDB { get; set; }
    public string? SubtenantDB { get; set; }

    // Assets
    public string? SystemAssets { get; set; }
    public string? TenantAssets { get; set; }
    public string? SubtenantAssets { get; set; }

    // Defaults
    public string? DefaultTenant { get; set; }
    public string? DefaultDB { get; set; }
    public string? DefaultAssets { get; set; }

    // Permissions
    public List<string> Permissions { get; set; } = new();

    // Headers
    // Here we include Headers of interest to our application
    // code. For instance, for AWS we may pass 
    // Region, and UserPoolId entries so our repo code 
    // can call Cognito.
    public Dictionary<string, string> Headers { get; set; } = new();


}
