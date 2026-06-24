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

    /// <summary>Issued-at epoch seconds (lifetime is enforced on the encrypted blob via the protector).</summary>
    [JsonPropertyName("iat")]
    public long IssuedAt { get; set; }
}
