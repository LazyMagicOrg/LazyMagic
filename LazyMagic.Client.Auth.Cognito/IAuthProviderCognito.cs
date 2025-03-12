namespace LazyMagic.Client.Auth;

public interface IAuthProviderCognito : IAuthProvider
{
    //public CognitoUser CognitoUser { get; }
    //public CognitoAWSCredentials Credentials { get; }
    public string? CognitoUserPoolId { get; }
    public string? CognitoClientId { get; }
    public string? CognitoIdentityPoolId { get; }
    public string? CognitoRegion { get; }
    
    public int SecurityLevel { get; }   

}
 