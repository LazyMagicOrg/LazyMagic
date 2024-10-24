
namespace LazyMagic.Client.Base;
public interface ILzClientConfig
{
    bool ConfigureError { get; set; }
    bool Configured { get; set; }
    string ConfigError { get; set; }
    JObject TenancyConfig { get; set; }
    Dictionary<string, JObject> AuthConfigs { get; set; }
    string TenantKey { get; set; }
    string Type { get; set; }
    string Region { get; set; }

    Task ReadAuthConfigAsync(string configFilePath);
    Task ReadTenancyConfigAsync(string configFilePath);
}