namespace LazyMagic.Client.Auth;
using LazyMagic.Client.Base;
using LazyMagic.Shared;  
using Amazon.Runtime;
using Microsoft.Extensions.Logging;

public partial class LzHttpClientSigV4 : LzHttpClientCognito, ILzHttpClient   
{
    public LzHttpClientSigV4(
        ILoggerFactory loggerFactory,
        ILzHost lzHost // Runtime environment. IsMAUI, IsWASM, URL etc.
        ) : base(loggerFactory, lzHost)
    {
    }
    public override async Task<HttpResponseMessage> SendV4SigAsync(
        HttpClient httpclient,
        HttpRequestMessage requestMessage,
        HttpCompletionOption httpCompletionOption,
        CancellationToken cancellationToken,
        string callerMemberName) 
    {

        // Note: For this ApiGateway, our Authorization header does not
        // contain the caller identity. We add a copy of the JWT Identity
        // token so the API can use it to perform authorization checks.
        var token = await _authProvider!.GetIdentityToken();
        requestMessage.Headers.Add("lz-config-identity", token);

        var iCreds = await _authProvider.GetCredsAsync();
        var awsCreds = new ImmutableCredentials(iCreds!.AccessKey, iCreds.SecretKey, iCreds.Token);
        var awsRegion = ((IAuthProviderCognito)_authProvider).CognitoRegion;   

        // Note. Using named parameters to satisfy version >= 3.x.x  signature of 
        // AwsSignatureVersion4 SendAsync method.
        var response = await httpclient.SendAsync(
        request: requestMessage,
        completionOption: httpCompletionOption,
        cancellationToken: cancellationToken,
        regionName: awsRegion!,
        serviceName: "execute-api",
        credentials: awsCreds);
        return response!;

    }

}
