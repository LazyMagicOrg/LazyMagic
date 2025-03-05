using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;  

namespace LazyMagic.Shared;
/// <summary>
/// The TenancyConfig class represents the tenancy configuration for a tenant.
/// The TenancyConfigPacked class represents the same information in a 
/// compact format necessitated by the limitations of storage in services 
/// like the AWS KeyValueStore. 
/// 
/// The largest data in the class are the Behaviors, so we pack these values
/// into a list of lists of strings. The first string in each list contains
/// the comman separated path(s).
/// 
/// When we serialize the TenancyConfigPacked class into JSON, the 
/// resulting size of the JSON is much smaller than if we had just 
/// serialized the TenancyConfig class.
/// 
/// Suffix Management
/// We must use suffixes to create globally unique names for assets and webapps.
/// We assign a System level suffix Ss, tenant level Suffix Ts, and subtenant level
/// suffix Sts. We can often use the same suffix value for all levels, but need to be 
/// able to change this behavior if a naming conflic occurs.
/// 
/// To accomodate the use of defaults, we use a substitution pattern in the suffixes. 
/// The pattern {Ss} is replaced with the System suffix, {Ts} is replaced with the 
/// Tenant suffix, and {Sts} is replaced with the Subtenant suffix. By default 
/// the subtenant suffix is set to {Ts} and the tenant suffix is set to {Ss}.
/// </summary>

public class TenancyConfigBase 
{
    public string SubtenantKey { get; set; } = "";
    public string SystemKey { get; set; } = "";
    public string TenantKey { get; set; } = "";
    public string Ss { get; set; } = "";
    public string Ts { get; set; } = "{Ss}";
    public string Sts { get; set; } = "{Ts}";
    public string Env { get; set; } = "";
    public string Region { get; set; } = "";

}

public class TenancyConfigPacked : TenancyConfigBase
{
    public List<List<string>> Behaviors { get; set; } = new List<List<string>>();
    public string ToJson()
    {
        return JsonConvert.SerializeObject(this);
    }   
}
/// <summary>
/// Represents an API behavior. The packed behavior has this format
///     0             1        2       3      4
/// [path, behaviorType, apiName, region, level]
/// </summary>
public class Api
{
    public string Path { get; set; } = "";
    public string BehaviorType { get; set; } = "api";
    public string ApiName { get; set; } = "";
    public string Region { get; set; } = "";
    public int Level { get; set; }
    public string Name { get; set; } = "";
    public string DBName { get; set; } = "";    
}

/// <summary>
/// Represents an asset behavior. The packed behavior has this format
///     0            1        2       3      4
/// [path, behaviorType, suffix, region, level]
/// </summary>
public class Asset
{
    public string Path { get; set; } = "";
    public string BehaviorType { get; set; } = "asset";
    public string Suffix { get; set; } = "";
    public string Region { get; set; } = "";
    public int Level { get; set; }
    public string Name { get; set; } = "";
}

/// <summary>
/// Represents a webapp behavior. The packed behavior has this format
///     0             1        2       3       4      5
/// [path, behaviorType, appName, suffix, region, level]
/// </summary>
public class WebApp
{
    public string Path { get; set; } = "";
    public string BehaviorType { get; set; } = "webapp";
    public string AppName { get; set; } = "";
    public string Suffix { get; set; } = "";
    public string Region { get; set; } = "";
    public int Level { get; set; }
    public string Name { get; set; } = "";
}

public class TenancyConfig : TenancyConfigBase, IItem, ITenancyConfig
{
    public string Id { get; set; } = "";
    public List<Api> Apis { get; set; } = new List<Api>();
    public List<Asset> Assets { get; set; } = new List<Asset>();
    public List<WebApp> WebApps { get; set; } = new List<WebApp>();


    public string System { get; private set; } = "";
    public string Tenant { get; private set; } = "";
    public string Subtenant { get; private set; } = "";
    public string SystemAssets { get; private set; } = "";
    public string TenantAssets { get; private set; } = "";
    public string SubtenantAssets { get; private set; } = "";
    public string SystemDB { get; private set; } = "";
    public string TenantDB { get; private set; } = "";
    public string SubtenantDB { get; private set; } = "";
    public string DefaultTenant { get; private set; } = "";
    public string DefaultAssets { get; private set; } = "";
    public string DefaultDB { get; private set; } = "";


