namespace LazyMagic.Service.AwsLocalWebApiRoutingMiddleware;


using Amazon.CloudFrontKeyValueStore;
using Amazon.CloudFrontKeyValueStore.Model;
using Amazon.CloudFront;
using Amazon.CloudFront.Model;
using YamlDotNet.RepresentationModel;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

/// <summary>
/// The AwsTenancyConfigService class is an implementation of the ITenancyConfigService
/// 
/// It is only useful in a local web service environment where the systemconfig.yaml
/// file can be read. This routine is used to mimic the behavior of the CloudFront 
/// {systemKey}---request function, which adds headers to the request required by 
/// the LzAuthorization middleware.
/// 
/// The constructor
/// 1. Reads the local systemconfig.yaml file to get the systemKey and defaultTenancy.
/// 2. Reads the system's CloudFront KeyValueStore named {systemKey}---kvs to get the ARN of the KeyValueStore. 
/// 3. Loads the _defaultTenancy config, which is subsequently used to set headers for each Api request.
/// </summary>
public class AwsLocalWebApiRoutingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IAmazonCloudFrontKeyValueStore _cloudFrontKeyValueStore;
    private readonly IAmazonCloudFront _cloudFront;
    private readonly ILogger<AwsLocalWebApiRoutingMiddleware> _logger;
    private string _systemKey;
    private string _kvsArn;
    private string _defaultTenancy = "";
    private string _defaultAuthname = "";
    private string _tenancyConfigPackedJson;

    public AwsLocalWebApiRoutingMiddleware(
        RequestDelegate next,
        IAmazonCloudFrontKeyValueStore cloudFrontKvs,
        IAmazonCloudFront cloudFront,
        ILogger<AwsLocalWebApiRoutingMiddleware> logger
        )
    {
        _next = next;
        _cloudFrontKeyValueStore = cloudFrontKvs;
        _cloudFront = cloudFront;
        _logger = logger;
        (_systemKey, _defaultTenancy, _defaultAuthname) = ReadSystemConfig().Result;
        _kvsArn = GetKvsArnByNameAsync(_systemKey! + "---kvs").Result;
        _tenancyConfigPackedJson = GetTenancyConfigJsonAsync(_defaultTenancy).Result;
    }

    private async Task<string> GetKvsArnByNameAsync(string kvsName)
    {
        try
        {
            var request = new ListKeyValueStoresRequest();
            var response = await _cloudFront.ListKeyValueStoresAsync(request);
            var store = response.KeyValueStoreList.Items.FirstOrDefault(s => s.Name == kvsName);
            return store!.ARN;
        }
        catch (AmazonCloudFrontException ex)
        {
            throw new Exception($"CloudFront error getting ARN for {kvsName}: {ex.Message}");
        }
    }
    protected Task<(string systemKey, string defaultTenancy, string defaultAuthname)> ReadSystemConfig()
    {
        var systemKey = "";
        var defaultTenancy = "";
        var defaultAuthname = "tenantauth"; // Default value if not specified

        // DISCOVER the systemconfig rather than reading a fixed "../../systemconfig.yaml".
        // That hardcoded path assumed both a filename and a fixed depth below the workspace
        // root; neither holds. Walk UP from the working directory and take the first ancestor
        // holding one — the same rule the lz CLI applies.
        var systemConfigPath = FindConfigUpward("systemconfig.*.yaml") ?? FindConfigUpward("systemconfig.yaml");
        if (systemConfigPath == null)
            throw new FileNotFoundException(
                "No systemconfig found in the working directory or any ancestor (looked for " +
                $"systemconfig.*.yaml then systemconfig.yaml, upward from '{Directory.GetCurrentDirectory()}').");

        using (var reader = new StreamReader(systemConfigPath))
        {
            var yaml = new YamlStream();
            yaml.Load(reader);
            var mapping = (YamlMappingNode)yaml.Documents[0].RootNode;

            // SystemKey: an inline key when present (the original single-file schema), otherwise
            // the second dotted segment of the filename — lz names these
            // systemconfig.{systemkey}.{env}.yaml and carries no SystemKey key inside.
            if (mapping.Children.TryGetValue(new YamlScalarNode("SystemKey"), out var systemKeyNode))
                systemKey = ((YamlScalarNode)systemKeyNode).Value!;
            else
                systemKey = KeyFromFilename(systemConfigPath, 1)
                    ?? throw new Exception(
                        $"SystemKey not found in {systemConfigPath} and not derivable from its name " +
                        "(expected systemconfig.{systemkey}.{env}.yaml).");

            // DefaultTenant: likewise inline when present, otherwise the tenant key from a sibling
            // tenantconfig.{systemkey}.{tenantkey}.{env}.yaml. With several tenants the first by
            // ordinal name wins — deterministic, and a local dev host only needs one.
            if (mapping.Children.TryGetValue(new YamlScalarNode("DefaultTenant"), out var tenantNode))
                defaultTenancy = ((YamlScalarNode)tenantNode).Value!;
            else
            {
                var dir = Path.GetDirectoryName(systemConfigPath)!;
                var tenantConfig = new DirectoryInfo(dir)
                    .GetFiles($"tenantconfig.{systemKey}.*.yaml")
                    .OrderBy(f => f.Name, StringComparer.Ordinal)
                    .FirstOrDefault();
                defaultTenancy = tenantConfig == null
                    ? throw new Exception(
                        $"No default tenant: {systemConfigPath} has no DefaultTenant key and no " +
                        $"tenantconfig.{systemKey}.*.yaml sits beside it.")
                    : KeyFromFilename(tenantConfig.FullName, 2)
                        ?? throw new Exception(
                            $"Could not derive the tenant key from {tenantConfig.Name} " +
                            "(expected tenantconfig.{systemkey}.{tenantkey}.{env}.yaml).");
            }

            // Get Default Authname (optional - defaults to "tenantauth" if not specified)
            if (mapping.Children.TryGetValue(new YamlScalarNode("DefaultAuthname"), out var authnameNode))
            {
                defaultAuthname = ((YamlScalarNode)authnameNode).Value!;
            }
        }
        return Task.FromResult((systemKey, defaultTenancy, defaultAuthname));
    }

    /// <summary>First ancestor of the working directory holding a file matching
    /// <paramref name="pattern"/>, or null. Ties break on ordinal filename.</summary>
    private static string? FindConfigUpward(string pattern)
    {
        for (var probe = new DirectoryInfo(Directory.GetCurrentDirectory()); probe != null; probe = probe.Parent)
        {
            var matches = probe.GetFiles(pattern);
            if (matches.Length > 0)
                return matches.OrderBy(f => f.Name, StringComparer.Ordinal).First().FullName;
        }
        return null;
    }

    /// <summary>The dotted segment at <paramref name="index"/> of a config filename
    /// (systemconfig.{sk}.{env}.yaml, tenantconfig.{sk}.{tk}.{env}.yaml), or null when the
    /// name does not have that shape.</summary>
    private static string? KeyFromFilename(string path, int index)
    {
        var parts = Path.GetFileName(path).Split('.');
        // parts = [kind, key…, env, "yaml"] — need a segment at index that is not the env or extension.
        return parts.Length >= index + 3 ? parts[index] : null;
    }
    public async Task<string> GetTenancyConfigJsonAsync(string key)
    {
        try
        {
            var request = new GetKeyRequest
            {
                KvsARN = _kvsArn,
                Key = key
            };

            var response = await _cloudFrontKeyValueStore.GetKeyAsync(request);
            
            if (response.Value == null)
            {
                return "";
            }
            return response.Value;
        }
        catch (Exception e)
        {
            throw new Exception($"Failed to get tenancy config for key '{key}'. {e.Message}");
        }
    }
    /// <summary>
    /// InvokeAsync is called by the middleware pipeline. It adds headers to
    /// each request process in the pipeline.
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public async Task InvokeAsync(HttpContext context)
    {

        var path = context.Request.Path.Value;

        context.Request.Headers.Append("lz-aws-kvsarn", _kvsArn);
        context.Request.Headers.Append("lz-tenantid", _defaultTenancy);
        context.Request.Headers.Append("lz-config", _tenancyConfigPackedJson);

        // Add lz-authname header for multi-tenant AppSync Events routing
        // This allows AppSyncWsEventPublisher to route events to the correct EventsApi
        // based on the authenticator (e.g., "tenantauth" or "consumerauth")
        // Only add if not already present to avoid duplicates
        if (!string.IsNullOrEmpty(_defaultAuthname) && !context.Request.Headers.ContainsKey("lz-authname"))
        {
            context.Request.Headers.Append("lz-authname", _defaultAuthname);
            _logger.LogDebug("Added lz-authname header: {Authname}", _defaultAuthname);
        }

        // leaving lz-config-authorization out for now as it is only useful
        // for the legacy REST API when using v4 signing.

        // Remove the first segment of the path. 
        // In the CloudFront Function, we use the first part of the path 
        // to idenify which Api Gatway to send the request to. In this 
        // local web host, we have aggregated the routes into a single
        // host, so we just remove the api idenfiier from the path.
        var newPath = "/" + string.Join("/", path.Split('/').Skip(2));
        context.Request.Path = newPath;
        await _next(context);
    }
}

// Extension method used to add the middleware to the HTTP request pipeline.
public static class AwsLocalWebApiRoutingMiddlewareExtensions
{
    public static IApplicationBuilder UseAwsLocalWebApiRoutingMiddleware(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<AwsLocalWebApiRoutingMiddleware>();
    }
}
