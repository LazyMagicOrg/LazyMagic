using Amazon.Runtime;

namespace LazyMagic.Client.Auth;

/// <summary>
/// This ILzHttpClient implementation supports calling Local, CloudFront or ApiGateway endpoints.
/// It is not an HttClient, instead it services SendAsync() calls made from the *ClientSDK and 
/// dispatches these calls to an HTTPClient configured for the API. 
/// 
/// You do not have to use this class. You can use your own HttpClient implementation and just 
/// cast it to this ILzHttpClient interface. For instance, you could move all the configuration 
/// to the top level of your app. You can use delegates for adding the headers etc.
/// 
/// TODO: Use IHttpClientFactory to handle DNS changes. We may want to add in delegates for 
/// handling retries and other HttpClientFactory features.
/// 
/// </summary>
public class LzHttpClientCognito : NotifyBase, ILzHttpClient
{
    private readonly ILogger _logger;
    public LzHttpClientCognito(
        ILoggerFactory loggerFactory,
        ILzHost lzHost // Runtime environment. IsMAUI, IsWASM, URL etc.
        )
    {
        _logger = loggerFactory.CreateLogger<LzHttpClientCognito>();   
        _lzHost= lzHost;
        
    }
    protected ILzCurrentSessionAuthProviderCreds? _currentSessionProvider;
    protected IAuthProviderCreds? _authProvider;
    protected ILzHost _lzHost;
    protected HttpClient? httpClient;
    protected string? _sessionId; 
    public bool IsInitialized { get; private set; } = false;    
    public void Initialize(ILzCurrentSessionAuthProviderCreds currrentSessionProvider)
    {
        _currentSessionProvider = _currentSessionProvider = currrentSessionProvider;
        IsInitialized = true;
    }

