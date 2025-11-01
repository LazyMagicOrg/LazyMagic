namespace LazyMagic.Auth.WASM.Services;

/// <summary>
/// Handles OAuth2 authorization code exchange for tokens
/// </summary>
public class TokenExchangeService
{
    private readonly HttpClient _httpClient;
    private readonly NavigationManager _navigation;
    private readonly ILogger<TokenExchangeService>? _logger;

    public TokenExchangeService(
        HttpClient httpClient,
        NavigationManager navigation,
        ILogger<TokenExchangeService>? logger = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        _logger = logger;
    }

    /// <summary>
    /// Exchange authorization code for tokens
    /// </summary>
    public async Task<TokenResponse> ExchangeCodeAsync(string code, JObject authConfig)
    {
        try
        {
            _logger?.LogInformation("Exchanging authorization code for tokens");

            var hostedUIDomain = authConfig["HostedUIDomain"]?.ToString();
            var clientId = authConfig["ClientId"]?.ToString() ?? authConfig["clientId"]?.ToString();
            var authority = authConfig["authority"]?.ToString();

            if (string.IsNullOrEmpty(clientId))
                throw new InvalidOperationException("ClientId not found in auth config");

            // Build token endpoint
            var tokenEndpoint = !string.IsNullOrEmpty(hostedUIDomain)
                ? $"{hostedUIDomain.TrimEnd('/')}/oauth2/token"
                : $"{authority}/oauth2/token";

            // Build callback URL
            var callbackUrl = new Uri(new Uri(_navigation.BaseUri), "callback").ToString();

            // Build request
            var requestData = new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["client_id"] = clientId,
                ["code"] = code,
                ["redirect_uri"] = callbackUrl
            };

            var request = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint)
            {
                Content = new FormUrlEncodedContent(requestData)
            };

            _logger?.LogDebug("Token endpoint: {TokenEndpoint}", tokenEndpoint);

            // Send request
            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger?.LogError("Token exchange failed: {StatusCode} {Error}",
                    response.StatusCode, errorContent);
                throw new InvalidOperationException($"Token exchange failed: {response.StatusCode}");
            }

            // Parse response
            var responseJson = await response.Content.ReadAsStringAsync();
            var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(responseJson);

            if (tokenResponse == null)
                throw new InvalidOperationException("Failed to parse token response");

            _logger?.LogInformation("Token exchange successful");
            return tokenResponse;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error exchanging authorization code");
            throw;
        }
    }

    /// <summary>
    /// Store tokens in localStorage in Microsoft OIDC format
    /// </summary>
    public async Task StoreTokensAsync(TokenResponse tokens, JObject authConfig, ILzJsUtilities jsUtilities)
    {
        try
        {
            var authority = authConfig["authority"]?.ToString();
            var clientId = authConfig["ClientId"]?.ToString() ?? authConfig["clientId"]?.ToString();

            if (string.IsNullOrEmpty(authority) || string.IsNullOrEmpty(clientId))
                throw new InvalidOperationException("Authority or ClientId missing from auth config");

            // Calculate expiration
            var expiresAt = DateTimeOffset.UtcNow.AddSeconds(tokens.ExpiresIn).ToUnixTimeSeconds();

            // Create token data in Microsoft OIDC format
            var tokenData = new
            {
                access_token = tokens.AccessToken,
                id_token = tokens.IdToken,
                refresh_token = tokens.RefreshToken,
                token_type = tokens.TokenType ?? "Bearer",
                scope = tokens.Scope ?? "openid profile email",
                expires_at = expiresAt
            };

            var tokenJson = JsonSerializer.Serialize(tokenData);
            var key = $"oidc.user:{authority}:{clientId}";

            await jsUtilities.SetItem(key, tokenJson);

            _logger?.LogInformation("Tokens stored in localStorage with key: {Key}", key);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error storing tokens");
            throw;
        }
    }
}

/// <summary>
/// Token response from OAuth2 token endpoint
/// </summary>
public class TokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    [JsonPropertyName("id_token")]
    public string IdToken { get; set; } = string.Empty;

    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; set; }

    [JsonPropertyName("token_type")]
    public string? TokenType { get; set; }

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonPropertyName("scope")]
    public string? Scope { get; set; }
}
