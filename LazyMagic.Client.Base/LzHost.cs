namespace LazyMagic.Client.Base;

public interface ILzHost
{
    string AppPath { get; set; }
    string RemoteApiUrl { get; set; } 
    string LocalApiUrl { get; set; }    
    string AssetsUrl { get; set; } 
    string AppUrl { get; set; }
    string AndroidAppUrl { get; set;}
    string WsUrl { get; set; }
    bool IsMAUI { get; set; }
    bool IsWASM { get; }
    bool IsAndroid { get; set; }    
    bool IsLocal { get; set;  }   
    bool UseLocalhostApi { get; set; }

    string GetApiUrl(string path);
    string GetAssetsUrl(string path);    
    
}

public class LzHost : ILzHost
{
    /// <summary>
    /// Thie LzHost class is used to store the host information for the application. It is 
    /// injected in the Program.cs or MauiProgram.cs startup. Some of its properties are 
    /// set by the indexinit.js file while the initial page loads. Others are set by the 
    /// Program.cs or MauiProgram.cs based on the type of application and, for the 
    /// Program.cs (PWA running locally), the builder.HostEnvironment.
    /// 
    /// Refer to the Program.cs, MauiProgram.cs and the indexinit.js files for 
    /// speicfics on how these properties are set.
    /// 
    /// </summary>
    /// <param name="appPath">Path for app on cloud host. Example "/myapp". Not used for Localhost or MAUI. Value defined in appConfig.js </param>
    /// <param name="appUrl">url application is served from. </param>
    /// <param name="androidAppUrl">url for local android proxy app. Set by indexinit.js.</param>
    /// <param name="remoteApiUrl">Cloud service api. Set by indexinit.js.</param>
    /// <param name="localApiUrl">locahost service api. Set by indexinit.js. </param>
    /// <param name="assetsUrl">cloud url for assets. Set by indexinit.js.</param>
    /// <param name="wsUrl">Websocket url, always served from cloud. Set by indexinit.js</param>
    /// <param name="isMAUI">Is a MAUI app. Set by Program.cs or MauiProgram.cs</param>
    /// <param name="isAndroid">Is an Andriod app. Set by Program.cs or MauiProgram.cs</param>
    /// <param name="isLocal">Is running local. Set by Program.cs or MauiProgram.cs</param>
    /// <param name="useLocalhostApi">use localhost service API. Set by Program.cs or MauiProgram.cs</param>
    public LzHost(
        string? appPath = null,
        string? appUrl = null,
        string? androidAppUrl = null,
        string? remoteApiUrl = null, 
        string? localApiUrl = null, 
        string? assetsUrl = null, 
        string? wsUrl = null, 
        bool isMAUI = true, 
        bool isAndroid = false, 
        bool isLocal = false, 
        bool useLocalhostApi = false
        )
    {
        AppPath = appPath ?? "";
        RemoteApiUrl = remoteApiUrl ?? "";
        LocalApiUrl = localApiUrl ?? "";
        AssetsUrl = assetsUrl ?? "";
        AppUrl = appUrl ?? "";  
        AndroidAppUrl = appUrl ?? "";
        WsUrl = wsUrl ?? "";
        IsMAUI = isMAUI;
        IsAndroid = isAndroid;
        IsLocal = isLocal;
        UseLocalhostApi = useLocalhostApi;
    }

    public string AppPath { get; set; } = string.Empty;
    public string AppUrl { get; set; } = string.Empty;
    public string AndroidAppUrl { get; set; } = string.Empty;
    public string RemoteApiUrl { get; set; } = string.Empty;
    public string LocalApiUrl { get; set; } = string.Empty; 
    public string AssetsUrl { get; set; } = string.Empty;
    public string WsUrl { get; set; } = string.Empty;
    public bool IsMAUI { get; set; }
    public bool IsWASM => !IsMAUI;
    public bool IsAndroid { get; set; }
    public bool IsLocal { get; set; }   
    public bool UseLocalhostApi { get; set; }

    public string GetApiUrl(string path) => UseLocalhostApi ? LocalApiUrl + path : RemoteApiUrl + path;
    public string GetAssetsUrl(string path) => AssetsUrl + path;
}
