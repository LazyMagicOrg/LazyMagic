namespace LazyMagic.OIDC.Bff;

/// <summary>
/// Encrypts/decrypts the short-lived (BffCookieBuilder.TransactionLifetime) login transaction cookie carrying the
/// PKCE verifier, state, nonce and returnUrl between /bff/login and /bff/callback.
/// </summary>
public interface IBffTransactionCodec
{
    /// <summary>Protect with BffCookieBuilder.TransactionLifetime → base64url cookie value.</summary>
    string Protect(BffTransaction txn);

    /// <summary>Unprotect; returns null on tamper/expiry/format error.</summary>
    BffTransaction? Unprotect(string cookieValue);
}
