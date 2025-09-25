namespace LazyMagic.Shared;
public class BrowserFingerprint
{
    // Basic info
    [JsonPropertyName("fingerprint")]
    public long Fingerprint { get; set; }
    
    [JsonPropertyName("browser")]
    public string Browser { get; set; } = string.Empty;
    
    [JsonPropertyName("browserVersion")]
    public string BrowserVersion { get; set; } = string.Empty;
    
    [JsonPropertyName("os")]
    public string OS { get; set; } = string.Empty;
    
    [JsonPropertyName("osVersion")]
    public string OSVersion { get; set; } = string.Empty;
    
    [JsonPropertyName("resolution")]
    public string Resolution { get; set; } = string.Empty;
    
    [JsonPropertyName("userAgent")]
    public string UserAgent { get; set; } = string.Empty;
    
    [JsonPropertyName("device")]
    public string Device { get; set; } = string.Empty;
    
    [JsonPropertyName("deviceType")]
    public string DeviceType { get; set; } = string.Empty;
    
    [JsonPropertyName("deviceVendor")]
    public string DeviceVendor { get; set; } = string.Empty;
    
    [JsonPropertyName("language")]
    public string Language { get; set; } = string.Empty;
    
    [JsonPropertyName("timeZone")]
    public string TimeZone { get; set; } = string.Empty;
    
    [JsonPropertyName("screenPrint")]
    public string ScreenPrint { get; set; } = string.Empty;
    
    // Browser flags - only populated when using GetExtendedBrowserInfoAsync
    [JsonPropertyName("isMobile")]
    public bool IsMobile { get; set; }
    
    [JsonPropertyName("isChrome")]
    public bool IsChrome { get; set; }
    
    [JsonPropertyName("isFirefox")]
    public bool IsFirefox { get; set; }
    
    [JsonPropertyName("isSafari")]
    public bool IsSafari { get; set; }
    
    [JsonPropertyName("isIE")]
    public bool IsIE { get; set; }
}