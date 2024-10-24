using System.Net.Http;
using System;

namespace LazyMagic.Client.Base;

public class LzClientConfig : ILzClientConfig
{
    public LzClientConfig(ILzHost host, HttpClient httpClient)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _httpClient = httpClient;

    }
    public Dictionary<string,JObject> AuthConfigs { get; set; } = new();
    public JObject TenancyConfig { get; set; } = JObject.Parse("{}");
    public string TenantKey { get; set; } = "";
    public string Type { get; set; } = "";
    public string Region { get; set; } = "";
  

    protected ILzHost _host;
    protected HttpClient _httpClient;

    public bool ConfigureError { get; set; }
    public bool Configured { get; set; }
    public string ConfigError { get; set; } = "";   

    /// <summary>
    /// You may call ReadTenancyConfigAsync multiple times with different paths. Config content 
    /// are merged with last value(s) found taking precedence.
    /// </summary>
    /// <param name="tenancyConfigPath"></param>
    /// <returns></returns>
    public virtual async Task ReadTenancyConfigAsync(string tenancyConfigPath)
    {
        ConfigError = "";   
        try
        {

            var json = await _httpClient.GetStringAsync(tenancyConfigPath);
            TenancyConfig.Merge(JObject.Parse(json), new JsonMergeSettings
            {
                MergeArrayHandling = MergeArrayHandling.Union
            });
            //JsonConvert.PopulateObject(json, TenancyConfig); // for some reason, this doesn't work
        }
        catch (Exception ex)
        {
            var msg = $"Error reading tenancy config: {ex.Message}";
            ConfigError = msg;
            Console.WriteLine(msg);
        }
    }

    /// <summary>
    /// Reads the AuthConfig file and sets the AuthConfig property based on the provided userPoolName.
    /// </summary>
    /// <param name="authConfigPath"></param>
    /// <param name="userPoolName"></param>
    /// <returns></returns>
    public virtual async Task ReadAuthConfigAsync(string authConfigPath)
    {
        ConfigError = "";
        try
        {

            var configJson = await _httpClient.GetStringAsync(authConfigPath);
            var configDoc = JObject.Parse(configJson);

            var meta = configDoc["meta"];
            if (meta is null)
            {
                ConfigureError = true;
                ConfigError = "AuthConfig missing meta data.";
                return;
            }

            var wsUrl = meta["wsUrl"]?.ToString();
            if (!string.IsNullOrEmpty(wsUrl))
                _host.WsUrl = wsUrl!;

            var tenantKey = meta["tenantKey"]?.ToString();
            if(!string.IsNullOrEmpty(tenantKey))
                TenantKey = tenantKey!;

            var region = meta["awsRegion"]?.ToString();    
            if(!string.IsNullOrEmpty(region))
                Region = region!;

            Configured = true;

            AuthConfigs = configDoc["authConfigs"]?.ToObject<Dictionary<string, JObject>>() 
                ?? new Dictionary<string, JObject>();

        }
        catch (Exception ex)
        {
            var msg = $"Error reading AuthConfig: {ex.Message}";
            ConfigError = msg;
            Console.WriteLine(msg);
        }
    }

}



