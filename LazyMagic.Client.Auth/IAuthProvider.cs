/// <summary>
/// General authentication flow Interface
/// </summary>
public interface IAuthProvider
{
    // Properties
    public bool AuthInitialized { get; set; }   
    public List<AuthChallengeEnum> AuthChallengeList { get; }
    public AuthChallengeEnum CurrentChallenge { get; }
    public AuthProcessEnum CurrentAuthProcess { get; }
    public bool IsLoginFormatOk { get; }
    public bool IsLoginVerified { get; }
    public bool IsNewLoginFormatOk { get; }
    public bool IsNewLoginVerified { get; }
    public bool IsEmailFormatOk { get; }
    public bool IsEmailVerified { get; }
    public bool IsNewEmailFormatOk { get; }
    public bool IsNewEmailVerified { get; }
    public bool IsPasswordFormatOk { get; }
    public bool IsPasswordVerified { get; }
    public bool IsNewPasswordFormatOk { get; }
    public bool IsNewPasswordVerified { get; }
    public bool IsPhoneFormatOk { get; }
    public bool IsPhoneVerified { get; }
    public bool IsNewPhoneFormatOk { get; }
    public bool IsNewPhoneVerified { get; }
    public bool IsCodeFormatOk { get; }
    public bool IsCodeVerified { get; }
    public bool IsCleared { get; } // Check if sensitive fields are cleared: password, newPassword, code
    // Auth state
    public bool IsSignedIn { get; }
    // Challenge states
    public bool HasChallenge { get; }
    // Format LzMessages
    public string[]? FormatMessages { get; }
    public string? FormatMessage { get; }

    // Currently Allows AuthProcess
    public bool CanSignOut { get; }
    public bool CanSignUp { get; }
    public bool CanSignIn { get; }
    public bool CanResetPassword { get; }
    public bool CanUpdateLogin { get; }
    public bool CanUpdateEmail { get; }
    public bool CanUpdatePassword { get; }
    public bool CanUpdatePhone { get; }
    public bool CanCancel { get; }
    public bool CanNext { get; }    
    public bool CanResendCode { get; }

    public bool IsChallengeLongWait { get; } //

    // Methods
    public Task<AuthEventEnum> ClearAsync();
    public Task<AuthEventEnum> CancelAsync();
    public Task<AuthEventEnum> SignOutAsync();
    public Task<AuthEventEnum> StartSignInAsync();
    public Task<AuthEventEnum> StartSignUpAsync();
    public Task<AuthEventEnum> StartResetPasswordAsync();
    public Task<AuthEventEnum> StartUpdateLoginAsync();
    public Task<AuthEventEnum> StartUpdateEmailAsync();
    public Task<AuthEventEnum> StartUpdatePhoneAsync();
    public Task<AuthEventEnum> StartUpdatePasswordAsync();

    public Task<AuthEventEnum> VerifyLoginAsync(string login);
    public Task<AuthEventEnum> VerifyNewLoginAsync(string newLogin);
    public Task<AuthEventEnum> VerifyPasswordAsync(string password);
    public Task<AuthEventEnum> VerifyNewPasswordAsync(string newPassword);
    public Task<AuthEventEnum> VerifyEmailAsync(string email);
    public Task<AuthEventEnum> VerifyNewEmailAsync(string newEmail);
    public Task<AuthEventEnum> VerifyPhoneAsync(string phone);
    public Task<AuthEventEnum> VerifyNewPhoneAsync(string newPhone);
    public Task<AuthEventEnum> VerifyCodeAsync(string code);

    public Task<AuthEventEnum> ResendCodeAsync();
    public Task<AuthEventEnum> RefreshUserDetailsAsync();

    public bool CheckLoginFormat(string? login);
    public bool CheckEmailFormat(string? email);
    public bool CheckPasswordFormat(string? password);
    public bool CheckNewLoginFormat(string? login);
    public bool CheckNewEmailFormat(string? email);
    public bool CheckNewPhoneFormat(string? phone);
    public bool CheckNewPasswordFormat(string? password);
    public bool CheckPhoneFormat(string? phone);
    public bool CheckCodeFormat(string? code);

    public Task<Creds?> GetCredsAsync();
    public Task<string?> GetJWTAsync();
    public void SetAuthenticator(JObject authenticator);
    public void SetSignUpAllowed(bool isAllowed);

}
