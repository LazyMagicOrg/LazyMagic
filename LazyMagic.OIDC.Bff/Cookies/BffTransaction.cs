using System.Text.Json.Serialization;

namespace LazyMagic.OIDC.Bff;

/// <summary>
/// Short-lived (5-min) DP-encrypted transaction stashed between /bff/login and
/// /bff/callback (§8.10). Carries the PKCE verifier + CSRF/replay guards.
/// </summary>
public sealed class BffTransaction
{
    [JsonPropertyName("v")]
    public string CodeVerifier { get; set; } = string.Empty;

    [JsonPropertyName("s")]
    public string State { get; set; } = string.Empty;

    [JsonPropertyName("n")]
    public string Nonce { get; set; } = string.Empty;

    [JsonPropertyName("r")]
    public string ReturnUrl { get; set; } = "/";

    /// <summary>
    /// The public viewer host where /bff/login was initiated (e.g. <c>uptown.lazymagicdev.click</c>).
    /// The authorize/callback redirect_uri is pinned to the APEX host (the only one registered with the
    /// IdP); after the apex callback completes the server-side exchange, the post-login redirect is fanned
    /// back to this host. Empty when no parent CookieDomain is configured (single-host / localhost dev) —
    /// the callback then redirects relative to its own host, preserving the legacy behavior.
    /// </summary>
    [JsonPropertyName("oh")]
    public string OriginHost { get; set; } = string.Empty;

    /// <summary>Issued-at epoch seconds (lifetime is enforced on the encrypted blob via the protector).</summary>
    [JsonPropertyName("iat")]
    public long IssuedAt { get; set; }
}
