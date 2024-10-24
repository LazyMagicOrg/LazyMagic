namespace LazyMagic.Client.Auth;
using LazyMagic.Client.Base;
using LazyMagic.Shared;  
using Amazon.Runtime;
using AwsSignatureVersion4;
using Microsoft.IdentityModel.Tokens;

public partial class LzHttpClientSigV4 : LzHttpClient, ILzHttpClient   
{
    public LzHttpClientSigV4(
        string awsRegion, // AWS region for the API 
        int securityLevel, // 0 = no security, 1 = JWT, 2 = AWS Signature V4    
        string tenantKey, // necessary for local debugging, CloudFront replaces this header value in the tenancy cache function
        IAuthProvider authProvider, // Auth service. ex: AuthProviderCognito
        ILzHost lzHost // Runtime environment. IsMAUI, IsWASM, URL etc.
        ) : base(securityLevel, tenantKey, authProvider, lzHost)
    {
        this.awsRegion = awsRegion;    
    }
    private string awsRegion;
    public override async Task<HttpResponseMessage> SendV4SigAsync(
        HttpClient httpclient,
        HttpRequestMessage requestMessage,
        HttpCompletionOption httpCompletionOption,
        CancellationToken cancellationToken,
        string callerMemberName) 
    {

        // Note: For this ApiGateway we need to add a copy of the JWT header 
        // to make it available to the Lambda. This type of ApiGateway will not
        // pass the authorization header through to the Lambda.
        var token = await authProvider!.GetJWTAsync();
        requestMessage.Headers.Add("LzIdentity", token);

        var iCreds = await authProvider.GetCredsAsync();
        var awsCreds = new ImmutableCredentials(iCreds!.AccessKey, iCreds.SecretKey, iCreds.Token);
        

        // Note. Using named parameters to satisfy version >= 3.x.x  signature of 
        // AwsSignatureVersion4 SendAsync method.
        var response = await httpclient.SendAsync(
        request: requestMessage,
        completionOption: httpCompletionOption,
        cancellationToken: cancellationToken,
        regionName: awsRegion,
        serviceName: "execute-api",
        credentials: awsCreds);
        return response!;

    }

}
