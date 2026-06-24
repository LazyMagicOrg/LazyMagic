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

    public BffCredentialsHandler()
    {
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

        return base.SendAsync(request, cancellationToken);
    }

    /// <summary>
    /// Builds a standalone handler chain (cookie credentials + CSRF over the browser
    /// HTTP stack) for callers that construct their own <see cref="HttpClient"/>, e.g. the
    /// StoreApp <c>IAppApi</c> registration via <see cref="IAuthenticationHandler"/>.
    /// </summary>
    public HttpMessageHandler CreateHandler()
    {
        return new BffCredentialsHandler
        {
            InnerHandler = new HttpClientHandler()
        };
    }
}
