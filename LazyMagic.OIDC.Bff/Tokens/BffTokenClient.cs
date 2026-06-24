using System.IdentityModel.Tokens.Jwt;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace LazyMagic.OIDC.Bff;

/// <summary>
/// Provider-aware OIDC token client. Endpoints + signing keys come from the OIDC
/// discovery document via <see cref="ConfigurationManager{T}"/> (cached + auto-refreshed).
/// </summary>
public sealed class BffTokenClient : IBffTokenClient
{
    private readonly HttpClient _http;
    private readonly BffOptions _options;
    private readonly ILogger<BffTokenClient> _logger;
    private readonly ConfigurationManager<OpenIdConnectConfiguration> _configManager;

    public BffTokenClient(
        HttpClient http,
        IOptions<BffOptions> options,
        ILogger<BffTokenClient> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
        _configManager = new ConfigurationManager<OpenIdConnectConfiguration>(
            _options.ResolvedMetadataUrl,
            new OpenIdConnectConfigurationRetriever(),
            new HttpDocumentRetriever(_http) { RequireHttps = true });
    }

    private Task<OpenIdConnectConfiguration> GetConfigAsync(CancellationToken ct) =>
        _configManager.GetConfigurationAsync(ct);

    // ---- PKCE ----

    private static PkcePair CreatePkce()
    {
        var verifierBytes = RandomNumberGenerator.GetBytes(32);
        var verifier = Base64Url(verifierBytes);
        using var sha = SHA256.Create();
        var challengeBytes = sha.ComputeHash(Encoding.ASCII.GetBytes(verifier));
        var challenge = Base64Url(challengeBytes);
        return new PkcePair { CodeVerifier = verifier, CodeChallenge = challenge, CodeChallengeMethod = "S256" };
    }

    private static string Base64Url(byte[] bytes) => WebEncoders.Base64UrlEncode(bytes);

    private static string RandomToken(int bytes = 32) => Base64Url(RandomNumberGenerator.GetBytes(bytes));

    // ---- Authorize ----

    public async Task<AuthorizeRequest> BuildAuthorizeUrlAsync(string redirectUri, CancellationToken ct = default)
    {
        var config = await GetConfigAsync(ct).ConfigureAwait(false);
        if (string.IsNullOrEmpty(config.AuthorizationEndpoint))
            throw new InvalidOperationException("OIDC discovery did not provide an authorization_endpoint.");

        var pkce = CreatePkce();
        var state = RandomToken();
        var nonce = RandomToken();

        var query = new Dictionary<string, string?>
        {
            ["response_type"] = "code",
            ["client_id"] = _options.ClientId,
            ["redirect_uri"] = redirectUri,
            ["scope"] = _options.ResolvedScopes,
            ["state"] = state,
            ["nonce"] = nonce,
            ["code_challenge"] = pkce.CodeChallenge,
            ["code_challenge_method"] = pkce.CodeChallengeMethod,
        };

        var url = QueryHelpers.AddQueryString(config.AuthorizationEndpoint, query);
        return new AuthorizeRequest
        {
            AuthorizeUrl = url,
            CodeVerifier = pkce.CodeVerifier,
            State = state,
            Nonce = nonce,
        };
    }

    // ---- Token exchange ----

