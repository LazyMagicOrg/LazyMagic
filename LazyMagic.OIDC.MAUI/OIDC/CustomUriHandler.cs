namespace LazyMagic.OIDC.MAUI;

/// <summary>
/// Handles custom URI schemes for OAuth callbacks
/// </summary>
public class CustomUriHandler : IUriHandler
{
    private readonly IOIDCService _oidcService;
    private readonly ILogger<CustomUriHandler> _logger;

    public CustomUriHandler(IOIDCService oidcService, ILogger<CustomUriHandler> logger)
    {
        _oidcService = oidcService;
        _logger = logger;
    }

    public async Task<bool> HandleUriAsync(string uri)
    {
        try
        {
            _logger.LogInformation("Handling custom URI: {Uri}", uri);

            // Check if this is our OAuth callback
            if (uri.StartsWith("awsloginmaui://auth-callback"))
            {
                if (_oidcService is MauiOIDCService mauiService)
                {
                    return await mauiService.HandleAuthCallbackAsync(uri);
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling custom URI: {Uri}", uri);
            return false;
        }
    }
}