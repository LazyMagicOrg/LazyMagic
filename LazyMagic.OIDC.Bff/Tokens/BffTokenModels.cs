using System.Security.Claims;

namespace LazyMagic.OIDC.Bff;

/// <summary>PKCE pair generated at /bff/login.</summary>
public sealed class PkcePair
{
    public string CodeVerifier { get; init; } = string.Empty;
    public string CodeChallenge { get; init; } = string.Empty;
    /// <summary>Always "S256" for this BFF.</summary>
    public string CodeChallengeMethod { get; init; } = "S256";
}

/// <summary>Result of building an authorize redirect (URL + the PKCE/state/nonce to stash).</summary>
public sealed class AuthorizeRequest
{
    public string AuthorizeUrl { get; init; } = string.Empty;
    public string CodeVerifier { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
    public string Nonce { get; init; } = string.Empty;
}

/// <summary>Token-endpoint response (subset we use).</summary>
public sealed class BffTokenResult
{
    public string AccessToken { get; init; } = string.Empty;
    public string? IdToken { get; init; }
    public string? RefreshToken { get; init; }
    public string TokenType { get; init; } = "Bearer";
    public int ExpiresIn { get; init; }

    /// <summary>Absolute access-token expiry, epoch seconds (now + expires_in).</summary>
    public long ExpiresAtEpoch { get; init; }
}

/// <summary>Validated id_token outcome.</summary>
public sealed class ValidatedIdToken
{
    public ClaimsPrincipal Principal { get; init; } = new(new ClaimsIdentity());
    public string? Sub { get; init; }
    public string? Nonce { get; init; }
}