    public long CreateUtcTick { get; set; }
    public long UpdateUtcTick { get; set; }

    public TenancyConfig()
    {

    }

    public TenancyConfig(string tenancyConfigJson, string id)
    {
        var tenancyConfigPacked = JsonConvert.DeserializeObject<TenancyConfigPacked>(tenancyConfigJson);
        Unpack(tenancyConfigPacked!, id);
        
    }

    public TenancyConfig(TenancyConfigPacked packed, string id)
    {
        Unpack(packed, id);
    }

    public virtual void SetCalculatedFields()
    {
        // Tenants
        System = SystemKey;
        if (!string.IsNullOrEmpty(TenantKey))
            Tenant = $"{System}-{TenantKey}";
        if (!string.IsNullOrEmpty(SubtenantKey))
        {
            Subtenant = $"{Tenant}-{SubtenantKey}";
        }

        // Database 
        SystemDB = SystemKey;
        DefaultDB = SystemDB;
        if (!string.IsNullOrEmpty(TenantKey))
        {
            TenantDB = SystemDB + "_" + TenantKey;
        }
        if (!string.IsNullOrEmpty(SubtenantKey))
        {
            SubtenantDB = TenantDB + "_" + SubtenantKey;
        }

        // Assets
        foreach (var asset in Assets)
        {
            if (asset.Path.StartsWith("/system/"))
            {
                SystemAssets = asset.Name;
            }
            else if (asset.Path.StartsWith("/tenancy/"))
            {
                TenantAssets = asset.Name;
            }
            else if (asset.Path.StartsWith("/subtenancy/"))
            {
                SubtenantAssets = asset.Name;
            }
        }

        // Defaults
        DefaultTenant = Tenant;
        DefaultDB = SystemDB;
        DefaultAssets = SystemAssets;
        if (!string.IsNullOrEmpty(TenantKey))
        {
            DefaultDB = TenantDB;
            DefaultAssets = TenantAssets;
        }
        if (!string.IsNullOrEmpty(SubtenantKey))
        {
            DefaultTenant = Subtenant;
            DefaultDB = SubtenantDB;
            DefaultAssets = SubtenantAssets;
        }
    }


    /// <summary>
    /// Create a TenancyConfig from a TenancyConfigPacked object.
    /// </summary>
    /// <param name="packed"></param>
    /// <param name="id"></param>
    /// <exception cref="Exception"></exception>
    public virtual void Unpack(TenancyConfigPacked packed, string id)
    {
        Id = id;  // This is usually the url of the tenancy config: teaant.tld, subtenant.tenant.tld
        SubtenantKey = packed.SubtenantKey;
        SystemKey = packed.SystemKey;
        TenantKey = packed.TenantKey;
        Ss = packed.Ss;
        Ts = packed.Ts;
        Sts = packed.Sts;
        Env = packed.Env;
        Region = packed.Region;
        foreach (var behavior in packed.Behaviors)
        {
            var behaviorArray = behavior.ToArray();
            var behaviorType = behaviorArray[1];
            switch (behaviorType)
            {
                case "api":
                    var api = new Api
                    {
                        //  0    1         2       3      4
                        // [path,assetType,apiname,region,env]
                        // ==> [apiName].execute-api.[region].amazonaws.com
                        Path = behaviorArray[0],
                        BehaviorType = behaviorArray[1],
                        ApiName = behaviorArray[2],
                        Region = behaviorArray[3],
                        Name = GetApiName(behaviorArray)
                    };
                    Apis.Add(api);
                    break;
                case "assets":
                    var assetLevel = int.Parse(behaviorArray[4]);
                    var asset = new Asset
                    {
                        //  0    1         2         3      4
                        // [path,assetType,assetname,suffix,region,level]
                        // ==> [systemKey]-[tenantKey]-[subtenantKey]-[assetType]-[suffix].s3.[region].amazonaws.com
                        Path = behaviorArray[0],
                        BehaviorType = behaviorArray[1],
                        Suffix = behaviorArray[2],
                        Region = behaviorArray[3],
                        Level = int.Parse(behaviorArray[4] ?? "0"),
                        Name = GetAssetName(behaviorArray)
                    };
                    Assets.Add(asset);
                    break;
                case "webapp":

                    var webapp = new WebApp
                    {
                        //  0    1         2         3      4      5
                        // [path,assetType,assetname,suffix,region,level]
                        // ==> [systemKey]-[tenantKey]-[subtenantKey]-[assetType]-[appName]-[suffix].s3.[region].amazonaws.com
                        Path = behaviorArray[0],
                        BehaviorType = behaviorArray[1],
                        AppName = behaviorArray[2],
                        Suffix = behaviorArray[3],
                        Region = behaviorArray[4],
                        Level = int.Parse(behaviorArray[5] ?? "0"),
                        Name = GetWebAppName(behaviorArray)
                    };
                    WebApps.Add(webapp);
                    break;
                default:
                    throw new Exception($"Unknown behavior type: {behaviorType}");
            }
        }
        SetCalculatedFields();
    }

