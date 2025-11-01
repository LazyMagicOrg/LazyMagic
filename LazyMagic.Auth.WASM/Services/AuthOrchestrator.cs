namespace LazyMagic.Auth.WASM.Services;

/// <summary>
/// Orchestrates the authentication flow for the auth app
/// Handles auth config selection, token validation, and login initiation
/// </summary>
public class AuthOrchestrator
{
    private readonly IOidcConfig _oidcConfig;
    private readonly ILzClientConfig _clientConfig;
    private readonly NavigationManager _navigation;
    private readonly ILzJsUtilities _jsUtilities;
    private readonly ILogger<AuthOrchestrator>? _logger;

    public AuthOrchestrator(
        IOidcConfig oidcConfig,
        ILzClientConfig clientConfig,
        NavigationManager navigation,
        ILzJsUtilities jsUtilities,
        ILogger<AuthOrchestrator>? logger = null)
    {
        _oidcConfig = oidcConfig ?? throw new ArgumentNullException(nameof(oidcConfig));
        _clientConfig = clientConfig ?? throw new ArgumentNullException(nameof(clientConfig));
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        _jsUtilities = jsUtilities ?? throw new ArgumentNullException(nameof(jsUtilities));
        _logger = logger;
    }

    /// <summary>
    /// Load authentication configurations from /config endpoint
    /// </summary>
    public async Task LoadAuthConfigsAsync()
    {
        try
        {
            _logger?.LogInformation("Loading auth configs from /config");
            await _clientConfig.InitializeAsync(_navigation.BaseUri);
            _logger?.LogInformation("Auth configs loaded: {Count} configurations available",
                _oidcConfig.AuthConfigs?.Count ?? 0);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error loading auth configs");
            throw;
        }
    }

    /// <summary>
    /// Get all available authentication configurations for user selection
    /// </summary>
    public Dictionary<string, JObject> GetAvailableAuthConfigs()
    {
        return _oidcConfig.AuthConfigs ?? new Dictionary<string, JObject>();
    }

    /// <summary>
    /// Check if there are any valid tokens in localStorage for any auth config
    /// </summary>
    public async Task<(bool hasValidTokens, string? authConfigName)> HasAnyValidTokensAsync()
    {
        try
        {
            var authConfigs = GetAvailableAuthConfigs();
            foreach (var authConfig in authConfigs)
            {
                var authConfigName = authConfig.Key;
                var config = authConfig.Value;

                var authority = config["authority"]?.ToString();
                var clientId = config["ClientId"]?.ToString() ?? config["clientId"]?.ToString();

                if (string.IsNullOrEmpty(authority) || string.IsNullOrEmpty(clientId))
                    continue;

                // Check for OIDC token in localStorage (Microsoft OIDC format)
                var key = $"oidc.user:{authority}:{clientId}";
                var tokenJson = await _jsUtilities.GetItem(key);

                if (!string.IsNullOrEmpty(tokenJson))
                {
                    try
                    {
                        var tokenData = JsonSerializer.Deserialize<JsonElement>(tokenJson);
                        if (tokenData.TryGetProperty("expires_at", out var expiresAt))
                        {
                            var expirationTime = expiresAt.GetInt64();
                            if (expirationTime > DateTimeOffset.UtcNow.ToUnixTimeSeconds())
                            {
                                _logger?.LogInformation("Found valid tokens for auth config: {AuthConfigName}", authConfigName);
                                return (true, authConfigName);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Error parsing token for {AuthConfigName}", authConfigName);
                    }
                }
            }

            _logger?.LogInformation("No valid tokens found in any auth config");
            return (false, null);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error checking for valid tokens");
            return (false, null);
        }
    }

    /// <summary>
    /// Select the auth configuration to use
    /// </summary>
    public async Task SelectAuthConfigAsync(string authConfigName)
    {
        _logger?.LogInformation("Selecting auth config: {AuthConfigName}", authConfigName);
        _oidcConfig.SelectedAuthConfig = authConfigName;
        await Task.CompletedTask;
    }

    /// <summary>
    /// Initiate login flow by navigating to Cognito Hosted UI
    /// </summary>
    public async Task InitiateLoginAsync(string authConfigName, string? subtenant, string? returnUrl)
    {
        try
        {
            _logger?.LogInformation("Initiating login for auth config: {AuthConfigName}", authConfigName);

            // Select the auth config
            await SelectAuthConfigAsync(authConfigName);

            // Get the auth config
            if (!_oidcConfig.AuthConfigs.TryGetValue(authConfigName, out var authConfig))
            {
                _logger?.LogError("Auth config not found: {AuthConfigName}", authConfigName);
                throw new InvalidOperationException($"Auth config '{authConfigName}' not found");
            }

            // Build state parameter with auth config info
            var rootDomain = await GetRootDomainAsync();
            var stateData = new
            {
                authConfigName = authConfigName,
                subtenant = subtenant,
                returnUrl = returnUrl ?? "/",
                targetDomain = rootDomain,
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };

            var stateJson = JsonSerializer.Serialize(stateData);
            var stateEncoded = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(stateJson));

            // Build authorize URL
            var authorizeUrl = BuildAuthorizeUrl(authConfig, stateEncoded);

            _logger?.LogInformation("Navigating to authorize URL");
            _navigation.NavigateTo(authorizeUrl, forceLoad: true);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error initiating login");
            throw;
        }
    }

    private string BuildAuthorizeUrl(JObject authConfig, string state)
    {
        var hostedUIDomain = authConfig["HostedUIDomain"]?.ToString();
        var clientId = authConfig["ClientId"]?.ToString() ?? authConfig["clientId"]?.ToString();
        var authority = authConfig["authority"]?.ToString();

        if (string.IsNullOrEmpty(clientId))
            throw new InvalidOperationException("ClientId not found in auth config");

        // Build callback URL (this app's /callback endpoint)
        var callbackUrl = new Uri(new Uri(_navigation.BaseUri), "callback").ToString();

        // Cognito authorize endpoint
        var authorizeEndpoint = !string.IsNullOrEmpty(hostedUIDomain)
            ? $"{hostedUIDomain.TrimEnd('/')}/oauth2/authorize"
            : $"{authority}/oauth2/authorize";

        // Build query parameters
        var queryParams = new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["redirect_uri"] = callbackUrl,
            ["response_type"] = "code",
            ["scope"] = "openid profile email",
            ["state"] = state
        };

        var queryString = string.Join("&", queryParams.Select(kvp =>
            $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));

        return $"{authorizeEndpoint}?{queryString}";
    }

    private async Task<string> GetRootDomainAsync()
    {
        try
        {
            var hostname = await _jsUtilities.GetHostnameAsync();
            var parts = hostname?.Split('.') ?? Array.Empty<string>();

            // Return last 2 parts for root domain
            var rootDomain = parts.Length > 2
                ? string.Join('.', parts.TakeLast(2))
                : hostname ?? "localhost";

            _logger?.LogDebug("Root domain: {RootDomain} from hostname: {Hostname}", rootDomain, hostname);
            return rootDomain;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error getting root domain");
            return "localhost";
        }
    }
}
