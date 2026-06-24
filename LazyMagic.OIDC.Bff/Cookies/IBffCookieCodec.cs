namespace LazyMagic.OIDC.Bff;

/// <summary>
/// Encrypts/decrypts the BFF session cookie via Data Protection. Tamper/expiry is
/// handled gracefully (Unprotect returns null) → caller treats as unauthenticated.
/// </summary>
public interface IBffCookieCodec
{
    /// <summary>Serialize + DP.Protect + base64url-encode into a cookie value.</summary>
    string Protect(BffCookiePayload payload);

    /// <summary>base64url-decode + DP.Unprotect + deserialize. Returns null on tamper/format error.</summary>
    BffCookiePayload? Unprotect(string cookieValue);
}
