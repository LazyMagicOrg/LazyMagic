

using Amazon.CognitoIdentityProvider.Model;

/// <summary> 
/// AWS Authentication and Authorization Strategy
/// AWS Cognito User Pools are used for Authentication
/// AWS Cognito Identity Pools are used for Authorization
/// 
/// API AuthModule - AWS Cognito Implementation
/// This code make use of the AWS SDK for .NET https://github.com/aws/aws-sdk-net/
/// specifically the AWSSDK.CognitoIdentity, AWSSDK.CognitoIdentityProvider 
/// and AWSSDK.Extensions.CognitoAuthentication packages.
/// 
/// References
/// https://www.nuget.org/packages/AWSSDK.Extensions.CognitoAuthentication.
/// https://docs.aws.amazon.com/sdk-for-net/v3/developer-guide/cognito-apis-intro.html
/// https://aws.amazon.com/blogs/developer/cognitoauthentication-extension-library-developer-preview
/// https://github.com/aws/aws-sdk-net-extensions-cognito/tree/master/src/Amazon.Extensions.CognitoAuthentication
///
/// For more on CognitoIdentityProvider see: 
/// https://github.com/aws/aws-sdk-net/ 
/// https://docs.aws.amazon.com/cognito/latest/developerguide/cognito-reference.html
/// https://aws.amazon.com/blogs/mobile/
/// https://aws.amazon.com/blogs/mobile/sign-up-and-confirm-with-user-pools-using-csharp/
/// https://aws.amazon.com/blogs/mobile/tracking-and-remembering-devices-using-amazon-cognito-your-user-pools/
///
/// Here are a few simple blogs that shows the bare basics
/// http://www.mcraesoft.com/authenticating-to-aws-cognito-from-windows/
/// 
/// Remember Device docs
/// https://aws.amazon.com/blogs/mobile/tracking-and-remembering-devices-using-amazon-cognito-your-user-pools/
/// 
/// A note about .ConfigureAwait()
/// None of the methods in this class use the UI context so we use
/// .ConfigureAwait(false) on all async calls into Cognito libs.
/// 
/// </summary>
namespace LazyMagic.Client.Auth;

/// <summary>
/// Implements IAuthProvider using AWS Cognito as authentication provider
/// You need to call SetAuthenticator(AuthConfig) before using the class.
/// AuthConfig is a JObject that contains the following properties:
///     type // cloudfront, local, localandroid
///     awsRegion // required
///     userPoolName // not used in this class
///     userPoolId // required
///     UserPoolClientId // required
///     UserPoolSecurityLevel /required. 0=insecure, 1=JWT, 2=Signed
///     IdentityPoolId // optional, required only for signing 
/// </summary>
public class AuthProviderCognito : IAuthProviderCognito
{
    public AuthProviderCognito(
        ILoginFormat loginFormat,
        IPasswordFormat passwordFormat,
        IEmailFormat emailFormat,
        ICodeFormat codeFormat,
        IPhoneFormat phoneFormat
    //,ICognitoConfig cognitoConfig = null
    )
    {
        this.loginFormat = loginFormat;
        this.passwordFormat = passwordFormat;
        this.emailFormat = emailFormat;
        this.codeFormat = codeFormat;
        this.phoneFormat = phoneFormat;

        //if(cognitoConfig != null) 
        //    SetStack(cognitoConfig);
    }

    #region AWS specific Fields
    protected string? clientId;
    protected string? userPoolId;
    protected string? identityPoolId;
    protected RegionEndpoint? regionEndpoint;
    protected AmazonCognitoIdentityProviderClient? providerClient;
    protected CognitoUserPool? userPool;
    protected AuthFlowResponse? authFlowResponse; // cleared after interim use
    #endregion Fields

    #region AWS specific Properties
    public string? IpIdentity { get; set; } // Identity Pool Identity.
    public string? UpIdentity { get; set; } // User Pool Identity. ie: JWT "sub" claim
    public CognitoAWSCredentials? Credentials { get; private set; }

    // CognitoUser is part of Amazon.Extensions.CognitoAuthentication -- see the following resources
    // https://docs.aws.amazon.com/sdk-for-net/v3/developer-guide/cognito-authentication-extension.html -- very limited docs
    // https://github.com/aws/aws-sdk-net-extensions-cognito/ -- you really need to read this code to use the lib properly
    public CognitoUser? CognitoUser { get; private set; } // 
    #endregion

    #region Fields
    protected ILoginFormat? loginFormat;
    protected IPasswordFormat? passwordFormat;
    protected IEmailFormat? emailFormat;
    protected ICodeFormat? codeFormat;
    protected IPhoneFormat? phoneFormat;
    //protected IStacksConfig stacksConfig;

    private bool allowAdminCreateUserOnly;

    private string? login; // set by VerifyLogin
    private string? newLogin; // set by VerifyNewLogin
    private string? password; // set by VerifyPassword
    private string? newPassword; // set by VerifyNewPassword
    private string? email; // set by VerifyEmail
    private string? newEmail; // set by VerifyNewEmail
    private string? phone; // set by VerifyPhone
    private string? newPhone; // set by VerifyNewPhone
    private string? code; // set by VerifyCode 
    #endregion

