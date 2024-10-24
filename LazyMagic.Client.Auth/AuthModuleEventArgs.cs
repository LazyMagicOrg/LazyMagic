namespace LazyMagic.Client.Auth;

// SeeAuthMessages.json for language specific messages
public enum AuthProcessEnum
{
    None,
    SigningIn,
    SigningUp,
    ResettingPassword,
    UpdatingLogin,
    UpdatingEmail,
    UpdatingPhone,
    UpdatingPassword,
    SigningOut
}

// SeeAuthMessages.json for language specific messages
public enum AuthChallengeEnum
{
    None, // No challenge
    Login,
    NewLogin,
    Password,
    NewPassword,
    Email,
    NewEmail,
    Phone,
    NewPhone,
    Code
} 

// SeeAuthMessages.json for language specific messages
public enum AuthEventEnum
{
    AuthChallenge, // One or more AuthChallenges are pending
    SignedIn, // User is fully authenticated and authorized
    SignedUp, // User is signed up, user needs to sign in to continue
    SignedOut,
    LoginUpdateDone,
    PasswordResetDone,
    PasswordUpdateDone,
    PhoneUpdateDone,
    EmailUpdateDone,
    VerificationCodeSent,
    Canceled,
    Cleared,

    // Alert events 
    Alert, // Any enum value >= than this enum item is an Alert - Alert not used itself.
    Alert_AuthProcessAlreadyStarted,
    Alert_DifferentAuthProcessActive,
    Alert_IncorrectAuthProcess,
    Alert_NoActiveAuthProcess,
    Alert_AlreadySignedIn,
    Alert_InternalSignInError,
    Alert_InternalSignUpError,
    Alert_InternalProcessError,
    Alert_SignUpMissingLogin,
    Alert_SignUpMissingPassword,
    Alert_SignUpMissingEmail,
    Alert_SignUpMissingCode,
    Alert_AuthAlreadyStarted,
    Alert_InvalidCallToResendAsyncCode,
    Alert_AccountWithThatEmailAlreadyExists,
    Alert_RefreshUserDetailsDone,
    Alert_EmailAddressIsTheSame,
    Alert_VerifyCalledButNoChallengeFound,
    Alert_CantRetrieveUserDetails,
    Alert_NeedToBeSignedIn,
    Alert_InvalidOperationWhenSignedIn,
    Alert_UserNotFound,
    Alert_NotConfirmed,
    Alert_NotAuthorized,
    Alert_VerifyFailed,
    Alert_LoginAlreadyUsed,
    Alert_LoginMustBeSuppliedFirst,
    Alert_LoginFormatRequirementsFailed,
    Alert_PasswordFormatRequirementsFailed,
    Alert_EmailFormatRequirementsFailed,
    Alert_PhoneFormatRequirementsFailed,
    Alert_TooManyAttempts,
    Alert_NothingToDo,
    Alert_OperationNotSupportedByAuthProvider,
    Alert_LimitExceededException,
    Alert_PasswordResetRequiredException,
    // Hail Marys
    Alert_Unknown
}

public class AuthModuleEventArgs : EventArgs
{
    public AuthEventEnum Result { get; }

    public AuthModuleEventArgs(AuthEventEnum r)
    {
        Result = r;
    }
}
