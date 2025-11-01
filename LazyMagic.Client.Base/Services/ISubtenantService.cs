namespace LazyMagic.Client.Base.Services;

/// <summary>
/// Service for managing subtenant context in multi-tenant applications
/// Handles storing and retrieving subtenant information from cookies and localStorage
/// </summary>
public interface ISubtenantService
{
    /// <summary>
    /// Gets the current subtenant identifier
    /// Checks cookie first, then falls back to localStorage
    /// </summary>
    /// <returns>The subtenant identifier or null if not set</returns>
    Task<string?> GetSubtenantAsync();

    /// <summary>
    /// Sets the current subtenant identifier
    /// Stores in both cookie (for CloudFront) and localStorage (for easy access)
    /// </summary>
    /// <param name="subtenant">The subtenant identifier to set</param>
    Task SetSubtenantAsync(string subtenant);

    /// <summary>
    /// Clears the subtenant from both cookie and localStorage
    /// </summary>
    Task ClearSubtenantAsync();

    /// <summary>
    /// Gets the root domain for the current hostname
    /// Used for setting cross-subdomain cookies
    /// </summary>
    /// <returns>Root domain (e.g., "lazymagicdev.click" from "uptown.lazymagicdev.click")</returns>
    Task<string> GetRootDomainAsync();

    /// <summary>
    /// Gets the current hostname from the browser
    /// </summary>
    /// <returns>Current hostname</returns>
    Task<string> GetHostnameAsync();
}
