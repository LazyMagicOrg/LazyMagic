namespace LazyMagic.Client.Auth;
using LazyMagic.Client.Base;
using LazyMagic.Shared;  
using Amazon.Runtime;
using Microsoft.Extensions.Logging;

public partial class LzHttpClientSigV4 : LzHttpClient, ILzHttpClient   
{
    public LzHttpClientSigV4(
        ILoggerFactory loggerFactory,
        IAuthProvider authProvider, // Auth service. ex: AuthProviderCognito
        ILzHost lzHost, // Runtime environment. IsMAUI, IsWASM, URL etc.
        string? sessionId = null    
        ) : base(loggerFactory,authProvider, lzHost, sessionId)
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
        var token = await authProvider!.GetIdentityToken();
        requestMessage.Headers.Add("lz-config-identity", token);

        var iCreds = await authProvider.GetCredsAsync();
        var awsCreds = new ImmutableCredentials(iCreds!.AccessKey, iCreds.SecretKey, iCreds.Token);
        var awsRegion = authProvider.CognitoRegion;   

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
