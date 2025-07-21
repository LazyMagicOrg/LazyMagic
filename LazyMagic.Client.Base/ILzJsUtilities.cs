
namespace LazyMagic.Client.Base;

/// <summary>
/// Represents options for configuring HTTP cookies in JavaScript through Blazor JSInterop.
/// </summary>
/// <remarks>
/// This class provides a strongly-typed way to specify cookie attributes when setting cookies
/// via JavaScript interop. All properties are optional and will use browser defaults if not specified.
/// </remarks>
/// <example>
/// <code>
/// // Simple persistent cookie
/// var options = new CookieOptions { Days = 30 };
/// await jsUtilities.SetCookie("username", "john_doe", options);
/// 
/// // Secure cookie with CSRF protection
/// var secureOptions = new CookieOptions 
/// { 
///     Days = 7,
///     Secure = true,
///     SameSite = "strict",
///     Path = "/"
/// };
/// await jsUtilities.SetCookie("authToken", "abc123", secureOptions);
/// </code>
/// </example>
public class CookieOptions
{
    /// <summary>
    /// Gets or sets the number of days until the cookie expires.
    /// </summary>
    /// <value>
    /// The number of days until expiration. If null, the cookie will be a session cookie
    /// that expires when the browser is closed.
    /// </value>
    /// <example>
    /// <code>
    /// // Cookie expires in 7 days
    /// var options = new CookieOptions { Days = 7 };
    /// 
    /// // Session cookie (expires on browser close)
    /// var options = new CookieOptions { Days = null };
    /// </code>
    /// </example>
    public int? Days { get; set; }

    /// <summary>
    /// Gets or sets the URL path that must exist in the requested URL for the browser to send the cookie.
    /// </summary>
    /// <value>
    /// The path attribute for the cookie. Default is "/" (root path) if not specified.
    /// </value>
    /// <remarks>
    /// Common values include:
    /// <list type="bullet">
    /// <item><description>"/" - Cookie available throughout the entire site</description></item>
    /// <item><description>"/admin" - Cookie only available in admin section</description></item>
    /// <item><description>"/api" - Cookie only available for API routes</description></item>
    /// </list>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Site-wide cookie
    /// var options = new CookieOptions { Path = "/" };
    /// 
    /// // API-only cookie
    /// var options = new CookieOptions { Path = "/api" };
    /// </code>
    /// </example>
    public string Path { get; set; }

    /// <summary>
    /// Gets or sets the domain for which the cookie is valid.
    /// </summary>
    /// <value>
    /// The domain attribute for the cookie. If null, the cookie is only valid for the current domain.
    /// </value>
    /// <remarks>
    /// Prefixing the domain with a dot (.) allows the cookie to be shared across subdomains.
    /// For example, ".example.com" makes the cookie available to www.example.com, api.example.com, etc.
    /// </remarks>
    /// <example>
    /// <code>
    /// // Current domain only
    /// var options = new CookieOptions { Domain = null };
    /// 
    /// // Share across all subdomains
    /// var options = new CookieOptions { Domain = ".example.com" };
    /// </code>
    /// </example>
    public string Domain { get; set; }

    /// <summary>
    /// Gets or sets whether the cookie should only be transmitted over secure HTTPS connections.
    /// </summary>
    /// <value>
    /// True if the cookie should only be sent over HTTPS; false or null if it can be sent over HTTP.
    /// </value>
    /// <remarks>
    /// This should always be set to true for cookies containing sensitive information such as
    /// authentication tokens or personal data. Required when SameSite is set to "none".
    /// </remarks>
    /// <example>
    /// <code>
    /// // Secure cookie for sensitive data
    /// var options = new CookieOptions { Secure = true };
    /// </code>
    /// </example>
    public bool? Secure { get; set; }

    /// <summary>
    /// Gets or sets the SameSite attribute for CSRF protection.
    /// </summary>
    /// <value>
    /// The SameSite mode: "strict", "lax", or "none". If null, browser default is used (typically "lax").
    /// </value>
    /// <remarks>
    /// SameSite controls when cookies are sent with cross-site requests:
    /// <list type="bullet">
    /// <item><description>"strict" - Cookie is never sent with cross-site requests</description></item>
    /// <item><description>"lax" - Cookie is sent with top-level navigation (GET requests)</description></item>
    /// <item><description>"none" - Cookie is sent with all cross-site requests (requires Secure = true)</description></item>
    /// </list>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Maximum CSRF protection
    /// var options = new CookieOptions { SameSite = "strict" };
    /// 
    /// // Allow cross-site usage (requires Secure = true)
    /// var options = new CookieOptions { SameSite = "none", Secure = true };
    /// </code>
    /// </example>
    public string SameSite { get; set; }
}

public interface ILzJsUtilities : INotifyPropertyChanged
{
    string ModuleFileName { get; }
	bool CheckingAssetData { get; }
	bool UpdatingAssetData { get; }
	bool UpdatingServiceWorker { get; }
	string CacheMiss { get; set; }
    void SetJSRuntime(object jsRuntime);
	ValueTask Initialize();
	ValueTask CheckForNewAssetData();
    ValueTask Reload();
    ValueTask<int> GetMemory();
	ValueTask SetPointerCapture(object elementRef, long pointerId);

	ValueTask<string> GetBase64Image(object elementReferenceImg);
    ValueTask<string> GetBase64ImageDownsized(object elementReferenceImg);
	ValueTask<bool> SharePng(string title, string text, string pngData, string? textData = null);
	ValueTask<bool> ShareText(string title, string text);
	ValueTask SetItem(string key, string value);
	ValueTask<string> GetItem(string key);
	ValueTask RemoveItem(string key);
	ValueTask SetCookie(string key, string value, CookieOptions options);
	ValueTask<string> GetCookie(string key);
	ValueTask DeleteCookie(string key, CookieOptions options = null);
	ValueTask<bool> CookieExists(string name);
	ValueTask ClearAllCookies();
	ValueTask<Dictionary<string,string>> GetAllCookies();
	ValueTask SetJSONCookie<T>(string name, T obj, CookieOptions options = null);
    ValueTask<T> GetJSONCookie<T>(string name);

    // Callbacks. ie. [JSInvokable]
    void AssetDataCheckStarted();
	void AssetDataCheckComplete();
    void AssetDataUpdateStarted();
    void AssetDataUpdateComplete();
    void ServiceWorkerUpdateStarted();
    void ServiceWorkerUpdateComplete();
	void CacheMissAction(string url);
	void MessageSelected(string key, string value);

}