    #region Properties
    public AuthProcessEnum CurrentAuthProcess { get; private set; }
    public bool AuthInitialized { get; set; }
    public List<AuthChallengeEnum> AuthChallengeList { get; } = new List<AuthChallengeEnum>();
    public AuthChallengeEnum CurrentChallenge
    {
        get
        {
            return AuthChallengeList.Count > 0
              ? AuthChallengeList[0]
              : AuthChallengeEnum.None;
        }
    }
    public async Task<string?> GetJWTAsync()
    {
        await Task.Delay(0);
        return CognitoUser?.SessionTokens?.IdToken;
    }
    public async Task<Creds?> GetCredsAsync()
    {
        if (!string.IsNullOrEmpty(identityPoolId))
            return new Creds();

            ImmutableCredentials? iCreds = null;
        try
        {
            iCreds = await Credentials!.GetCredentialsAsync();
            return new Creds()
            {
                AccessKey = iCreds.AccessKey,
                SecretKey = iCreds.SecretKey,
                Token = iCreds.Token
            };
        }
        catch 
        {
            return new Creds();
        }

    }
    public bool IsLoginFormatOk { get; private set; }
    public bool IsLoginVerified { get; private set; }
    public bool IsNewLoginFormatOk { get; private set; }
    public bool IsNewLoginVerified { get; private set; }
    public bool IsEmailFormatOk { get; private set; }
    public bool IsEmailVerified { get; private set; }
    public bool IsNewEmailFormatOk { get; private set; }
    public bool IsNewEmailVerified { get; private set; }
    public bool IsPasswordFormatOk { get; private set; }
    public bool IsPasswordVerified { get; private set; }
    public bool IsNewPasswordFormatOk { get; private set; }
    public bool IsNewPasswordVerified { get; private set; }
    public bool IsPhoneFormatOk { get; private set; }
    public bool IsPhoneVerified { get; private set; }
    public bool IsNewPhoneFormatOk { get; private set; }
    public bool IsNewPhoneVerified { get; private set; }
    public bool IsCodeFormatOk { get; private set; }
    public bool IsCodeVerified { get; private set; }
    public bool IsCleared { get { return !IsPasswordFormatOk && !IsNewPasswordFormatOk && !IsCodeFormatOk; } }
    public bool IsSignedIn { get; private set; }
    public bool HasChallenge { get { return AuthChallengeList.Count > 0; } }
    public bool CanSignOut => IsSignedIn && CurrentAuthProcess == AuthProcessEnum.None;
    public bool CanSignIn => !IsSignedIn && CurrentAuthProcess == AuthProcessEnum.None;
    public bool CanSignUp => !allowAdminCreateUserOnly && !IsSignedIn && CurrentAuthProcess == AuthProcessEnum.None;
    public bool CanResetPassword => !IsSignedIn && CurrentAuthProcess == AuthProcessEnum.None;
    public bool CanUpdateLogin => false; // not supported in AWS Cognito
    public bool CanUpdateEmail => IsSignedIn && CurrentAuthProcess == AuthProcessEnum.None;
    public bool CanUpdatePassword => IsSignedIn && CurrentAuthProcess == AuthProcessEnum.None;
    public bool CanUpdatePhone => false; // not currently implemented
    public bool CanCancel => CurrentAuthProcess != AuthProcessEnum.None;
    public bool CanNext => (CurrentChallenge == AuthChallengeEnum.Login && IsLoginFormatOk)
        || (CurrentChallenge == AuthChallengeEnum.Password && IsPasswordFormatOk)
        || (CurrentChallenge == AuthChallengeEnum.NewLogin && IsNewLoginFormatOk)
        || (CurrentChallenge == AuthChallengeEnum.NewPassword && IsNewPasswordFormatOk)
        || (CurrentChallenge == AuthChallengeEnum.Email && IsEmailFormatOk)
        || (CurrentChallenge == AuthChallengeEnum.NewEmail && IsNewEmailFormatOk)
        || (CurrentChallenge == AuthChallengeEnum.Phone && IsPhoneFormatOk)
        || (CurrentChallenge == AuthChallengeEnum.NewPhone && IsPhoneFormatOk) 
        || (CurrentChallenge == AuthChallengeEnum.Code && IsCodeFormatOk)
        ;
       
    public bool CanResendCode => CurrentChallenge == AuthChallengeEnum.Code;
    public bool IsChallengeLongWait
    {
        get
        {
            switch (CurrentChallenge)
            {
                case AuthChallengeEnum.Code:
                    return true;

                case AuthChallengeEnum.Password:
                    return CurrentAuthProcess == AuthProcessEnum.SigningIn;

                case AuthChallengeEnum.Email:
                    return true;

                case AuthChallengeEnum.Login:
                    return false;

                case AuthChallengeEnum.NewEmail:
                    return true;

                case AuthChallengeEnum.NewLogin:
                    return true;

                case AuthChallengeEnum.NewPassword:
                    return true;

                case AuthChallengeEnum.Phone:
                    return true;

                case AuthChallengeEnum.NewPhone:
                    return true;

                case AuthChallengeEnum.None:
                    return false;
                
            }
            return false;
        }
    } // will the current challenge do a server roundtrip?
    public string[]? FormatMessages { get; private set; }
    public string FormatMessage
    {
        get
        {
            return FormatMessages?.Length > 0 ? FormatMessages[0] : "";
        }
    }
    #endregion Properties

    /// <summary>
    /// SetAuthenticator is used to configure the Cognito User Pool and Identity Pool
    /// to enable authentication.
    /// The authConfig is a JObject that contains the following properties:
    /// awsRegion 
    /// userPoolName
    /// userPoolId 
    /// userPoolClientId
    /// userPoolSecurityLevel 
    /// identityPoolId
    /// </summary>
    /// <param name="authConfig"></param>
    /// <exception cref="Exception"></exception>
    public void SetAuthenticator(JObject authConfig)
    {
        if (authConfig == null)
            throw new Exception("Cognito Config is null");

        clientId = (string?)authConfig["userPoolClientId"] ?? throw new Exception("Cognito AuthConfig.userPoolClientId is null");

        var _region = (string?)authConfig["awsRegion"] ?? throw new Exception("Cognito AuthConfig.region is null");
        regionEndpoint = RegionEndpoint.GetBySystemName(_region);

        userPoolId = (string?)authConfig["userPoolId"] ?? throw new Exception("Cognito AuthConfig.userPoolId is null");

        identityPoolId = (string?)authConfig["identityPoolId"];

        //Console.WriteLine($"About to call AmazonCognitoIdentityProviderClient(.., {regionEndpoint.DisplayName})");
        try
        {
            providerClient = new AmazonCognitoIdentityProviderClient(new AnonymousAWSCredentials(), regionEndpoint);

        }
        catch (Exception ex)
        {
            Console.WriteLine($"InnerExcpetion {ex.InnerException?.Message}");
            Console.WriteLine($"InnerExcpetion2 {ex.InnerException?.InnerException?.Message}");
            Console.WriteLine($"InnerExcpetion3 {ex.InnerException?.InnerException?.InnerException?.Message}");
            throw ex;   
        }
        //Console.WriteLine($"SetAuthenticator. userPoolId:{userPoolId}, clientId:{clientId}");
        userPool = new CognitoUserPool(userPoolId, clientId, providerClient);

        AuthInitialized = true;
    }

