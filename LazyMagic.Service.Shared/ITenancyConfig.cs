
namespace LazyMagic.Service.Shared;

public interface ITenancyConfig
{
    string SubtenantKey { get; set; }
    string SystemKey { get; set; }
    string TenantKey { get; set; }
    string Ss { get; set; }
    string Ts { get; set; }
    string Sts { get; set; }
    string Env { get; set; }
    string Region { get; set; }

    List<Api> Apis { get; set; }
    List<Asset> Assets { get; set; }
    List<WebApp> WebApps { get; set; }

    // Calculated Fields - vary by platform target
    string System { get; }
    string Tenant { get; }
    string Subtenant { get; }
    string SystemAssets { get; }    
    string TenantAssets { get; }
    string SubtenantAssets { get; }
    string SystemDB { get; }
    string TenantDB { get; }
    string SubtenantDB { get; }

    string DefaultTenant { get; }
    string DefaultAssets { get; }
    string DefaultDB { get; }


    // Repo required fields
    long CreateUtcTick { get; set; }
    string Id { get; set; }
    long UpdateUtcTick { get; set; }

    // Methods
    void SetCalculatedFields();
    TenancyConfigPacked Pack();
    void Unpack(TenancyConfigPacked packed, string id);
}