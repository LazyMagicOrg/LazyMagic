namespace LazyMagic.Client.Base.Services;

/// <summary>
/// Implementation of ISubtenantService for managing subtenant context
/// in multi-tenant applications with CloudFront routing
/// </summary>
public class SubtenantService : ISubtenantService
{
    private readonly ILzJsUtilities _jsUtilities;
    private readonly ILogger<SubtenantService>? _logger;

    public SubtenantService(ILzJsUtilities jsUtilities, ILogger<SubtenantService>? logger = null)
    {
        _jsUtilities = jsUtilities ?? throw new ArgumentNullException(nameof(jsUtilities));
        _logger = logger;
    }

    /// <summary>
    /// Gets the current subtenant identifier
    /// Priority: cookie > localStorage
    /// </summary>
    public async Task<string?> GetSubtenantAsync()
    {
        try
        {
            // Try cookie first (CloudFront accessible)
            var subtenant = await _jsUtilities.GetCookie("lz-subtenant");
            if (!string.IsNullOrEmpty(subtenant))
            {
                _logger?.LogDebug("Subtenant retrieved from cookie: {Subtenant}", subtenant);
                return subtenant;
            }

            // Fall back to localStorage
            subtenant = await _jsUtilities.GetItem("lz-subtenant");
            if (!string.IsNullOrEmpty(subtenant))
            {
                _logger?.LogDebug("Subtenant retrieved from localStorage: {Subtenant}", subtenant);
                return subtenant;
            }

            _logger?.LogDebug("No subtenant found in cookie or localStorage");
            return null;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error getting subtenant");
            return null;
        }
    }

    /// <summary>
    /// Sets the current subtenant identifier in both cookie and localStorage
    /// </summary>
    public async Task SetSubtenantAsync(string subtenant)
    {
        if (string.IsNullOrEmpty(subtenant))
            throw new ArgumentNullException(nameof(subtenant));

        try
        {
            var rootDomain = await GetRootDomainAsync();

            // Store in cookie (cross-subdomain, CloudFront accessible)
            var cookieOptions = new CookieOptions
            {
                Domain = $".{rootDomain}",
                Path = "/",
                Secure = true,
                SameSite = "Lax",
                Days = 30
            };

            await _jsUtilities.SetCookie("lz-subtenant", subtenant, cookieOptions);
            _logger?.LogInformation("Subtenant cookie set: {Subtenant} for domain: {Domain}", subtenant, cookieOptions.Domain);

            // Also store in localStorage (easy access, faster reads)
            await _jsUtilities.SetItem("lz-subtenant", subtenant);
            _logger?.LogDebug("Subtenant stored in localStorage: {Subtenant}", subtenant);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error setting subtenant: {Subtenant}", subtenant);
            throw;
        }
    }

    /// <summary>
    /// Clears the subtenant from both cookie and localStorage
    /// </summary>
    public async Task ClearSubtenantAsync()
    {
        try
        {
            var rootDomain = await GetRootDomainAsync();

            // Clear cookie
            var cookieOptions = new CookieOptions
            {
                Domain = $".{rootDomain}",
                Path = "/"
            };

            await _jsUtilities.DeleteCookie("lz-subtenant", cookieOptions);
            _logger?.LogInformation("Subtenant cookie cleared");

            // Clear localStorage
            await _jsUtilities.RemoveItem("lz-subtenant");
            _logger?.LogDebug("Subtenant cleared from localStorage");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error clearing subtenant");
            throw;
        }
    }

    /// <summary>
    /// Gets the root domain from the current hostname
    /// Example: "uptown.lazymagicdev.click" -> "lazymagicdev.click"
    /// </summary>
    public async Task<string> GetRootDomainAsync()
    {
        try
        {
            var hostname = await GetHostnameAsync();
            var parts = hostname.Split('.');

            // Return last 2 parts for root domain
            var rootDomain = parts.Length > 2
                ? string.Join('.', parts.TakeLast(2))
                : hostname;

            _logger?.LogDebug("Root domain: {RootDomain} from hostname: {Hostname}", rootDomain, hostname);
            return rootDomain;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error getting root domain");
            throw;
        }
    }

    /// <summary>
    /// Gets the current hostname from the browser
    /// </summary>
    public async Task<string> GetHostnameAsync()
    {
        try
        {
            // Use JS interop to get window.location.hostname
            var hostname = await _jsUtilities.GetHostnameAsync();
            _logger?.LogDebug("Current hostname: {Hostname}", hostname);
            return hostname ?? "localhost";
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error getting hostname");
            return "localhost";
        }
    }
}
