namespace LazyMagic.OIDC.WASM.Bff;

/// <summary>
/// BFF replacement for the bearer-token handler. In BFF mode the SPA never holds a token;
/// instead every outgoing request must carry the <c>HttpOnly</c> session cookie and a CSRF
/// marker. This handler:
///   - sets <c>credentials: include</c> via <see cref="WebAssemblyHttpRequestMessageExtensions.SetBrowserRequestCredentials"/>
///     so the same-origin <c>__bff</c> cookie is sent automatically, and
///   - adds the non-safelisted header <c>X-CSRF: 1</c> on every request. Its presence forces a
///     cross-origin preflight that our origin rejects, which is the CSRF defense paired with
///     <c>SameSite=Lax</c> (MultiTenantAuth.md §8.12).
///
/// It also implements <see cref="IAuthenticationHandler"/> so it slots into the existing
/// LazyMagic API-client wiring (<c>IAuthenticationHandler.CreateHandler()</c>) unchanged.
/// </summary>
public sealed class BffCredentialsHandler : DelegatingHandler, IAuthenticationHandler
{
    private const string CsrfHeaderName = "X-CSRF";
    private const string CsrfHeaderValue = "1";
    private const string PoolMarkerHeaderName = "lz-bff-pool";

    private readonly string? _poolMarker;

    /// <param name="poolMarker">
    /// Multi-pool BFF instance marker (e.g. <c>cbff</c> for consumerauth). When set, every request
    /// carries <c>lz-bff-pool: {marker}</c> so the apphost cookie→Bearer bridge selects THIS pool's
    /// cookie/Bearer. Null (the default) ⇒ no marker ⇒ the tenantauth instance, keeping StoreApp/
    /// AdminApp wire-identical.
    /// </param>
    public BffCredentialsHandler(string? poolMarker = null)
    {
        _poolMarker = string.IsNullOrWhiteSpace(poolMarker) ? null : poolMarker;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // Include the HttpOnly BFF session cookie on every (same-origin) call.
        request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);

        // CSRF marker — required by the BFF for state-changing calls; harmless on reads.
        if (!request.Headers.Contains(CsrfHeaderName))
            request.Headers.Add(CsrfHeaderName, CsrfHeaderValue);

        // Multi-pool: tell the apphost cookie→Bearer bridge which BFF instance's cookie to use.
        // Omitted for the default tenantauth instance (StoreApp/AdminApp stay wire-identical).
        if (_poolMarker is not null && !request.Headers.Contains(PoolMarkerHeaderName))
            request.Headers.Add(PoolMarkerHeaderName, _poolMarker);

        return base.SendAsync(request, cancellationToken);
    }

    /// <summary>
    /// Builds a standalone handler chain (cookie credentials + CSRF over the browser
    /// HTTP stack) for callers that construct their own <see cref="HttpClient"/>, e.g. the
    /// StoreApp <c>IAppApi</c> registration via <see cref="IAuthenticationHandler"/>.
    /// </summary>
    public HttpMessageHandler CreateHandler()
    {
        return new BffCredentialsHandler(_poolMarker)
        {
            InnerHandler = new HttpClientHandler()
        };
    }
}