    public async Task<BffTokenResult> ExchangeCodeAsync(string code, string codeVerifier, string redirectUri, CancellationToken ct = default)
    {
        var config = await GetConfigAsync(ct).ConfigureAwait(false);
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = redirectUri,
            ["client_id"] = _options.ClientId,
            ["code_verifier"] = codeVerifier,
        };
        return await PostTokenAsync(config.TokenEndpoint, form, ct).ConfigureAwait(false);
    }

    public async Task<BffTokenResult> RefreshAsync(string refreshToken, CancellationToken ct = default)
    {
        var config = await GetConfigAsync(ct).ConfigureAwait(false);
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
            ["client_id"] = _options.ClientId,
        };
        return await PostTokenAsync(config.TokenEndpoint, form, ct).ConfigureAwait(false);
    }

    private async Task<BffTokenResult> PostTokenAsync(string tokenEndpoint, Dictionary<string, string> form, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(tokenEndpoint))
            throw new InvalidOperationException("OIDC discovery did not provide a token_endpoint.");

        using var req = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint)
        {
            Content = new FormUrlEncodedContent(form),
        };
        // Confidential client: HTTP Basic auth with client_id:client_secret (works for both Cognito and Keycloak).
        if (!string.IsNullOrEmpty(_options.ClientSecret))
        {
            var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_options.ClientId}:{_options.ClientSecret}"));
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", basic);
        }

        using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
        var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
        {
            _logger.LogWarning("BFF token endpoint returned {Status}.", (int)resp.StatusCode);
            throw new InvalidOperationException($"Token endpoint error: {(int)resp.StatusCode}");
        }

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        var expiresIn = root.TryGetProperty("expires_in", out var ei) && ei.TryGetInt32(out var eiv) ? eiv : 0;
        return new BffTokenResult
        {
            AccessToken = GetStr(root, "access_token") ?? string.Empty,
            IdToken = GetStr(root, "id_token"),
            RefreshToken = GetStr(root, "refresh_token"),
            TokenType = GetStr(root, "token_type") ?? "Bearer",
            ExpiresIn = expiresIn,
            ExpiresAtEpoch = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + expiresIn,
        };
    }

    private static string? GetStr(JsonElement el, string name) =>
        el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

    // ---- Logout ----

    public async Task<string> BuildLogoutUrlAsync(string? idTokenHint, string postLogoutRedirectUri, CancellationToken ct = default)
    {
        var config = await GetConfigAsync(ct).ConfigureAwait(false);

        if (_options.Provider == BffProvider.Keycloak)
        {
            // Standard OIDC end_session_endpoint.
            var endSession = config.EndSessionEndpoint;
            if (string.IsNullOrEmpty(endSession))
                throw new InvalidOperationException("Keycloak discovery did not provide an end_session_endpoint.");
            var q = new Dictionary<string, string?>
            {
                ["post_logout_redirect_uri"] = postLogoutRedirectUri,
                ["client_id"] = _options.ClientId,
            };
            if (!string.IsNullOrEmpty(idTokenHint))
                q["id_token_hint"] = idTokenHint;
            return QueryHelpers.AddQueryString(endSession, q);
        }

        // Cognito: the hosted-UI /logout endpoint is NOT in the discovery doc. Derive it
        // from the authorization endpoint host (same hosted-UI domain): {scheme}://{host}/logout.
        var logoutBase = DeriveCognitoLogoutEndpoint(config);
        var cq = new Dictionary<string, string?>
        {
            ["client_id"] = _options.ClientId,
            ["logout_uri"] = postLogoutRedirectUri,
        };
        return QueryHelpers.AddQueryString(logoutBase, cq);
    }

    private static string DeriveCognitoLogoutEndpoint(OpenIdConnectConfiguration config)
    {
        // Prefer an explicit end_session_endpoint if the provider supplied one.
        if (!string.IsNullOrEmpty(config.EndSessionEndpoint))
            return config.EndSessionEndpoint;

        if (string.IsNullOrEmpty(config.AuthorizationEndpoint))
            throw new InvalidOperationException("Cannot derive Cognito /logout: no authorization_endpoint in discovery.");

        var authUri = new Uri(config.AuthorizationEndpoint);
        return new Uri(authUri, "/logout").ToString();
    }

    // ---- id_token validation ----

    public async Task<ValidatedIdToken> ValidateIdTokenAsync(string idToken, string expectedNonce, CancellationToken ct = default)
    {
        var config = await GetConfigAsync(ct).ConfigureAwait(false);

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = config.Issuer,
            ValidateAudience = true,
            ValidAudience = _options.ClientId,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(Math.Max(0, _options.AccessTokenSkewSeconds)),
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = config.SigningKeys,
            // Pin the accepted (asymmetric) signing algorithms so a poisoned/symmetric JWKS key
            // cannot enable algorithm-confusion/downgrade. Cognito and Keycloak both sign id_tokens
            // with RS256; ES* are included for forward-compatibility.
            RequireSignedTokens = true,
            ValidAlgorithms = new[] { "RS256", "RS384", "RS512", "ES256", "ES384", "ES512" },
            NameClaimType = "sub",
        };

        var handler = new JwtSecurityTokenHandler();
        var principal = handler.ValidateToken(idToken, validationParameters, out var validated);
        var jwt = (JwtSecurityToken)validated;

        // Nonce binding (replay protection) — the id_token nonce MUST equal what we issued.
        var tokenNonce = jwt.Claims.FirstOrDefault(c => c.Type == "nonce")?.Value;
        if (string.IsNullOrEmpty(tokenNonce) || !FixedTimeEquals(tokenNonce, expectedNonce))
            throw new SecurityTokenValidationException("id_token nonce mismatch.");

        var sub = jwt.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;
        return new ValidatedIdToken { Principal = principal, Sub = sub, Nonce = tokenNonce };
    }

    private static bool FixedTimeEquals(string a, string b)
    {
        var ba = Encoding.UTF8.GetBytes(a);
        var bb = Encoding.UTF8.GetBytes(b);
        if (ba.Length != bb.Length) return false;
        return CryptographicOperations.FixedTimeEquals(ba, bb);
    }
}
