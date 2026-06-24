using System.Text.Json.Serialization;

namespace LazyMagic.OIDC.Bff;

/// <summary>
/// The ONLY thing in the browser (DP-encrypted, base64url). The refresh token is
/// NEVER here — that is the split (§8.4, §8.11). Keep &lt; 4KB.
/// </summary>
public sealed class BffCookiePayload
{
    /// <summary>Session id (links to the DynamoDB SESSION#sid row).</summary>
    [JsonPropertyName("sid")]
    public string Sid { get; set; } = string.Empty;

    /// <summary>Short-lived access token (JWT) attached as Bearer on the hot path.</summary>
    [JsonPropertyName("at")]
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>Access-token expiry, epoch seconds.</summary>
    [JsonPropertyName("exp")]
    public long Exp { get; set; }

    /// <summary>
    /// Minimal claims surfaced to /bff/user (sub, name, groups/roles, etc.). Each claim maps
    /// to a LIST of string values (single-valued claims are 1-element lists). This is
    /// round-trip-safe: a <c>Dictionary&lt;string, object&gt;</c> deserializes its values to
    /// <c>JsonElement</c>, which the AppHost's Newtonsoft MVC then serializes as
    /// <c>{"valueKind":N}</c> on /bff/user — losing the roles. <c>List&lt;string&gt;</c> values
    /// serialize correctly through both System.Text.Json (the cookie) and Newtonsoft (/bff/user).
    /// </summary>
    [JsonPropertyName("c")]
    public Dictionary<string, List<string>> Claims { get; set; } = new();
}
