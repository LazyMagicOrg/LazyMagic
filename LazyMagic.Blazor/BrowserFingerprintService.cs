using System.Text.Json.Serialization;

namespace LazyMagic.Blazor;

/// <summary>
/// This module wraps the browserFigngerprint.js JavaScript module.
/// It is used to gather various browser and device information.
/// </summary>
public class BrowserFingerprintService : LzBaseJSModule
{
    // ModuleFileName is the path to the JS file that will be loaded by the Blazor app.
    public override string ModuleFileName => $"./_content/LazyMagic.Blazor/browserFingerprint.js";

    public async ValueTask<BrowserFingerprint> GetFingerprintAsync()
    {
        try
        {
            // Get all browser info in a single call and directly deserialize to BrowserFingerprint
            var fingerprint = await InvokeSafeAsync<BrowserFingerprint>("getBrowserInfo");
            return fingerprint ?? new BrowserFingerprint();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting browser fingerprint: {ex.Message}");
            return new BrowserFingerprint();
        }
    }

    // Individual property methods
    public async ValueTask<string> GetUserAgentAsync()
    {
        return await InvokeSafeAsync<string>("getUserAgent") ?? string.Empty;
    }

    public async ValueTask<string> GetDeviceAsync()
    {
        return await InvokeSafeAsync<string>("getDevice") ?? string.Empty;
    }

    public async ValueTask<string> GetDeviceTypeAsync()
    {
        return await InvokeSafeAsync<string>("getDeviceType") ?? string.Empty;
    }

    public async ValueTask<string> GetDeviceVendorAsync()
    {
        return await InvokeSafeAsync<string>("getDeviceVendor") ?? string.Empty;
    }

    public async ValueTask<bool> IsMobileAsync()
    {
        return await InvokeSafeAsync<bool>("isMobile");
    }

    public async ValueTask<bool> IsChromeAsync()
    {
        return await InvokeSafeAsync<bool>("isChrome");
    }

    public async ValueTask<bool> IsFirefoxAsync()
    {
        return await InvokeSafeAsync<bool>("isFirefox");
    }

    public async ValueTask<bool> IsSafariAsync()
    {
        return await InvokeSafeAsync<bool>("isSafari");
    }

    public async ValueTask<bool> IsIEAsync()
    {
        return await InvokeSafeAsync<bool>("isIE");
    }

    public async ValueTask<string> GetLanguageAsync()
    {
        return await InvokeSafeAsync<string>("getLanguage") ?? string.Empty;
    }

    public async ValueTask<string> GetTimeZoneAsync()
    {
        return await InvokeSafeAsync<string>("getTimeZone") ?? string.Empty;
    }

    public async ValueTask<string> GetScreenPrintAsync()
    {
        return await InvokeSafeAsync<string>("getScreenPrint") ?? string.Empty;
    }

}