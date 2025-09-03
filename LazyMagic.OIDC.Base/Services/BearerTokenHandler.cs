using LazyMagic.OIDC.Base;

public interface IAuthenticationHandler
{
    HttpMessageHandler CreateHandler();
}
public class BearerTokenHandler : DelegatingHandler, IAuthenticationHandler
{
    private readonly IOIDCService _tokenService;

    public BearerTokenHandler(IOIDCService tokenService)
    {
        _tokenService = tokenService;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var token = await _tokenService.GetAccessTokenAsync();

        // Add bearer token to request
        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Authorization", token);

        // Continue with request
        return await base.SendAsync(request, cancellationToken);
    }

    public HttpMessageHandler CreateHandler()
    {
        return new BearerTokenHandler(_tokenService)
        {
            InnerHandler = new HttpClientHandler()
        };
    }
}
