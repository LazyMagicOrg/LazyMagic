
namespace LazyMagic.Client.Base;
public interface ILzClientConfig : IOidcConfig
{
    bool ConfigureError { get; set; }
    bool Configured { get; set; }
    string ConfigError { get; set; }
    JObject TenancyConfig { get; set; }
    // AuthConfigs and EventsApis are inherited from IOidcConfig
    string TenantKey { get; set; }
    string Type { get; set; }
    string Region { get; set; }

    Task InitializeAsync(string hostUrl);
}