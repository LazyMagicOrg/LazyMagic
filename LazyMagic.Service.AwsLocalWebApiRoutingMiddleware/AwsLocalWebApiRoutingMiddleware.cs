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
        (_systemKey, _defaultTenancy) = ReadSystemConfig().Result;
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
    protected Task<(string systemKey, string defaultTenancy)> ReadSystemConfig()
    {
        var systemKey = "";
        var defaultTenancy = "";
        // Read systemconfig.yaml
        using (var reader = new StreamReader("../../systemconfig.yaml"))
        {
            var yaml = new YamlStream();
            yaml.Load(reader);
            var mapping = (YamlMappingNode)yaml.Documents[0].RootNode;

            // Get SystemKey
            if (mapping.Children.TryGetValue(new YamlScalarNode("SystemKey"), out var systemKeyNode))
            {
                systemKey = ((YamlScalarNode)systemKeyNode).Value!;
            }
            else throw new Exception("SystemKey not found in systemconfig.yaml");

            // Get Default Tenancy
            if (mapping.Children.TryGetValue(new YamlScalarNode("DefaultTenant"), out var tenantNode))
            {
                defaultTenancy = ((YamlScalarNode)tenantNode).Value!;
            }
            else throw new Exception("No default tenant found in systemconfig.yaml");
        }
        return Task.FromResult((systemKey, defaultTenancy));
    }
    public async Task<string> GetTenancyConfigJsonAsync(string key)
    {
        try
        {
            var cloudFrontKvsClient = new AmazonCloudFrontKeyValueStoreClient();
            
            var request = new GetKeyRequest
            {
                KvsARN = _kvsArn,
                Key = key
            };

            var response = await cloudFrontKvsClient.GetKeyAsync(request);
            
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