    public async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage requestMessage,
        HttpCompletionOption httpCompletionOption,
        CancellationToken cancellationToken,
        [CallerMemberName] string? callerMemberName = null!)
    {
        if(!IsInitialized)
        {
            throw new InvalidOperationException("LzHttpClient is not initialized. Call Initialize() before using this method.");
        }

        // Grab the current session auth provider creds
        _authProvider = _currentSessionProvider!.CurrentSessionAuthProviderCreds;   
        _sessionId = _authProvider.SessionId;

        var securityLevel = 0;
        if(_authProvider != null)
        {
            securityLevel = _authProvider.SecurityLevel;
        }   

        var baseUrl = _lzHost.UseLocalhostApi ? _lzHost.LocalApiUrl : _lzHost.RemoteApiUrl;
        if(!baseUrl.EndsWith("/"))
            baseUrl += "/"; // baseUrl must end with a / or contcat with relative path may fail

        requestMessage.Headers.Add("SessionId", _sessionId);

        // Create HttpClient if it doesn't exist
        if (httpClient is null)
        {
            httpClient = _lzHost.IsLocal && _lzHost.IsMAUI
                ? new HttpClient(GetInsecureHandler())
                : new HttpClient();
            httpClient.BaseAddress = new Uri(baseUrl);
        }

        try
        {
            HttpResponseMessage? response = null;
            switch (securityLevel)
            {
                case 0: // No security 
                    try
                    {
                        response = await httpClient.SendAsync(
                            requestMessage,
                            httpCompletionOption,
                            cancellationToken);
                        return response;
                    }
                    catch (HttpRequestException e) 
                    {
                        // request failed due to an underlying issue such as network connectivity,
                        // DNS failure, server certificate validation or timeout
                        _logger.LogDebug($"HttpRequestException {e.Message}");  
                        return new HttpResponseMessage(System.Net.HttpStatusCode.BadRequest);
                    } 
                    catch (Exception e)
                    {
                        _logger.LogDebug($"Error: {callerMemberName} {e.Message}");
                        return new HttpResponseMessage(System.Net.HttpStatusCode.BadRequest);
                    }

                case 1: // Use JWT Token signing process
                    try
                    {
                        string? token = "";
                        try
                        {
                            token = await _authProvider!.GetIdentityToken();
                            requestMessage.Headers.Add("Authorization", token);

                            //// When running against localhost, we need to add the region and userpool id
                            //// because these are not being added by virtue of the API Gateway proxy transformation
                            //// that would happen if we were calling the API Gateway.
                            //if (_lzHost.UseLocalhostApi)
                            //{
                            //    if (!string.IsNullOrEmpty(_lzHost.CognitoRegion))
                            //        requestMessage.Headers.Add("lz-cognito-region", _lzHost.CognitoRegion);
                            //    if (!string.IsNullOrEmpty(_lzHost.CognitoUserPoolId))
                            //        requestMessage.Headers.Add("lz-cognito-userpool-id", _lzHost.CognitoUserPoolId);
                            //}

                        }
                        catch
                        {
                            // Ignore. We ignore this error and let the 
                            // api handle the missing token. This gives us a 
                            // way of testing an improperly configured API.
                            _logger.LogDebug("authProvider.GetJWTAsync() failed");
                            return new HttpResponseMessage(System.Net.HttpStatusCode.BadRequest);
                        }

                        response = await httpClient.SendAsync(
                            requestMessage,
                            httpCompletionOption,
                            cancellationToken);
                        return response;
                    }
                    catch (HttpRequestException e)
                    {
                        // request failed due to an underlying issue such as network connectivity,
                        // DNS failure, server certificate validation or timeout
                        _logger.LogDebug($"HttpRequestException {e.Message}");
                        return new HttpResponseMessage(System.Net.HttpStatusCode.BadRequest);
                    }
                    catch (Exception e)
                    {
                        _logger.LogDebug($"Error: {callerMemberName} {e.Message}");
                        return new HttpResponseMessage(System.Net.HttpStatusCode.BadRequest);
                    }
                case 2: // Use AWS Signature V4 signing process
                    try
                    {
                        return await SendV4SigAsync(httpClient, requestMessage, httpCompletionOption, cancellationToken, callerMemberName!);
                    }
                    catch (System.Exception e)
                    {
                        _logger.LogDebug($"Error: {e.Message}");
                    }
                    break;
                    throw new Exception($"Security Level {_authProvider.SecurityLevel} not supported.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug($"Error: {ex.Message}");
            return new HttpResponseMessage(System.Net.HttpStatusCode.BadRequest);
        }
        return new HttpResponseMessage(System.Net.HttpStatusCode.BadRequest);
    }
    // This method is virtual so that it can be overridden by the LzHttpClientSigV4 class. This allows us 
    // to avoid dragging in the SigV4 package (and associated crypto libs) if we don't need it.
    public virtual async Task<HttpResponseMessage> SendV4SigAsync(
        HttpClient httpclient,
        HttpRequestMessage requestMessage,
        HttpCompletionOption httpCompletionOption,
        CancellationToken cancellationToken,
        string callerMemberName)
    {
        await Task.Delay(0);
        throw new NotImplementedException("AWS Signature V4 signing requires the LzHttpClientSigV4 class. Use the LazyMagic.Client.Auth.Cognito.SigV4 package.");
    }
    public void Dispose()
    {
        if(httpClient != null)
            httpClient.Dispose();
    }

    //https://docs.microsoft.com/en-us/xamarin/cross-platform/deploy-test/connect-to-local-web-services
    //Attempting to invoke a local secure web service from an application running in the iOS simulator 
    //or Android emulator will result in a HttpRequestException being thrown, even when using the managed 
    //network stack on each platform.This is because the local HTTPS development certificate is self-signed, 
    //and self-signed certificates aren't trusted by iOS or Android.
    public static HttpClientHandler GetInsecureHandler()
    {
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
            {
                if (cert!.Issuer.Equals("CN=localhost"))
                    return true;
                return errors == System.Net.Security.SslPolicyErrors.None;
            }
        };
        return handler;
    }

}