    /// <summary>
    /// allowAdminCreateUserOnly can't be queried from AWS without 
    /// credentials.
    //  We allow the client to set it here so it's value  can be
    //  used in the auth state machine. Of course this value 
    //  and the one in the Cognito User Pool must match.
    /// </summary>
    /// <param name="isAllowed"></param>
    public void SetSignUpAllowed(bool isAllowed)
    {
        allowAdminCreateUserOnly = !isAllowed;
    }

    #region Challenge Flow Methods -- affect AuthChallengeList or IsAuthorized
    private void ClearSensitiveFields()
    {
        login = newLogin = string.Empty;
        password = newPassword = string.Empty;
        email = newEmail = string.Empty;
        phone = newPhone = string.Empty;
        code = string.Empty;
        IsLoginVerified = false;
        IsNewLoginVerified = false;
        IsEmailVerified = false;
        IsNewEmailVerified = false;
        IsPasswordVerified = false;
        IsNewPasswordVerified = false;
        IsPhoneVerified = false;
        IsNewPhoneVerified = false;
        IsCodeVerified = false;
    }
    public virtual void InternalClearAsync()
    {
        ClearSensitiveFields();
        CognitoUser = null;
        AuthChallengeList.Clear();
        authFlowResponse = null;
        IsSignedIn = false;
        CurrentAuthProcess = AuthProcessEnum.None;

    }
    public virtual Task<AuthEventEnum> ClearAsync()
    {
        InternalClearAsync();
        return Task.FromResult(AuthEventEnum.Cleared);
    }
    public virtual Task<AuthEventEnum> SignOutAsync()
    {
        InternalClearAsync();
        return Task.FromResult(AuthEventEnum.SignedOut);
    }
    // Cancel the currently executing auth process
    public virtual Task<AuthEventEnum> CancelAsync()
    {
        switch (CurrentAuthProcess)
        {
            case AuthProcessEnum.None:
            case AuthProcessEnum.SigningIn:
            case AuthProcessEnum.SigningUp:
            case AuthProcessEnum.ResettingPassword:
                InternalClearAsync();
                return Task.FromResult(AuthEventEnum.Canceled);

            default:
                ClearSensitiveFields();
                AuthChallengeList.Clear();
                CurrentAuthProcess = AuthProcessEnum.None;
                return Task.FromResult(AuthEventEnum.Canceled);
        }
    }
    public virtual Task<AuthEventEnum> StartSignInAsync()
    {
        if (IsSignedIn)
            return Task.FromResult(AuthEventEnum.Alert_AlreadySignedIn);

        InternalClearAsync();
        CurrentAuthProcess = AuthProcessEnum.SigningIn;
        AuthChallengeList.Add(AuthChallengeEnum.Login);
        AuthChallengeList.Add(AuthChallengeEnum.Password);
        return Task.FromResult(AuthEventEnum.AuthChallenge);
    }
    public virtual async Task<AuthEventEnum> StartSignUpAsync()
    {
        if (IsSignedIn)
            return AuthEventEnum.Alert_AlreadySignedIn;

        await ClearAsync();

        CurrentAuthProcess = AuthProcessEnum.SigningUp;

        AuthChallengeList.Add(AuthChallengeEnum.Login);
        AuthChallengeList.Add(AuthChallengeEnum.Password);
        AuthChallengeList.Add(AuthChallengeEnum.Email);
        return AuthEventEnum.AuthChallenge;
    }
    public virtual Task<AuthEventEnum> StartResetPasswordAsync()
    {

        if (IsSignedIn)
            return Task.FromResult(AuthEventEnum.Alert_InvalidOperationWhenSignedIn);

        CurrentAuthProcess = AuthProcessEnum.ResettingPassword;

        AuthChallengeList.Add(AuthChallengeEnum.Login);
        AuthChallengeList.Add(AuthChallengeEnum.NewPassword);
        return Task.FromResult(AuthEventEnum.AuthChallenge);

    }
    public virtual Task<AuthEventEnum> StartUpdateLoginAsync()
    {
        return Task.FromResult(AuthEventEnum.Alert_OperationNotSupportedByAuthProvider);
    }
    public virtual Task<AuthEventEnum> StartUpdateEmailAsync()
    {
        if (!IsSignedIn)
            return Task.FromResult(AuthEventEnum.Alert_NeedToBeSignedIn);

        CurrentAuthProcess = AuthProcessEnum.UpdatingEmail;

        AuthChallengeList.Add(AuthChallengeEnum.NewEmail);
        return Task.FromResult(AuthEventEnum.AuthChallenge);
    }
    public virtual Task<AuthEventEnum> StartUpdatePhoneAsync()
    {
        return Task.FromResult(AuthEventEnum.Alert_InternalProcessError);
    }
    public virtual Task<AuthEventEnum> StartUpdatePasswordAsync()
    {
        if (!IsSignedIn)
            return Task.FromResult(AuthEventEnum.Alert_NeedToBeSignedIn);

        CurrentAuthProcess = AuthProcessEnum.UpdatingPassword;

        AuthChallengeList.Add(AuthChallengeEnum.Password);
        AuthChallengeList.Add(AuthChallengeEnum.NewPassword);
        return Task.FromResult(AuthEventEnum.AuthChallenge);
    }
    public virtual async Task<AuthEventEnum> VerifyLoginAsync(string login)
    {
        if (CurrentChallenge != AuthChallengeEnum.Login)
            return AuthEventEnum.Alert_VerifyCalledButNoChallengeFound;

        if (!CheckLoginFormat(login))
            return AuthEventEnum.Alert_LoginFormatRequirementsFailed;

        try
        {
            switch (CurrentAuthProcess)
            {
                case AuthProcessEnum.SigningIn:
                    if (IsSignedIn)
                        return AuthEventEnum.Alert_AlreadySignedIn;
                    if(string.IsNullOrEmpty(clientId)) throw new Exception("Config.UserPoolClientId is empty"); 
                    if(userPool is null) throw new Exception("Config.UserPoolId is null");
                    if(providerClient is null) throw new Exception("ProviderClient is null");
                    // We don't expect this to ever throw an exception as the AWS operation is local
                    CognitoUser = new CognitoUser(login, clientId, userPool, providerClient);
                    this.login = login;
                    IsLoginVerified = true;
                    AuthChallengeList.Remove(AuthChallengeEnum.Login);
                    return await NextChallenge();

                case AuthProcessEnum.SigningUp:
                    if (IsSignedIn)
                        return AuthEventEnum.Alert_AlreadySignedIn;
                    // We don't expect this to ever throw an exception
                    this.login = login;
                    IsLoginVerified = true;
                    AuthChallengeList.Remove(AuthChallengeEnum.Login);
                    return await NextChallenge();

                case AuthProcessEnum.ResettingPassword:
                    if (IsSignedIn)
                        return AuthEventEnum.Alert_InvalidOperationWhenSignedIn;
                    if (string.IsNullOrEmpty(clientId)) throw new Exception("Config.UserPoolClientId is empty");
                    if (userPool is null) throw new Exception("Config.UserPoolId is null");
                    if (providerClient is null) throw new Exception("ProviderClient is null");
                    // This step may throw an exception if the call to ForgotPasswordAsync fails.
                    CognitoUser = new CognitoUser(login, clientId, userPool, providerClient);
                    this.login = login;
                    IsLoginVerified = true;
                    AuthChallengeList.Remove(AuthChallengeEnum.Login);
                    return await NextChallenge();

                default:
                    return AuthEventEnum.Alert_InternalProcessError;
            }
        }
        catch (TooManyRequestsException) { return AuthEventEnum.Alert_TooManyAttempts; }
        catch (TooManyFailedAttemptsException) { return AuthEventEnum.Alert_TooManyAttempts; }
        catch (Exception e)
        {
            Debug.WriteLine($"VerifyLogin() threw an exception {e}");
            CognitoUser = null;
            return AuthEventEnum.Alert_Unknown;
        }
    }
    public virtual Task<AuthEventEnum> VerifyNewLoginAsync(string login)
    {
        return Task.FromResult(AuthEventEnum.Alert_OperationNotSupportedByAuthProvider);
    }
    public virtual async Task<AuthEventEnum> VerifyPasswordAsync(string password)
    {
        if (CurrentChallenge != AuthChallengeEnum.Password)
            return AuthEventEnum.Alert_VerifyCalledButNoChallengeFound;

        if (!CheckPasswordFormat(password))
            return AuthEventEnum.Alert_PasswordFormatRequirementsFailed;

        try
        {
            switch (CurrentAuthProcess)
            {
                case AuthProcessEnum.SigningIn:
                    if (IsSignedIn)
                        return AuthEventEnum.Alert_AlreadySignedIn;
                    // When using Cognito, we must have already verified login to create the CognitoUser
                    // before we can collect password and attempt to sign in.
                    if (!IsLoginVerified)
                        return AuthEventEnum.Alert_LoginMustBeSuppliedFirst;
                    // This step may throw an exception in the AWS StartWithSrpAuthAsync call.
                    // AWS exceptions for sign in are a bit hard to figure out in some cases.
                    // Depending on the UserPool setup, AWS may request an auth code. This would be 
                    // detected and handled in the NextChallenge() call. 
                    authFlowResponse = await CognitoUser!.StartWithSrpAuthAsync(
                        new InitiateSrpAuthRequest()
                        {
                            Password = password
                        }
                        ).ConfigureAwait(false);
                    this.password = password;
                    IsPasswordVerified = true;
                    AuthChallengeList.Remove(AuthChallengeEnum.Password);
                    return await NextChallenge();

                case AuthProcessEnum.SigningUp:
                    if (IsSignedIn)
                        return AuthEventEnum.Alert_AlreadySignedIn;
                    // We don't expect this step to throw an exception
                    this.password = password;
                    IsPasswordVerified = true;
                    AuthChallengeList.Remove(AuthChallengeEnum.Password);
                    return await NextChallenge();

                case AuthProcessEnum.UpdatingPassword:
                    if (!IsSignedIn)
                        return AuthEventEnum.Alert_NeedToBeSignedIn;
                    // We don't expect this step to throw an exception
                    this.password = password;
                    IsPasswordVerified = true;
                    AuthChallengeList.Remove(AuthChallengeEnum.Password);
                    return await NextChallenge();

                default:
                    return AuthEventEnum.Alert_InternalProcessError;
            }
        }
        catch (InvalidPasswordException) { return AuthEventEnum.Alert_PasswordFormatRequirementsFailed; }
        catch (TooManyRequestsException) { return AuthEventEnum.Alert_TooManyAttempts; }
        catch (TooManyFailedAttemptsException) { return AuthEventEnum.Alert_TooManyAttempts; }
        catch (NotAuthorizedException) { return AuthEventEnum.Alert_NotAuthorized; }
        catch (UserNotFoundException) { return AuthEventEnum.Alert_UserNotFound; }
        catch (UserNotConfirmedException) { return AuthEventEnum.Alert_NotConfirmed; }
        catch (Exception e)
        {
            Debug.WriteLine($"VerifyPassword() threw an exception {e}");
            CognitoUser = null;
            return AuthEventEnum.Alert_Unknown;
        }
    }
    public virtual async Task<AuthEventEnum> VerifyNewPasswordAsync(string newPassword)
    {
        if (CurrentChallenge != AuthChallengeEnum.NewPassword)
            return AuthEventEnum.Alert_VerifyCalledButNoChallengeFound;

        if (!CheckNewPasswordFormat(newPassword))
            return AuthEventEnum.Alert_PasswordFormatRequirementsFailed;

        try
        {
            switch (CurrentAuthProcess)
            {
                case AuthProcessEnum.SigningUp:
                    authFlowResponse = await CognitoUser!.RespondToNewPasswordRequiredAsync(
                        new RespondToNewPasswordRequiredRequest()
                        {
                            SessionID = authFlowResponse!.SessionID,
                            NewPassword = newPassword
                        }
                        ).ConfigureAwait(false);

                    this.newPassword = newPassword;
                    AuthChallengeList.Remove(AuthChallengeEnum.NewPassword);
                    return await NextChallenge();

                case AuthProcessEnum.ResettingPassword:
                    this.newPassword = newPassword;
                    CognitoUser user = new CognitoUser(login, clientId, userPool, providerClient);
                    await user.ForgotPasswordAsync().ConfigureAwait(false);
                    AuthChallengeList.Remove(AuthChallengeEnum.NewPassword);
                    AuthChallengeList.Add(AuthChallengeEnum.Code);
                    return await NextChallenge();

                case AuthProcessEnum.UpdatingPassword:
                    this.newPassword = newPassword;
                    AuthChallengeList.Remove(AuthChallengeEnum.NewPassword);
                    return await NextChallenge();

                default:
                    return AuthEventEnum.Alert_InternalProcessError;
            }
        }
        catch (InvalidPasswordException) { return AuthEventEnum.Alert_PasswordFormatRequirementsFailed; }
        catch (TooManyRequestsException) { return AuthEventEnum.Alert_TooManyAttempts; }
        catch (TooManyFailedAttemptsException) { return AuthEventEnum.Alert_TooManyAttempts; }
        catch (NotAuthorizedException) { return AuthEventEnum.Alert_NotAuthorized; }
        catch (UserNotFoundException) { return AuthEventEnum.Alert_UserNotFound; }
        catch (UserNotConfirmedException) { return AuthEventEnum.Alert_NotConfirmed; }
        catch (Exception e)
        {
            Debug.WriteLine($"VerifyPassword() threw an exception {e}");
            CognitoUser = null;
            return AuthEventEnum.Alert_Unknown;
        }
    }
    public virtual async Task<AuthEventEnum> VerifyEmailAsync(string email)
    {
        if (CurrentChallenge != AuthChallengeEnum.Email)
            return AuthEventEnum.Alert_VerifyCalledButNoChallengeFound;

        if (!CheckEmailFormat(email))
            return AuthEventEnum.Alert_EmailFormatRequirementsFailed;

        try
        {
            switch (CurrentAuthProcess)
            {
                case AuthProcessEnum.SigningUp:
                    IsEmailVerified = true;
                    this.email = email;
                    AuthChallengeList.Remove(AuthChallengeEnum.Email);
                    return await NextChallenge();

                default:
                    return AuthEventEnum.Alert_InternalProcessError;
            }
        }
        catch (TooManyRequestsException) { return AuthEventEnum.Alert_TooManyAttempts; }
        catch (TooManyFailedAttemptsException) { return AuthEventEnum.Alert_TooManyAttempts; }
        catch (Exception e)
        {
            Debug.WriteLine($"UpdateEmail() threw an exception {e}");
            return AuthEventEnum.Alert_Unknown;
        }

    }
    public virtual async Task<AuthEventEnum> VerifyNewEmailAsync(string newEmail)
    {
        if (CurrentChallenge != AuthChallengeEnum.NewEmail)
            return AuthEventEnum.Alert_VerifyCalledButNoChallengeFound;

        if (!CheckEmailFormat(newEmail))
            return AuthEventEnum.Alert_EmailFormatRequirementsFailed;

        try
        {
            switch (CurrentAuthProcess)
            {
                case AuthProcessEnum.UpdatingEmail:
                    if (!IsSignedIn)
                        return AuthEventEnum.Alert_NeedToBeSignedIn;
                    // Get the current values on the server. 
                    // This step may throw an exception in RefreshUserDetailsAsync. There seems to be
                    // no way to recover from this other than retry or abandon the process. Let the
                    // calling class figure out what is right for their usecase.
                    AuthEventEnum refreshUserDetailsResult = await RefreshUserDetailsAsync().ConfigureAwait(false);
                    if (refreshUserDetailsResult != AuthEventEnum.Alert_RefreshUserDetailsDone)
                        return AuthEventEnum.Alert_CantRetrieveUserDetails;

                    // make sure the values are different
                    if (email!.Equals(newEmail)) //todo - check
                    {
                        return AuthEventEnum.Alert_EmailAddressIsTheSame;
                    }

                    // Update the user email on the server
                    // This may throw an exception in the UpdateAttributesAsync call.
                    var attributes = new Dictionary<string, string>() { { "email", newEmail } };
                    // Cognito sends a auth code when the Email attribute is changed
                    await CognitoUser!.UpdateAttributesAsync(attributes).ConfigureAwait(false);

                    AuthChallengeList.Remove(AuthChallengeEnum.NewEmail);
                    IsEmailVerified = true;
                    return await NextChallenge();

                default:
                    return AuthEventEnum.Alert_InternalProcessError;
            }
        }
        catch (TooManyRequestsException) { return AuthEventEnum.Alert_TooManyAttempts; }
        catch (TooManyFailedAttemptsException) { return AuthEventEnum.Alert_TooManyAttempts; }
        catch (Exception e)
        {
            Debug.WriteLine($"UpdateEmail() threw an exception {e}");
            return AuthEventEnum.Alert_Unknown;
        }
    }
    public virtual async Task<AuthEventEnum> VerifyPhoneAsync(string phone)
    {
        if (CurrentChallenge != AuthChallengeEnum.Phone)
            return AuthEventEnum.Alert_VerifyCalledButNoChallengeFound;

        if (!CheckPhoneFormat(phone))
            return AuthEventEnum.Alert_PhoneFormatRequirementsFailed;

        AuthChallengeList.Remove(AuthChallengeEnum.Phone);
        return await NextChallenge();
    }
    public virtual async Task<AuthEventEnum> VerifyNewPhoneAsync(string newPhone)
    {
        if (CurrentChallenge != AuthChallengeEnum.NewPhone)
            return AuthEventEnum.Alert_VerifyCalledButNoChallengeFound;

        if (!CheckPhoneFormat(newPhone))
            return AuthEventEnum.Alert_PhoneFormatRequirementsFailed;

        AuthChallengeList.Remove(AuthChallengeEnum.NewPhone);
        return await NextChallenge();
    }
    public virtual async Task<AuthEventEnum> VerifyCodeAsync(string code)
    {
        if (CurrentAuthProcess == AuthProcessEnum.None)
            return AuthEventEnum.Alert_NoActiveAuthProcess;

        if (CurrentChallenge != AuthChallengeEnum.Code)
            return AuthEventEnum.Alert_VerifyCalledButNoChallengeFound;

        try
        {
            switch (CurrentAuthProcess)
            {
                case AuthProcessEnum.None:
                    return AuthEventEnum.Alert_InternalProcessError;

                case AuthProcessEnum.ResettingPassword:
                    await CognitoUser!.ConfirmForgotPasswordAsync(code, newPassword).ConfigureAwait(false);
                    AuthChallengeList.Remove(AuthChallengeEnum.Code);
                    return await NextChallenge();

                case AuthProcessEnum.SigningUp:
                    var result = await providerClient!.ConfirmSignUpAsync(
                        new ConfirmSignUpRequest
                        {
                            ClientId = clientId,
                            Username = login,
                            ConfirmationCode = code
                        }).ConfigureAwait(false);

                    IsCodeVerified = true;
                    AuthChallengeList.Remove(AuthChallengeEnum.Code);
                    return await NextChallenge();

                case AuthProcessEnum.SigningIn:
                    if (authFlowResponse == null) // authFlowResponse set during VerifyPassword
                        return AuthEventEnum.Alert_InternalSignInError;

                    authFlowResponse = await CognitoUser!.RespondToSmsMfaAuthAsync(
                        new RespondToSmsMfaRequest()
                        {
                            SessionID = authFlowResponse.SessionID,
                            MfaCode = code
                        }
                        ).ConfigureAwait(false);

                    AuthChallengeList.Remove(AuthChallengeEnum.Code);
                    return await NextChallenge();

                case AuthProcessEnum.UpdatingEmail:
                    await CognitoUser!.VerifyAttributeAsync("email", code).ConfigureAwait(false);
                    IsCodeVerified = true;
                    AuthChallengeList.Remove(AuthChallengeEnum.Code);
                    return await NextChallenge();

                case AuthProcessEnum.UpdatingPhone:
                    return AuthEventEnum.Alert_InternalProcessError;

                default:
                    return AuthEventEnum.Alert_InternalProcessError;

            }
        }
        catch (InvalidPasswordException) { return AuthEventEnum.Alert_PasswordFormatRequirementsFailed; }
        catch (TooManyRequestsException) { return AuthEventEnum.Alert_TooManyAttempts; }
        catch (TooManyFailedAttemptsException) { return AuthEventEnum.Alert_TooManyAttempts; }
        catch (NotAuthorizedException) { return AuthEventEnum.Alert_NotAuthorized; }
        catch (UserNotFoundException) { return AuthEventEnum.Alert_UserNotFound; }
        catch (UserNotConfirmedException) { return AuthEventEnum.Alert_NotConfirmed; }
        catch (CodeMismatchException) { return AuthEventEnum.Alert_VerifyFailed; }
        catch (AliasExistsException) { return AuthEventEnum.Alert_AccountWithThatEmailAlreadyExists; }
        catch (Exception e)
        {
            Debug.WriteLine($"VerifyCode() threw an exception {e}");
            CognitoUser = null;
            return AuthEventEnum.Alert_Unknown;
        }

    }
    public virtual async Task<AuthEventEnum> ResendCodeAsync()
    {
        if (CurrentChallenge != AuthChallengeEnum.Code)
            return AuthEventEnum.Alert_InvalidCallToResendAsyncCode;

        try
        {
            switch (CurrentAuthProcess)
            {
                case AuthProcessEnum.UpdatingEmail:
                    // We need to re-submit the email change request for Amazon to resend the code
                    if (!IsSignedIn)
                        return AuthEventEnum.Alert_NeedToBeSignedIn;
                    //// Get the current values on the server. 
                    //// This step may throw an exception in RefreshUserDetailsAsync. There seems to be
                    //// no way to recover from this other than retry or abandon the process. Let the
                    //// calling class figure out what is right for their usecase.
                    //AuthEventEnum refreshUserDetailsResult = await RefreshUserDetailsAsync().ConfigureAwait(false);
                    //if (refreshUserDetailsResult != AuthEventEnum.Alert_RefreshUserDetailsDone)
                    //    return AuthEventEnum.Alert_CantRetrieveUserDetails;

                    //// make sure the values are different
                    //if (this.email.Equals(newEmail)) //todo - check
                    //{
                    //    return AuthEventEnum.Alert_EmailAddressIsTheSame;
                    //}

                    //// Update the user email on the server
                    //// This may throw an exception in the UpdateAttributesAsync call.
                    //var attributes = new Dictionary<string, string>() { { "email", newEmail } };
                    //// Cognito sends a auth code when the Email attribute is changed
                    //await CognitoUser.UpdateAttributesAsync(attributes).ConfigureAwait(false);

                    await CognitoUser!.GetAttributeVerificationCodeAsync("email").ConfigureAwait(false);

                    return AuthEventEnum.VerificationCodeSent;

                case AuthProcessEnum.ResettingPassword:
                    // we need to issue the ForgotPassword again to resend code
                    CognitoUser user = new CognitoUser(login, clientId, userPool, providerClient);
                    await user.ForgotPasswordAsync().ConfigureAwait(false);
                    return AuthEventEnum.AuthChallenge;

                case AuthProcessEnum.SigningUp:
                    _ = await providerClient!.ResendConfirmationCodeAsync(
                        new ResendConfirmationCodeRequest
                        {
                            ClientId = clientId,
                            Username = login
                        }).ConfigureAwait(false);

                    return AuthEventEnum.AuthChallenge;
                default:
                    return AuthEventEnum.Alert_InvalidCallToResendAsyncCode;
            }

        }
        catch (Exception e)
        {
            Debug.WriteLine($"SignUp() threw an exception {e}");
            return AuthEventEnum.Alert_Unknown;
        }
    }
    private async Task<AuthEventEnum> NextChallenge(AuthEventEnum lastAuthEventEnum = AuthEventEnum.AuthChallenge)
    {
        try
        {
            if (!HasChallenge)
            {
                switch (CurrentAuthProcess)
                {
                    case AuthProcessEnum.None:
                        return AuthEventEnum.Alert_NothingToDo;

                    case AuthProcessEnum.ResettingPassword:

                        CurrentAuthProcess = AuthProcessEnum.None;
                        ClearSensitiveFields();
                        return AuthEventEnum.PasswordResetDone;

                    case AuthProcessEnum.SigningUp:

                        if (HasChallenge)
                            return AuthEventEnum.AuthChallenge;

                        if (!IsLoginFormatOk)
                            AuthChallengeList.Add(AuthChallengeEnum.Login);
                        else
                        if (!IsPasswordFormatOk)
                            AuthChallengeList.Add(AuthChallengeEnum.Password);
                        else
                        if (!IsEmailFormatOk)
                            AuthChallengeList.Add(AuthChallengeEnum.Email);

                        if (HasChallenge)
                            return AuthEventEnum.AuthChallenge;

                        if (!IsCodeVerified)
                        {
                            // Request Auth Code
                            var signUpRequest = new SignUpRequest()
                            {
                                ClientId = clientId,
                                Password = password,
                                Username = login
                            };

                            signUpRequest.UserAttributes.Add(
                                new AttributeType()
                                {
                                    Name = "email",
                                    Value = email
                                });

                            // This call may throw an exception
                            var result = await providerClient!.SignUpAsync(signUpRequest).ConfigureAwait(false);

                            if (!AuthChallengeList.Contains(AuthChallengeEnum.Code))
                                AuthChallengeList.Add(AuthChallengeEnum.Code);

                            return AuthEventEnum.AuthChallenge;
                        }

                        CurrentAuthProcess = AuthProcessEnum.None;
                        ClearSensitiveFields();
                        return AuthEventEnum.SignedUp;

                    case AuthProcessEnum.SigningIn:
                        if (authFlowResponse != null && authFlowResponse.ChallengeName == ChallengeNameType.NEW_PASSWORD_REQUIRED) // Update Passsword
                        {
                            if (!AuthChallengeList.Contains(AuthChallengeEnum.NewPassword))
                                AuthChallengeList.Add(AuthChallengeEnum.NewPassword);
                            authFlowResponse = null;
                            return AuthEventEnum.AuthChallenge;
                        }

                        // Grab JWT from login to User Pools to extract User Pool Identity
                        //var token = new JwtSecurityToken(jwtEncodedString: CognitoUser.SessionTokens.IdToken);
                        //UpIdentity = token.Claims.First(c => c.Type == "sub").Value; // JWT sub cliam contains User Pool Identity

                        //// Note: creates Identity Pool identity if it doesn't exist
                        if(!string.IsNullOrEmpty(identityPoolId))
                            Credentials = CognitoUser!.GetCognitoAWSCredentials(identityPoolId, regionEndpoint);

                        IsSignedIn = true;
                        CurrentAuthProcess = AuthProcessEnum.None;
                        ClearSensitiveFields();
                        return AuthEventEnum.SignedIn;

                    case AuthProcessEnum.UpdatingEmail:
                        if (!IsCodeVerified)
                        {
                            AuthChallengeList.Add(AuthChallengeEnum.Code);
                            return AuthEventEnum.VerificationCodeSent;
                        }

                        CurrentAuthProcess = AuthProcessEnum.None;
                        ClearSensitiveFields();
                        return AuthEventEnum.EmailUpdateDone;

                    case AuthProcessEnum.UpdatingPassword:
                        await CognitoUser!.ChangePasswordAsync(password, newPassword).ConfigureAwait(false);
                        CurrentAuthProcess = AuthProcessEnum.None;
                        ClearSensitiveFields();
                        return AuthEventEnum.PasswordUpdateDone;

                    case AuthProcessEnum.UpdatingPhone:
                        CurrentAuthProcess = AuthProcessEnum.None;
                        ClearSensitiveFields();
                        return AuthEventEnum.PhoneUpdateDone;
                }
            }
        }
        catch (UsernameExistsException) { return AuthEventEnum.Alert_LoginAlreadyUsed; }
        catch (InvalidParameterException) { return AuthEventEnum.Alert_InternalProcessError; }
        catch (InvalidPasswordException) { return AuthEventEnum.Alert_PasswordFormatRequirementsFailed; }
        catch (TooManyRequestsException) { return AuthEventEnum.Alert_TooManyAttempts; }
        catch (TooManyFailedAttemptsException) { return AuthEventEnum.Alert_TooManyAttempts; }
        catch (PasswordResetRequiredException) { return AuthEventEnum.Alert_PasswordResetRequiredException; }
        catch (Exception e)
        {
            Debug.WriteLine($"SignUp() threw an exception {e}");
            return AuthEventEnum.Alert_Unknown;
        }

        return lastAuthEventEnum;
    }
    #endregion

