namespace LazyMagic.Client.Base.Services;

/// <summary>
/// HTTP message handler that automatically adds the subtenant header to all outgoing requests
/// Used in the HttpClient pipeline to inject subtenant context for API calls
/// </summary>
public class SubtenantMessageHandler : DelegatingHandler
{
    private readonly ISubtenantService _subtenantService;
    private readonly ILogger<SubtenantMessageHandler>? _logger;

    public SubtenantMessageHandler(ISubtenantService subtenantService, ILogger<SubtenantMessageHandler>? logger = null)
    {
        _subtenantService = subtenantService ?? throw new ArgumentNullException(nameof(subtenantService));
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // Get subtenant from service (checks cookie and localStorage)
        var subtenant = await _subtenantService.GetSubtenantAsync();

        if (!string.IsNullOrEmpty(subtenant))
        {
            // Add subtenant header for API backend
            request.Headers.Add("lz-subtenant", subtenant);
            _logger?.LogDebug("Added lz-subtenant header: {Subtenant} to request: {RequestUri}",
                subtenant, request.RequestUri);
        }
        else
        {
            _logger?.LogDebug("No subtenant found, skipping header for request: {RequestUri}",
                request.RequestUri);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
