namespace LazyMagic.OIDC.Bff;

/// <summary>
/// Encrypts/decrypts the short-lived (5-min) login transaction cookie carrying the
/// PKCE verifier, state, nonce and returnUrl between /bff/login and /bff/callback.
/// </summary>
public interface IBffTransactionCodec
{
    /// <summary>Protect with a 5-minute lifetime → base64url cookie value.</summary>
    string Protect(BffTransaction txn);

    /// <summary>Unprotect; returns null on tamper/expiry/format error.</summary>
    BffTransaction? Unprotect(string cookieValue);
}
