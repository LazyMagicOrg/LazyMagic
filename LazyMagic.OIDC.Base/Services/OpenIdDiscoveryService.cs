namespace LazyMagic.OIDC.Base;

/// <summary>
/// Service for fetching and caching OpenID Connect discovery documents.
/// Retrieves the .well-known/openid-configuration from OIDC providers.
/// Note: This service creates its own HttpClient to avoid DI scoping issues
/// (singleton service cannot depend on scoped HttpClient in Blazor WASM).
/// </summary>
public class OpenIdDiscoveryService : IOpenIdDiscoveryService
{
    private readonly ILogger<OpenIdDiscoveryService> _logger;
    private readonly SemaphoreSlim _cacheLock = new(1, 1);
    private readonly Dictionary<string, OpenIdDiscoveryDocument> _cache = new();
    private readonly TimeSpan _cacheExpiration = TimeSpan.FromHours(1);
    private readonly Dictionary<string, DateTime> _cacheTimestamps = new();
    private HttpClient? _httpClient;

    public OpenIdDiscoveryService(ILogger<OpenIdDiscoveryService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Gets or creates the HttpClient lazily to avoid issues during DI resolution
    /// </summary>
    private HttpClient HttpClient => _httpClient ??= new HttpClient();

    /// <summary>
    /// Fetches the OpenID discovery document from the specified metadata URL.
    /// Results are cached for 1 hour to minimize network calls.
    /// </summary>
    /// <param name="metadataUrl">The .well-known/openid-configuration URL</param>
    /// <returns>The parsed discovery document or null if fetch fails</returns>
    public async Task<OpenIdDiscoveryDocument?> GetDiscoveryDocumentAsync(string metadataUrl)
    {
        if (string.IsNullOrEmpty(metadataUrl))
        {
            _logger.LogWarning("[GetDiscoveryDocumentAsync] MetadataUrl is null or empty");
            return null;
        }

        await _cacheLock.WaitAsync();
        try
        {
            // Check cache first
            if (_cache.TryGetValue(metadataUrl, out var cachedDoc) &&
                _cacheTimestamps.TryGetValue(metadataUrl, out var timestamp) &&
                DateTime.UtcNow - timestamp < _cacheExpiration)
            {
                _logger.LogDebug("[GetDiscoveryDocumentAsync] Returning cached discovery document for {MetadataUrl}", metadataUrl);
                return cachedDoc;
            }

            _logger.LogInformation("[GetDiscoveryDocumentAsync] Fetching discovery document from {MetadataUrl}", metadataUrl);

            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                var response = await HttpClient.GetAsync(metadataUrl, cts.Token);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("[GetDiscoveryDocumentAsync] Failed to fetch discovery document. Status: {StatusCode}", response.StatusCode);
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync();
                var document = JsonConvert.DeserializeObject<OpenIdDiscoveryDocument>(json);

                if (document != null)
                {
                    _cache[metadataUrl] = document;
                    _cacheTimestamps[metadataUrl] = DateTime.UtcNow;
                    _logger.LogInformation("[GetDiscoveryDocumentAsync] Successfully cached discovery document. EndSessionEndpoint: {EndSessionEndpoint}", 
                        document.EndSessionEndpoint ?? "not provided");
                }

                return document;
            }
            catch (TaskCanceledException)
            {
                _logger.LogWarning("[GetDiscoveryDocumentAsync] Timeout fetching discovery document from {MetadataUrl}", metadataUrl);
                return null;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "[GetDiscoveryDocumentAsync] HTTP error fetching discovery document from {MetadataUrl}", metadataUrl);
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GetDiscoveryDocumentAsync] Unexpected error fetching discovery document");
            return null;
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    /// <summary>
    /// Gets the end_session_endpoint from the discovery document.
    /// </summary>
    /// <param name="metadataUrl">The .well-known/openid-configuration URL</param>
    /// <returns>The end_session_endpoint URL or null if not available</returns>
    public async Task<string?> GetEndSessionEndpointAsync(string metadataUrl)
    {
        var document = await GetDiscoveryDocumentAsync(metadataUrl);
        return document?.EndSessionEndpoint;
    }

    /// <summary>
    /// Clears the cached discovery documents.
    /// </summary>
    public void ClearCache()
    {
        _cacheLock.Wait();
        try
        {
            _cache.Clear();
            _cacheTimestamps.Clear();
            _logger.LogInformation("[ClearCache] Discovery document cache cleared");
        }
        finally
        {
            _cacheLock.Release();
        }
    }
}

/// <summary>
/// Interface for the OpenID discovery service
/// </summary>
public interface IOpenIdDiscoveryService
{
    /// <summary>
    /// Fetches the OpenID discovery document from the specified metadata URL.
    /// </summary>
    Task<OpenIdDiscoveryDocument?> GetDiscoveryDocumentAsync(string metadataUrl);

    /// <summary>
    /// Gets the end_session_endpoint from the discovery document.
    /// </summary>
    Task<string?> GetEndSessionEndpointAsync(string metadataUrl);

    /// <summary>
    /// Clears the cached discovery documents.
    /// </summary>
    void ClearCache();
}

/// <summary>
/// Represents the OpenID Connect discovery document (.well-known/openid-configuration)
/// </summary>
public class OpenIdDiscoveryDocument
{
    [JsonProperty("issuer")]
    public string? Issuer { get; set; }

    [JsonProperty("authorization_endpoint")]
    public string? AuthorizationEndpoint { get; set; }

    [JsonProperty("token_endpoint")]
    public string? TokenEndpoint { get; set; }

    [JsonProperty("userinfo_endpoint")]
    public string? UserinfoEndpoint { get; set; }

    [JsonProperty("end_session_endpoint")]
    public string? EndSessionEndpoint { get; set; }

    [JsonProperty("jwks_uri")]
    public string? JwksUri { get; set; }

    [JsonProperty("revocation_endpoint")]
    public string? RevocationEndpoint { get; set; }

    [JsonProperty("scopes_supported")]
    public List<string>? ScopesSupported { get; set; }

    [JsonProperty("response_types_supported")]
    public List<string>? ResponseTypesSupported { get; set; }

    [JsonProperty("grant_types_supported")]
    public List<string>? GrantTypesSupported { get; set; }

    [JsonProperty("subject_types_supported")]
    public List<string>? SubjectTypesSupported { get; set; }

    [JsonProperty("id_token_signing_alg_values_supported")]
    public List<string>? IdTokenSigningAlgValuesSupported { get; set; }

    [JsonProperty("token_endpoint_auth_methods_supported")]
    public List<string>? TokenEndpointAuthMethodsSupported { get; set; }

    [JsonProperty("claims_supported")]
    public List<string>? ClaimsSupported { get; set; }

    [JsonProperty("code_challenge_methods_supported")]
    public List<string>? CodeChallengeMethodsSupported { get; set; }
}