    /// <summary>
    /// Get the API name from the behavior array.
    /// Override this method for platforms other than 
    /// AWS.
    /// </summary>
    /// <param name="behaviorArray"></param>
    /// <returns></returns>
    protected virtual string GetApiName(string[] behaviorArray)
    {
        return behaviorArray[2] + ".execute-api." + behaviorArray[3] + ".amazon.com";
    }


    /// <summary>
    /// Get the asset name from the behavior array.
    /// Override this method for platforms other than
    /// AWS.
    /// </summary>
    /// <param name="behaviorArray"></param>
    /// <returns></returns>
    protected virtual string GetAssetName(string[] behaviorArray)
    {
        var assetLevel = int.Parse(behaviorArray[4]);
        return ($"{SystemKey}-"
            + (assetLevel > 0 ? $"{TenantKey}-" : "-")
            + (assetLevel > 1 ? $"{SubtenantKey}-" : "-")
            + $"{behaviorArray[1]}-{behaviorArray[2]}.s3.{behaviorArray[3]}.amazonaws.com")
            .Replace("{sts}", Sts)
            .Replace("{ts}", Ts)
            .Replace("{ss}", Ss);
    }
    /// <summary>
    /// Get the webapp name from the behavior array.
    /// Override this method for platforms other than
    /// AWS.
    /// </summary>
    /// <param name="behaviorArray"></param>
    /// <returns></returns>
    protected virtual string GetWebAppName(string[] behaviorArray)
    {
        var webappLevel = int.Parse(behaviorArray[5]);
        return ($"{SystemKey}-"
            + (webappLevel > 0 ? $"{TenantKey}-" : "-")
            + (webappLevel > 1 ? $"{SubtenantKey}-" : "-")
            + $"{behaviorArray[1]}-{behaviorArray[2]}-{behaviorArray[3]}.s3.{behaviorArray[4]}.amazonaws.com")
            .Replace("{sts}", Sts)
            .Replace("{ts}", Ts)
            .Replace("{ss}", Ss);
    }
    public virtual TenancyConfigPacked Pack()
    {
        var packed = new TenancyConfigPacked
        {
            SubtenantKey = SubtenantKey,
            SystemKey = SystemKey,
            TenantKey = TenantKey,
            Ss = Ss,
            Ts = Ts,
            Sts = Sts,
            Env = Env,
            Region = Region,
            Behaviors = new List<List<string>>()
        };
        foreach (var api in Apis)
        {
            packed.Behaviors.Add(new List<string>
            {
                api.Path,
                api.BehaviorType,
                api.ApiName,
                api.Region,
                api.Level.ToString()
            });
        }
        foreach (var asset in Assets)
        {
            packed.Behaviors.Add(new List<string>
            {
                asset.Path,
                asset.BehaviorType,
                asset.Suffix,
                asset.Region,
                asset.Level.ToString()
            });
        }
        foreach (var webapp in WebApps)
        {
            packed.Behaviors.Add(new List<string>
            {
                webapp.Path,
                webapp.BehaviorType,
                webapp.AppName,
                webapp.Suffix,
                webapp.Region,
                webapp.Level.ToString()
            });
        }
        return packed;
    }
}