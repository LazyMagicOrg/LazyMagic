using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;

namespace LazyMagic.OIDC.Bff;

/// <summary>
/// Time-limited Data-Protection implementation of <see cref="IBffTransactionCodec"/>.
/// The 5-minute lifetime is enforced cryptographically by
/// <see cref="ITimeLimitedDataProtector"/> so a stale/expired txn cannot be replayed.
/// </summary>
public sealed class BffTransactionCodec : IBffTransactionCodec
{
    /// <summary>Data Protection purpose for the login transaction.</summary>
    public const string Purpose = "LazyMagic.OIDC.Bff.LoginTxn.v1";

    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    private readonly ITimeLimitedDataProtector _protector;
    private readonly ILogger<BffTransactionCodec> _logger;

    public BffTransactionCodec(IDataProtectionProvider provider, ILogger<BffTransactionCodec> logger)
    {
        _protector = provider.CreateProtector(Purpose).ToTimeLimitedDataProtector();
        _logger = logger;
    }

    public string Protect(BffTransaction txn)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(txn);
        var protectedBytes = _protector.Protect(json, Lifetime);
        return WebEncoders.Base64UrlEncode(protectedBytes);
    }

    public BffTransaction? Unprotect(string cookieValue)
    {
        if (string.IsNullOrWhiteSpace(cookieValue))
            return null;
        try
        {
            var protectedBytes = WebEncoders.Base64UrlDecode(cookieValue);
            var json = _protector.Unprotect(protectedBytes); // throws if expired/tampered
            return JsonSerializer.Deserialize<BffTransaction>(json);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "BFF login transaction unprotect failed (expired/tampered).");
            return null;
        }
    }
}
