using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;

namespace LazyMagic.OIDC.Bff;

/// <summary>
/// Data-Protection-backed implementation of <see cref="IBffCookieCodec"/>.
/// Purpose string is versioned so a future payload change can rotate cleanly.
/// </summary>
public sealed class BffCookieCodec : IBffCookieCodec
{
    /// <summary>Data Protection purpose. Versioned per §8.11.</summary>
    public const string Purpose = "LazyMagic.OIDC.Bff.Session.v1";

    /// <summary>
    /// Soft ceiling for the encoded cookie value. Browsers silently drop a cookie whose total
    /// size exceeds ~4KB, so warn well before that (leaving headroom for cookie-name/attributes).
    /// </summary>
    private const int CookieSizeWarnBytes = 3900;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly IDataProtector _protector;
    private readonly ILogger<BffCookieCodec> _logger;

    public BffCookieCodec(IDataProtectionProvider provider, ILogger<BffCookieCodec> logger)
    {
        _protector = provider.CreateProtector(Purpose);
        _logger = logger;
    }

    public string Protect(BffCookiePayload payload)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions);
        var protectedBytes = _protector.Protect(json);
        var encoded = WebEncoders.Base64UrlEncode(protectedBytes);

        // Guard: an over-limit cookie is SILENTLY dropped by the browser, making the very next
        // request appear anonymous (intermittent, group-membership-dependent auth failure).
        if (encoded.Length > CookieSizeWarnBytes)
        {
            _logger.LogWarning(
                "BFF session cookie is {Length} bytes, exceeding the {Limit}-byte soft limit; browsers may drop cookies over ~4KB. " +
                "Reduce the claims stored in the cookie (e.g. cap group/role count).",
                encoded.Length, CookieSizeWarnBytes);
        }

        return encoded;
    }

    public BffCookiePayload? Unprotect(string cookieValue)
    {
        if (string.IsNullOrWhiteSpace(cookieValue))
            return null;
        try
        {
            var protectedBytes = WebEncoders.Base64UrlDecode(cookieValue);
            var json = _protector.Unprotect(protectedBytes);
            return JsonSerializer.Deserialize<BffCookiePayload>(json, JsonOptions);
        }
        catch (Exception ex)
        {
            // Tampered, wrong key ring, malformed base64url, or schema mismatch.
            // Treat as unauthenticated rather than throwing (§ "On any failure, pass through unauthenticated").
            _logger.LogDebug(ex, "BFF cookie unprotect failed; treating as unauthenticated.");
            return null;
        }
    }

    /// <summary>Convenience for callers that already have UTF-8 strings.</summary>
    internal static string Utf8(byte[] bytes) => Encoding.UTF8.GetString(bytes);
}