    #region Non ChallengeFlow Methods - do not affect AuthChallengeList or IaAuthorized
    public bool CheckLoginFormat(string? login)
    {
        login ??= "";
        FormatMessages = loginFormat!.CheckLoginFormat(login).ToArray();
        return IsLoginFormatOk = FormatMessages.Length == 0;
    }
    public bool CheckNewLoginFormat(string? newLogin)
    {
        newLogin ??= "";
        FormatMessages = loginFormat!.CheckLoginFormat(newLogin).ToArray();
        return IsLoginFormatOk = FormatMessages.Length == 0;
    }
    public bool CheckEmailFormat(string? email)
    {
        email ??= "";
        FormatMessages = emailFormat!.CheckEmailFormat(email).ToArray();
        return IsEmailFormatOk = FormatMessages.Length == 0;
    }
    public bool CheckNewEmailFormat(string? newEmail)
    {
        newEmail ??= "";
        FormatMessages = emailFormat!.CheckEmailFormat(newEmail).ToArray();
        return IsEmailFormatOk = FormatMessages.Length == 0;
    }
    public bool CheckPasswordFormat(string? password)
    {
        password ??= "";
        FormatMessages = passwordFormat!.CheckPasswordFormat(password).ToArray();
        return IsPasswordFormatOk = FormatMessages.Length == 0;
    }
    public bool CheckNewPasswordFormat(string? password)
    {
        password ??= "";
        FormatMessages = passwordFormat!.CheckPasswordFormat(password).ToArray();
        return IsNewPasswordFormatOk = FormatMessages.Length == 0;
    }
    public bool CheckCodeFormat(string? code)
    {
        code ??= "";
        FormatMessages = codeFormat!.CheckCodeFormat(code).ToArray();
        return IsCodeFormatOk = FormatMessages.Length == 0;
    }
    public bool CheckPhoneFormat(string? phone)
    {
        phone ??= "";
        FormatMessages = phoneFormat!.CheckPhoneFormat(phone).ToArray();
        return IsPhoneFormatOk = FormatMessages.Length == 0;
    }
    public bool CheckNewPhoneFormat(string? newPhone)
    {
        newPhone ??= "";
        FormatMessages = phoneFormat!.CheckPhoneFormat(newPhone).ToArray();
        return IsPhoneFormatOk = FormatMessages.Length == 0;
    }
    private Task NoOp()
    {
        return Task.CompletedTask;
    }
    public virtual async Task<string> GetAccessToken()
    {
        if (CognitoUser == null)
            return string.Empty;
        if (CognitoUser.SessionTokens.IsValid())
            return CognitoUser.SessionTokens.AccessToken;
        if (await RefreshTokenAsync())
            return CognitoUser.SessionTokens.AccessToken;
        return string.Empty;
    }
    public virtual async Task<string?> GetIdentityToken()
    {
        if (CognitoUser == null)
            return string.Empty; // Need to authenticate user first!

        if (!string.IsNullOrEmpty(identityPoolId))
        { // Using Identity Pools

            //var credentials = new CognitoAWSCredentials(IdentityPoolId, RegionEndpoint);
            CognitoAWSCredentials credentials = CognitoUser.GetCognitoAWSCredentials(identityPoolId, regionEndpoint);

            try
            {
                var IpIdentity = await credentials.GetIdentityIdAsync();
                Debug.WriteLine($" IpIdentity {IpIdentity}");
                return IpIdentity;
            }
            catch (Exception e)
            {
                Debug.WriteLine($"{e.Message}");
                return string.Empty;
            }
        }

        // Using UserPools directly
        if (CognitoUser.SessionTokens.IsValid())
            return CognitoUser.SessionTokens.IdToken;
        if (await RefreshTokenAsync())
            return CognitoUser.SessionTokens.IdToken;
        return null;
    }
    private async Task<bool> RefreshTokenAsync()
    {
        if (CognitoUser == null)
            return false;

        try
        {
            AuthFlowResponse context = await CognitoUser.StartWithRefreshTokenAuthAsync(new InitiateRefreshTokenAuthRequest
            {
                AuthFlowType = AuthFlowType.REFRESH_TOKEN_AUTH
            }).ConfigureAwait(false);
            return true;
        }
        catch (Exception e)
        {
            Debug.WriteLine($"RefreshToken() threw an exception {e}");
            return false;
        }
    }
    public virtual async Task<AuthEventEnum> RefreshUserDetailsAsync()
    {
        if (CognitoUser == null)
            return AuthEventEnum.Alert_NeedToBeSignedIn;

        try
        {
            // Get the current user attributes from the server
            // and set UserEmail and IsUserEmailVerified
            GetUserResponse getUserResponse = await CognitoUser.GetUserDetailsAsync().ConfigureAwait(false);
            foreach (AttributeType item in getUserResponse.UserAttributes)
            {
                if (item.Name.Equals("email"))
                    email = item.Value;

                if (item.Name.Equals("email_verified"))
                    IsEmailVerified = item.Value.Equals("true");
            }
            return AuthEventEnum.Alert_RefreshUserDetailsDone;
        }
        catch (Exception e)
        {
            Debug.WriteLine($"RefreshUserDetails threw an exception {e}");
            return AuthEventEnum.Alert_Unknown;
        }
    }
    #endregion

}

