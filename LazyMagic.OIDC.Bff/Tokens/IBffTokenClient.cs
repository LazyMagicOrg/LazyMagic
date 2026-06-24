namespace LazyMagic.OIDC.Bff;

/// <summary>
/// Provider-aware OAuth/OIDC client for the BFF. Discovers endpoints + JWKS from the
/// OIDC metadata document and validates id_tokens. (§8.10)
/// </summary>
public interface IBffTokenClient
{
    /// <summary>
    /// Build the authorization-endpoint redirect: authorization endpoint + PKCE S256
    /// challenge + state + nonce + redirect_uri. Returns the URL and the verifier/state/nonce
    /// to stash in the transaction cookie.
    /// </summary>
    Task<AuthorizeRequest> BuildAuthorizeUrlAsync(string redirectUri, CancellationToken ct = default);

    /// <summary>Exchange the authorization code at the token endpoint (client_id+secret, code, code_verifier).</summary>
    Task<BffTokenResult> ExchangeCodeAsync(string code, string codeVerifier, string redirectUri, CancellationToken ct = default);

    /// <summary>grant_type=refresh_token.</summary>
    Task<BffTokenResult> RefreshAsync(string refreshToken, CancellationToken ct = default);

    /// <summary>
    /// Build the provider logout URL.
    /// Cognito: <c>{domain}/logout?client_id&amp;logout_uri</c>.
    /// Keycloak: discovery <c>end_session_endpoint?id_token_hint&amp;post_logout_redirect_uri&amp;client_id</c>.
    /// </summary>
    Task<string> BuildLogoutUrlAsync(string? idTokenHint, string postLogoutRedirectUri, CancellationToken ct = default);

    /// <summary>Validate id_token (issuer, audience, nonce, lifetime, signature) against discovered JWKS.</summary>
    Task<ValidatedIdToken> ValidateIdTokenAsync(string idToken, string expectedNonce, CancellationToken ct = default);
}
