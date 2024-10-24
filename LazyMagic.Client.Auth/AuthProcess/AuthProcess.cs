

// Code Naming Conventions
// Property backing fields start with _ ex: _authProvider
// Public members start with uppercase ex: Authprovider
// Private members start with lowercase ex: myYada
// Local varaibles start with lowercase ex: bool result

namespace LazyMagic.Client.Auth;

/// <summary>
/// AuthProcess wraps the IAuthProvider class with state and processing 
/// useful for UI support.
/// Events, see property IsChatty:
///     When IsChatty is true (default), the class instance genertes events 
///     for almost every change to a property. This is suitable for clients 
///     like Xamarin. When IsChatty is false, the class does not generate 
///     events except for:
///      (None at this time, leaving in support in-case there is a need.)
///     This may be more suitable for clients like Blazor that tend to 
///     call StateChanged() in component code-behind methods handling 
///     user input.
/// 
/// </summary>
public class AuthProcess : NotifyBase, IAuthProcess
{

    public AuthProcess(IAuthProvider authProvider)
    {
        _authProvider = authProvider;

    }

    #region Fields
    AuthProcessEnum lastAuthProcessEnum = AuthProcessEnum.None;
    AuthChallengeEnum lastAuthChallengeEnum = AuthChallengeEnum.None;
    #endregion 

    #region Properties
    public bool IsChatty { get; set; } = true; // emit events?
    public bool ClearAllFields { get; set; } = false; // Affects behavior of ClearAsync and ClearSenstitiveFields
    public bool AssignFieldOnCheck { get; set; } = true;

    public List<AuthChallengeEnum> AuthChallengeList => _authProvider.AuthChallengeList;

    readonly IAuthProvider _authProvider;
    public IAuthProvider AuthProvider { get { return _authProvider; } }
    public bool AuthInitialized { get; set; }    
    public AuthChallengeEnum CurrentChallenge => _authProvider.CurrentChallenge;
    public AuthProcessEnum CurrentAuthProcess => _authProvider.CurrentAuthProcess;

    private string _login = string.Empty;
    public string Login
    {
        get { return _login; }
        set
        {
            if (IsChatty) 
                SetProperty(ref _login, value); 
            else 
                _login = value;
            CheckLoginFormat();
        }
    }

    private string _newLogin = string.Empty;
    public string NewLogin
    {
        get { return _newLogin; }
        set
        {
            if (IsChatty) SetProperty(ref _newLogin, value); else _newLogin = value;
            CheckLoginFormat();
        }
    }

    private string _password = string.Empty;
    public string Password
    {
        get { return _password; }
        set
        {
            if (IsChatty) SetProperty(ref _password, value); else _password = value;
            CheckPasswordFormat();
        }
    }

    private string _newPassword = string.Empty;
    public string NewPassword
    {
        get { return _newPassword; }
        set
        {
            if (IsChatty) SetProperty(ref _newPassword, value); else _newPassword = value;
            CheckNewPasswordFormat();
        }
    }

    private string _email = string.Empty;
    public string Email
    {
        get { return _email; }
        set
        {
            if (IsChatty) SetProperty(ref _email, value); else _email = value;
            CheckEmailFormat();
        }
    }

    private string _newEmail = string.Empty;
    public string NewEmail
    {
        get { return _newEmail; }
        set
        {
            if (IsChatty) SetProperty(ref _newEmail, value); else _newEmail = value;
            CheckEmailFormat();
        }
    }

    private string _phone = string.Empty;
    public string Phone
    {
        get { return _phone; }
        set { if (IsChatty) SetProperty(ref _phone, value); else _phone = value; }
    }

    private string _newPhone = string.Empty;
    public string NewPhone
    {
        get { return _newPhone; }
        set { if (IsChatty) SetProperty(ref _newPhone, value); else _newPhone = value; }
    }

    private string _code = string.Empty;
    public string Code
    {
        get { return _code; }
        set
        {
            if (IsChatty) SetProperty(ref _code, value); else _code = value;
            CheckCodeFormat();
        }
    }

    // UI LzMessages
    private string? _loginFormatMessage;
    public string? LoginFormatMessage => _loginFormatMessage;

    private string? _newLoginFormatMessage;
    public string? NewLoginFormatMessage => _newLoginFormatMessage;

    private string? _passwordFormatMessage;
    public string? PasswordFormatMessage => _passwordFormatMessage;

    private string? _newPasswordFormatMessage;
    public string? NewPasswordFormatMessage => _newPasswordFormatMessage; 

    private string? _emailFormatMessage;
    public string? EmailFormatMessage => _emailFormatMessage;

    private string? _newEmailFormatMessage;
    public string? NewEmailFormatMessage => _newEmailFormatMessage;

    private string? _phoneFormatMessage;
    public string? PhoneFormatMessage => _phoneFormatMessage;

    private string? _newPhoneFormatMessage;
    public string? NewPhoneFormatMessage => _newPhoneFormatMessage;

    private string? _codeFormatMessage;
    public string? CodeFormatMessage => _codeFormatMessage;


    // Entry Properties states
    public bool IsLoginFormatOk => _authProvider.IsLoginFormatOk;
    public bool IsLoginVerified => _authProvider.IsLoginVerified;
    public bool LoginNotVerified => !_authProvider.IsLoginVerified;

    public bool IsNewLoginFormatOk => _authProvider.IsNewLoginFormatOk;
    public bool IsNewLoginVerified => _authProvider.IsNewLoginVerified;
    public bool NewLoginNotVerified => !_authProvider.IsNewLoginVerified;

    public bool IsEmailFormatOk => _authProvider.IsEmailFormatOk;
    public bool IsEmailVerified => _authProvider.IsEmailVerified;
    public bool EmailNotVerified => !_authProvider.IsEmailVerified;

    public bool IsNewEmailFormatOk => _authProvider.IsNewEmailFormatOk;
    public bool IsNewEmailVerified => _authProvider.IsNewEmailVerified;
    public bool NewEmailNotVerified => !_authProvider.IsNewEmailVerified;

    public bool IsPasswordFormatOk => _authProvider.IsPasswordFormatOk;
    public bool IsPasswordVerified => _authProvider.IsPasswordVerified;
    public bool PasswordNotVerified => !_authProvider.IsPasswordVerified;

    public bool IsNewPasswordFormatOk => _authProvider.IsNewPasswordFormatOk;
    public bool IsNewPasswordVerified => _authProvider.IsNewPasswordVerified;
    public bool NewPasswordNotVerified => !_authProvider.IsNewPasswordVerified;

    public bool IsPhoneFormatOk => _authProvider.IsPhoneFormatOk;
    public bool IsPhoneVerified => _authProvider.IsPhoneVerified;
    public bool PhoneNotVerified => !_authProvider.IsPhoneVerified;

    public bool IsNewPhoneFormatOk => _authProvider.IsNewPhoneFormatOk;
    public bool IsNewPhoneVerified => _authProvider.IsNewPhoneVerified;
    public bool NewPhoneNotVerified => !_authProvider.IsNewPhoneVerified;

    public bool IsCodeFormatOk => _authProvider.IsCodeFormatOk;
    public bool IsCodeVerified => _authProvider.IsCodeVerified;
    public bool CodeNotVerified => !_authProvider.IsCodeVerified;

    public bool IsCleared => _authProvider.IsCleared;

    // Auth state
    public bool IsSignedIn => _authProvider.IsSignedIn;
    public bool IsNotSignedIn => !_authProvider.IsSignedIn;

    // Format LzMessages
    public string[]? FormatMessages { get { return _authProvider.FormatMessages; } }
    public string? FormatMessage { get { return _authProvider.FormatMessage; } }

    // CurrentAuthProcess
    private string? _authProcessMessage = string.Empty;
    public string? AuthProcessMessage => _authProcessMessage;
    public bool HasActiveAuthProcess => CurrentAuthProcess != AuthProcessEnum.None;
    public bool NoActiveAuthProcess => CurrentAuthProcess == AuthProcessEnum.None;
    public bool IsSigningIn => CurrentAuthProcess == AuthProcessEnum.SigningIn;
    public bool IsSigningUp => CurrentAuthProcess == AuthProcessEnum.SigningUp;
    public bool IsResettingPassword => CurrentAuthProcess == AuthProcessEnum.ResettingPassword;
    public bool IsUpdatingLogin => CurrentAuthProcess == AuthProcessEnum.UpdatingLogin;
    public bool IsUpdatingEmail => CurrentAuthProcess == AuthProcessEnum.UpdatingEmail;
    public bool IsUpdatingPhone => CurrentAuthProcess == AuthProcessEnum.UpdatingPhone;
    public bool IsUpdatingPassword => CurrentAuthProcess == AuthProcessEnum.UpdatingPassword;

    public bool IsChallengeLongWait => _authProvider.IsChallengeLongWait;


    // Challenge states
    private string? _authChallengeMessage = string.Empty;
    public string? AuthChallengeMessage => _authChallengeMessage;

    public bool HasChallenge => _authProvider.CurrentChallenge != AuthChallengeEnum.None;
    public bool NoChallenge => _authProvider.CurrentChallenge == AuthChallengeEnum.None;

    public bool CurrentChallengeIsLogin => _authProvider.CurrentChallenge == AuthChallengeEnum.Login;
    public bool CollectLogin => _authProvider.AuthChallengeList.Contains(AuthChallengeEnum.Login);

    public bool CurrentChallengeIsNewLogin => _authProvider.CurrentChallenge == AuthChallengeEnum.NewLogin;
    public bool CollectNewLogin => _authProvider.AuthChallengeList.Contains(AuthChallengeEnum.NewLogin);

    public bool CurrentChallengeIsPassword => _authProvider.CurrentChallenge == AuthChallengeEnum.Password;
    public bool CollectPassword => _authProvider.AuthChallengeList.Contains(AuthChallengeEnum.Password);

    public bool CurrentChallengeIsNewPassword => _authProvider.CurrentChallenge == AuthChallengeEnum.NewPassword;
    public bool CollectNewPassword => _authProvider.AuthChallengeList.Contains(AuthChallengeEnum.NewPassword);

    public bool CurrentChallengeIsEmail => _authProvider.CurrentChallenge == AuthChallengeEnum.Email;
    public bool CollectEmail => _authProvider.AuthChallengeList.Contains(AuthChallengeEnum.Email);

    public bool CurrentChallengeIsNewEmail => _authProvider.CurrentChallenge == AuthChallengeEnum.NewEmail;
    public bool CollectNewEmail => _authProvider.AuthChallengeList.Contains(AuthChallengeEnum.NewEmail);

    public bool CurrentChallengeIsPhone => _authProvider.CurrentChallenge == AuthChallengeEnum.Phone;
    public bool CollectPhone => _authProvider.AuthChallengeList.Contains(AuthChallengeEnum.Phone);

    public bool CurrentChallengeIsNewPhone => _authProvider.CurrentChallenge == AuthChallengeEnum.NewPhone;
    public bool CollectNewPhone => _authProvider.AuthChallengeList.Contains(AuthChallengeEnum.NewPhone);

    public bool CurrentChallengeIsCode => _authProvider.CurrentChallenge == AuthChallengeEnum.Code;
    public bool CollectCode => _authProvider.AuthChallengeList.Contains(AuthChallengeEnum.Code);

    // Alert states
    private string? _alertMessage = string.Empty;
    public string? AlertMessage => _alertMessage;
    public bool HasAlert => _alertMessage != null && _alertMessage.Length > 0; 

    // Currently Allowed AuthProcess
    public bool CanSignOut => _authProvider.CanSignOut;
    public bool CanSignIn => _authProvider.CanSignIn;
    public bool CanSignUp => _authProvider.CanSignUp;
    public bool CanResetPassword => _authProvider.CanResetPassword;
    public bool CanUpdateLogin => _authProvider.CanUpdateLogin;
    public bool CanUpdateEmail => _authProvider.CanUpdateEmail;
    public bool CanUpdatePassword => _authProvider.CanUpdatePassword;
    public bool CanUpdatePhone => _authProvider.CanUpdatePhone;
    public bool CanCancel => _authProvider.CanCancel;
    public bool CanNext => _authProvider.CanNext;
    public bool CanResendCode => _authProvider.CanResendCode;

    public bool _IsBusy = false;
    public bool IsBusy
    {
        get { return _IsBusy; }
        set
        {
            var diff = _IsBusy != value;
            SetProperty(ref _IsBusy, value);
            if(diff)
                RaisePropertyChanged(nameof(IsNotBusy));
        }
    }
    public bool IsNotBusy { get { return !_IsBusy; } }

    private bool _IsLongBusy;
    public bool IsLongBusy
    {
        get { return _IsLongBusy; }
        set { 
            var diff = _IsLongBusy != value;
            SetProperty(ref _IsLongBusy, value);
            if(diff)
                RaisePropertyChanged(nameof(IsNotLongBusy));
        }
    } 

    public bool IsNotLongBusy => !IsLongBusy;

    #endregion

    #region Methods

    /// <summary>
    /// Raise All Properties except:
    /// AlertMessage, ProcessMessage, ChallengeMessage which are 
    /// handled directly in RaiseAuthModuleEventAndProperties
    /// </summary>
    private void RaiseAllProperties()
    {
        if (!IsChatty)
            return;

        // This looks a little event heavy but isn't really
        // an issue as most of the bindings are hidden
        // or unused in the UI for any given auth process step.
        RaisePropertyChanged(nameof(CurrentChallenge));
        RaisePropertyChanged(nameof(CurrentAuthProcess));

        RaisePropertyChanged(nameof(Login));
        RaisePropertyChanged(nameof(NewLogin));
        RaisePropertyChanged(nameof(Password));
        RaisePropertyChanged(nameof(NewPassword));
        RaisePropertyChanged(nameof(Email));
        RaisePropertyChanged(nameof(NewEmail));
        RaisePropertyChanged(nameof(Phone));
        RaisePropertyChanged(nameof(NewPhone));
        RaisePropertyChanged(nameof(Code));

        RaisePropertyChanged(nameof(IsLoginFormatOk));
        RaisePropertyChanged(nameof(IsLoginVerified));
        RaisePropertyChanged(nameof(LoginNotVerified));

        RaisePropertyChanged(nameof(IsNewLoginFormatOk));
        RaisePropertyChanged(nameof(IsNewLoginVerified));
        RaisePropertyChanged(nameof(NewLoginNotVerified));

        RaisePropertyChanged(nameof(IsEmailFormatOk));
        RaisePropertyChanged(nameof(IsEmailVerified));
        RaisePropertyChanged(nameof(EmailNotVerified));

        RaisePropertyChanged(nameof(IsNewEmailFormatOk));
        RaisePropertyChanged(nameof(IsNewEmailVerified));
        RaisePropertyChanged(nameof(NewEmailNotVerified));

        RaisePropertyChanged(nameof(IsPasswordFormatOk));
        RaisePropertyChanged(nameof(IsPasswordVerified));
        RaisePropertyChanged(nameof(PasswordNotVerified));

        RaisePropertyChanged(nameof(IsNewPasswordFormatOk));
        RaisePropertyChanged(nameof(IsNewPasswordVerified));
        RaisePropertyChanged(nameof(NewPasswordNotVerified));

        RaisePropertyChanged(nameof(IsPhoneFormatOk));
        RaisePropertyChanged(nameof(IsPhoneVerified));
        RaisePropertyChanged(nameof(PhoneNotVerified));

        RaisePropertyChanged(nameof(IsNewPhoneFormatOk));
        RaisePropertyChanged(nameof(IsNewPhoneVerified));
        RaisePropertyChanged(nameof(NewPhoneNotVerified));

        RaisePropertyChanged(nameof(IsCodeFormatOk));
        RaisePropertyChanged(nameof(IsCodeVerified));
        RaisePropertyChanged(nameof(CodeNotVerified));

        RaisePropertyChanged(nameof(HasActiveAuthProcess));
        RaisePropertyChanged(nameof(NoActiveAuthProcess));

        RaisePropertyChanged(nameof(IsSignedIn));
        RaisePropertyChanged(nameof(IsNotSignedIn));

        RaisePropertyChanged(nameof(IsSigningIn));
        RaisePropertyChanged(nameof(IsSigningUp));
        RaisePropertyChanged(nameof(IsResettingPassword));
        RaisePropertyChanged(nameof(IsUpdatingLogin));
        RaisePropertyChanged(nameof(IsUpdatingEmail));
        RaisePropertyChanged(nameof(IsUpdatingPhone));
        RaisePropertyChanged(nameof(IsUpdatingPassword));

        RaisePropertyChanged(nameof(HasChallenge));
        RaisePropertyChanged(nameof(NoChallenge));

        RaisePropertyChanged(nameof(CurrentChallengeIsLogin));
        RaisePropertyChanged(nameof(CollectLogin));

        RaisePropertyChanged(nameof(CurrentChallengeIsNewLogin));
        RaisePropertyChanged(nameof(CollectNewLogin));

        RaisePropertyChanged(nameof(CurrentChallengeIsPassword));
        RaisePropertyChanged(nameof(CollectPassword));

        RaisePropertyChanged(nameof(CurrentChallengeIsNewPassword));
        RaisePropertyChanged(nameof(CollectNewPassword));

        RaisePropertyChanged(nameof(CurrentChallengeIsEmail));
        RaisePropertyChanged(nameof(CollectEmail));

        RaisePropertyChanged(nameof(CurrentChallengeIsNewEmail));
        RaisePropertyChanged(nameof(CollectNewEmail));

        RaisePropertyChanged(nameof(CurrentChallengeIsPhone));
        RaisePropertyChanged(nameof(CollectPhone));

        RaisePropertyChanged(nameof(CurrentChallengeIsNewPhone));
        RaisePropertyChanged(nameof(CollectNewPhone));

        RaisePropertyChanged(nameof(CurrentChallengeIsCode));
        RaisePropertyChanged(nameof(CollectCode));

        RaisePropertyChanged(nameof(HasAlert));

        // Currently Allowed AuthProcess
        RaisePropertyChanged(nameof(CanSignOut));
        RaisePropertyChanged(nameof(CanSignIn));
        RaisePropertyChanged(nameof(CanSignUp));
        RaisePropertyChanged(nameof(CanResetPassword));
        RaisePropertyChanged(nameof(CanUpdateLogin));
        RaisePropertyChanged(nameof(CanUpdateEmail));
        RaisePropertyChanged(nameof(CanUpdatePassword));
        RaisePropertyChanged(nameof(CanUpdatePhone));
        RaisePropertyChanged(nameof(CanCancel));
        RaisePropertyChanged(nameof(CanResendCode));

        RaisePropertyChanged(nameof(AlertMessage));

        RaisePropertyChanged(nameof(AuthChallengeMessage));

        RaisePropertyChanged(nameof(AuthProcessMessage));

    }

    private void ClearSensitiveFields()
    {
        Password = string.Empty;
        NewPassword = string.Empty;
        Code = string.Empty;
        if (ClearAllFields)
        {
            //Login = string.Empty;
            NewLogin = string.Empty;
            Email = string.Empty;
            NewEmail = string.Empty;
        }
    }

    public async Task<AuthEventEnum> ClearAsync() => await Execute(_authProvider.ClearAsync);

    public async Task<AuthEventEnum> CancelAsync() { 
        ClearSensitiveFields(); 
        var result = await _authProvider.CancelAsync();
        await RaiseAuthModuleEventAndProperties(result);
        return result;
    }

    public async Task<AuthEventEnum> SignOutAsync() { ClearSensitiveFields(); return await Execute(_authProvider.SignOutAsync); }
    public async Task<AuthEventEnum> StartSignInAsync() { ClearSensitiveFields(); return await Execute(_authProvider.StartSignInAsync); }
    public async Task<AuthEventEnum> StartSignUpAsync() { ClearSensitiveFields(); return await Execute(_authProvider.StartSignUpAsync); }
    public async Task<AuthEventEnum> StartResetPasswordAsync() { ClearSensitiveFields(); return await Execute(_authProvider.StartResetPasswordAsync); }
    public async Task<AuthEventEnum> StartUpdateLoginAsync() { ClearSensitiveFields(); return await Execute(_authProvider.StartUpdateLoginAsync); }
    public async Task<AuthEventEnum> StartUpdateEmailAsync() { ClearSensitiveFields(); return await Execute(_authProvider.StartUpdateEmailAsync); }
    public async Task<AuthEventEnum> StartUpdatePhoneAsync() { ClearSensitiveFields(); return await Execute(_authProvider.StartUpdatePhoneAsync); }
    public async Task<AuthEventEnum> StartUpdatePasswordAsync() { ClearSensitiveFields(); return await Execute(_authProvider.StartUpdatePasswordAsync); }
    public async Task<AuthEventEnum> Verify()
    {
        switch(CurrentChallenge)
        {
            case AuthChallengeEnum.None:
                return AuthEventEnum.Alert_VerifyCalledButNoChallengeFound;

            case AuthChallengeEnum.Login:
                if (!CheckLoginFormat(Login))
                    return AuthEventEnum.Alert_NotConfirmed;
                return await Execute(_authProvider.VerifyLoginAsync, Login);

            case AuthChallengeEnum.NewLogin:
                if (!CheckNewLoginFormat(NewLogin))
                    return AuthEventEnum.Alert_NotConfirmed;
                return await Execute(_authProvider.VerifyNewLoginAsync, NewLogin);

            case AuthChallengeEnum.Email:
                if (!CheckEmailFormat(Email))
                    return AuthEventEnum.Alert_NotConfirmed;
                return await Execute(_authProvider.VerifyEmailAsync, Email);

            case AuthChallengeEnum.NewEmail:
                if (!CheckNewEmailFormat(NewEmail))
                    return AuthEventEnum.Alert_NotConfirmed;
                return await Execute(_authProvider.VerifyNewEmailAsync, NewEmail);

            case AuthChallengeEnum.Phone:
                if (!CheckPhoneFormat(Phone))
                    return AuthEventEnum.Alert_NotConfirmed;
                return await Execute(_authProvider.VerifyPhoneAsync, Phone);

            case AuthChallengeEnum.NewPhone:
                if (!CheckNewPhoneFormat(NewPhone))
                    return AuthEventEnum.Alert_NotConfirmed;
                return await Execute(_authProvider.VerifyNewPhoneAsync, NewPhone);

            case AuthChallengeEnum.Password:
                if (!CheckPasswordFormat(Password))
                    return AuthEventEnum.Alert_NotConfirmed;
                return await Execute(_authProvider.VerifyPasswordAsync, Password);

            case AuthChallengeEnum.NewPassword:
                if (!CheckNewPasswordFormat(NewPassword))
                    return AuthEventEnum.Alert_NotConfirmed;
                return await Execute(_authProvider.VerifyNewPasswordAsync, NewPassword);

            case AuthChallengeEnum.Code:
                if (!CheckCodeFormat(Code))
                    return AuthEventEnum.Alert_NotConfirmed;
                return await Execute(_authProvider.VerifyCodeAsync, Code);

            default:
                return AuthEventEnum.Alert_NothingToDo;
        }
    }
    public async Task<AuthEventEnum> Verify(string val)
    {
        switch(CurrentChallenge)
        {
            case AuthChallengeEnum.None:
                return AuthEventEnum.Alert_VerifyCalledButNoChallengeFound;
            case AuthChallengeEnum.Login:
                Login = val;
                return await Verify();
            case AuthChallengeEnum.NewLogin:
                NewLogin = val;
                return await Verify();

            case AuthChallengeEnum.Email:
                Email = val;    
                return await Verify();
            case AuthChallengeEnum.NewEmail:
                NewEmail = val; 
                return await Verify();

            case AuthChallengeEnum.Phone:
                Phone = val;    
                return await Verify();
            case AuthChallengeEnum.NewPhone:
                NewPhone = val;
                return await Verify();

            case AuthChallengeEnum.Password:
                Password = val; 
                return await Verify();
            case AuthChallengeEnum.NewPassword:
                NewPassword = val;
                return await Verify();

            case AuthChallengeEnum.Code:
                Code = val;
                return await Verify();
            default:
                return AuthEventEnum.Alert_NothingToDo;
        }
    }
    public async Task<AuthEventEnum> VerifyLoginAsync() => await Execute(_authProvider.VerifyLoginAsync, Login);
    public async Task<AuthEventEnum> VerifyLoginAsync(string login)
    {
        Login = login;
        return await Execute(_authProvider.VerifyLoginAsync, Login);
    }

    public async Task<AuthEventEnum> VerifyNewLoginAsync() => await Execute(_authProvider.VerifyNewLoginAsync, NewLogin);
    public async Task<AuthEventEnum> VerifyNewLoginAsync(string newLogin)
    {
        _newLogin = newLogin;
        return await Execute(_authProvider.VerifyNewLoginAsync, NewLogin);
    }

    public async Task<AuthEventEnum> VerifyPasswordAsync() => await Execute(_authProvider.VerifyPasswordAsync, Password);
    public async Task<AuthEventEnum> VerifyPasswordAsync(string password)
    {
        _password = password;
        return await Execute(_authProvider.VerifyPasswordAsync, Password);
    }
    public async Task<AuthEventEnum> VerifyNewPasswordAsync() => await Execute(_authProvider.VerifyNewPasswordAsync, NewPassword);
    public async Task<AuthEventEnum> VerifyNewPasswordAsync(string newPassword)
    {
        _newPassword = newPassword;
        return await Execute(_authProvider.VerifyNewPasswordAsync, NewPassword);
    }

    public async Task<AuthEventEnum> VerifyEmailAsync() => await Execute(_authProvider.VerifyEmailAsync, Email);
    public async Task<AuthEventEnum> VerifyEmailAsync(string email)
    {
        _email = email;
        return await Execute(_authProvider.VerifyEmailAsync, Email);
    }

    public async Task<AuthEventEnum> VerifyNewEmailAsync() => await Execute(_authProvider.VerifyNewEmailAsync, NewEmail);
    public async Task<AuthEventEnum> VerifyNewEmailAsync(string newEmail)
    {
        _newEmail = newEmail;
        return await Execute(_authProvider.VerifyNewEmailAsync, NewEmail);
    }

    public async Task<AuthEventEnum> VerifyPhoneAsync() => await Execute(_authProvider.VerifyPhoneAsync, Phone);
    public async Task<AuthEventEnum> VerifyPhoneAsync(string phone)
    {
        _phone = phone;
        return await Execute(_authProvider.VerifyPhoneAsync, Phone);
    }

    public async Task<AuthEventEnum> VerifyNewPhoneAsync() => await Execute(_authProvider.VerifyNewPhoneAsync, NewPhone);
    public async Task<AuthEventEnum> VerifyNewPhoneAsync(string newPhone)
    {
        _newPhone = newPhone;
        return await Execute(_authProvider.VerifyNewPhoneAsync, NewPhone);
    }

    public async Task<AuthEventEnum> VerifyCodeAsync() => await Execute(_authProvider.VerifyCodeAsync, Code);
    public async Task<AuthEventEnum> VerifyCodeAsync(string code)
    {
        _code = code;
        return await Execute(_authProvider.VerifyCodeAsync, Code);
    }

    public async Task<AuthEventEnum> ResendCodeAsync() => await Execute(_authProvider.ResendCodeAsync);

    public async Task<AuthEventEnum> RefreshUserDetailsAsync() => await Execute(_authProvider.RefreshUserDetailsAsync);


    public bool CheckFormat()
    {
        switch(CurrentChallenge)
        {
            case AuthChallengeEnum.None:
                return false;
            case AuthChallengeEnum.Login:
                return CheckLoginFormat();
            case AuthChallengeEnum.NewLogin:
                return CheckNewLoginFormat();
            case AuthChallengeEnum.Email:
                return CheckEmailFormat();
            case AuthChallengeEnum.NewEmail:
                return CheckNewEmailFormat();
            case AuthChallengeEnum.Phone:
                return CheckPhoneFormat();
            case AuthChallengeEnum.NewPhone:
                return CheckNewPhoneFormat();
            case AuthChallengeEnum.Password:
                return CheckPasswordFormat();
            case AuthChallengeEnum.NewPassword:
                return CheckNewPasswordFormat();
            case AuthChallengeEnum.Code:
                return CheckCodeFormat();   
            default:
                return false;
        }
    }

    public bool CheckFormat(string? val)
    {
        val ??= "";
        switch(CurrentChallenge)
        {
            case AuthChallengeEnum.None:
                return false;
            case AuthChallengeEnum.Login:
                Login = val;
                return CheckFormat();
            case AuthChallengeEnum.NewLogin:
                NewLogin = val;
                return CheckFormat();
            case AuthChallengeEnum.Email:
                Email = val;    
                return CheckFormat();
            case AuthChallengeEnum.NewEmail:
                NewEmail = val;
                return CheckFormat();
            case AuthChallengeEnum.Phone:
                Phone= val;
                return CheckFormat();
            case AuthChallengeEnum.NewPhone:
                NewPhone = val;
                return CheckFormat();
            case AuthChallengeEnum.Password:
                Password= val;  
                return CheckFormat();
            case AuthChallengeEnum.NewPassword:
                NewPassword = val;
                return CheckFormat();
            case AuthChallengeEnum.Code:
                Code = val; 
                return CheckFormat();
            default:
                return false;
        }
    }
    public bool CheckLoginFormat() => CheckLoginFormat(null);

    public bool CheckNewLoginFormat() => CheckNewLoginFormat(null);

    public bool CheckPasswordFormat() => CheckPasswordFormat(null);

    public bool CheckNewPasswordFormat() => CheckPasswordFormat(null);
    public bool CheckEmailFormat() => CheckEmailFormat(null);
    public bool CheckNewEmailFormat() => CheckNewEmailFormat(null);
    public bool CheckPhoneFormat() => CheckPhoneFormat(null);
    public bool CheckNewPhoneFormat() => CheckNewPhoneFormat(null);
    public bool CheckCodeFormat() => CheckCodeFormat(null);

    public bool CheckLoginFormat(string? login)
    {
        if (AssignFieldOnCheck && login != null)
            Login = login;
        else
            login = Login;
        var result = _authProvider.CheckLoginFormat(login);
        _loginFormatMessage = FormatMessages?.FirstOrDefault();
        if (IsChatty) RaisePropertyChanged(nameof(IsLoginFormatOk));
        return result;
    }

    public bool CheckNewLoginFormat(string? login)
    {
        if (AssignFieldOnCheck && login != null)
            NewLogin = login;
        else
            login = NewLogin;
        var result = _authProvider.CheckLoginFormat(login);
        _newLoginFormatMessage = FormatMessages?.FirstOrDefault();
        if (IsChatty) RaisePropertyChanged(nameof(IsLoginFormatOk));
        return result;
    }

    public bool CheckPasswordFormat(string? password)
    {
        if (AssignFieldOnCheck && password != null)
            Password = password;
        else
            password = Password;
        var result = _authProvider.CheckPasswordFormat(password);
        //_passwordFormatMessage = Array.Find<string>(FormatMessages, p => true);
        _passwordFormatMessage = FormatMessages?.FirstOrDefault();
        if (IsChatty) RaisePropertyChanged(nameof(IsPasswordFormatOk));
        return result;
    }

    public bool CheckNewPasswordFormat(string? password)
    {
        if (AssignFieldOnCheck && password != null)
            NewPassword = password;
        else
            password = NewPassword;
        var result = _authProvider.CheckNewPasswordFormat(password);
        //_newPasswordFormatMessage = Array.Find<string>(FormatMessages, p => true);
        _newPasswordFormatMessage = FormatMessages?.FirstOrDefault();
        if (IsChatty) RaisePropertyChanged(nameof(IsNewPasswordFormatOk));
        return result;
    }

    public bool CheckEmailFormat(string? email)
    {
        if (AssignFieldOnCheck && email != null)
            Email = email;
        else
            email = Email;
        var result = _authProvider.CheckEmailFormat(email);
        // _emailFormatMessage = Array.Find<string>(FormatMessages, p => true);
        _emailFormatMessage = FormatMessages?.FirstOrDefault();
        if (IsChatty) RaisePropertyChanged(nameof(IsEmailFormatOk));
        return result;
    }

    public bool CheckNewEmailFormat(string? email)
    {
        if (AssignFieldOnCheck && email != null)
            NewEmail = email;
        else
            email = NewEmail;
        var result = _authProvider.CheckEmailFormat(email);
        // _newEmailFormatMessage = Array.Find<string>(FormatMessages, p => true);
        _newEmailFormatMessage = FormatMessages?.FirstOrDefault();
        if (IsChatty) RaisePropertyChanged(nameof(IsEmailFormatOk));
        return result;
    }

    public bool CheckPhoneFormat(string? phone)
    {
        if (AssignFieldOnCheck && phone != null)
            Phone = phone;
        else
            phone = Phone;
        var result = _authProvider.CheckPhoneFormat(phone);
        // _phoneFormatMessage = Array.Find<string>(FormatMessages, p => true);
        _phoneFormatMessage = FormatMessages?.FirstOrDefault();
        if (IsChatty) RaisePropertyChanged(nameof(IsPhoneFormatOk));
        return result;

    }

    public bool CheckNewPhoneFormat(string? phone)
    {
        if (AssignFieldOnCheck && phone != null)
            NewPhone = phone;
        else
            phone = NewPhone;
        var result = _authProvider.CheckPhoneFormat(phone);
        // _newPhoneFormatMessage = Array.Find<string>(FormatMessages, p => true);
        _newPhoneFormatMessage = FormatMessages?.FirstOrDefault();
        if (IsChatty) RaisePropertyChanged(nameof(IsPhoneFormatOk));
        return result;

    }

    public bool CheckCodeFormat(string? code)
    {
        if (AssignFieldOnCheck && code != null)
            Code = code;
        else
            code = Code;
        var result = _authProvider.CheckCodeFormat(code);
        // _codeFormatMessage = Array.Find<string>(FormatMessages, p => true);
        _codeFormatMessage = FormatMessages?.FirstOrDefault();
        if (IsChatty) RaisePropertyChanged(nameof(IsCodeFormatOk));
        return result;

    }

    public async Task<Creds?> GetCredsAsync()
    {
        return await _authProvider!.GetCredsAsync();
    }

    public async Task<string?> GetJWTAsync()
    {
        return await _authProvider.GetJWTAsync();
    }

    // Wrap an execution in a IsBusy
    protected async virtual Task<AuthEventEnum> Execute(Func<Task<AuthEventEnum>> func)
    {
        IsBusy = true;
        if (IsLongBusy = IsChallengeLongWait) 
            await Task.Delay(10);
        var result = await func();
        result = await RaiseAuthModuleEventAndProperties(result);
        IsBusy = false;
        IsLongBusy = false;
        return result;
    }

    protected async virtual Task<AuthEventEnum> Execute(Func<string, Task<AuthEventEnum>> func, string arg)
    {
        IsBusy = true;
        if (IsLongBusy = IsChallengeLongWait) 
            await Task.Delay(10);
        var result = await func(arg);
        result = await RaiseAuthModuleEventAndProperties(result); 
        IsBusy = false;
        IsLongBusy = false;
        return result;
    }

    #endregion

    #region AuthProcessEvents
    public static event EventHandler<AuthModuleEventArgs>? AuthModuleEventFired;

    // Wrap event invocations inside a protected virtual method
    // to allow derived classes to override the event invocation behavior
    protected virtual void OnAuthModuleEvent(AuthModuleEventArgs e)
    {
        // Make a temporary copy of the event to avoid possibility of
        // a race condition if the last subscriber unsubscribes
        // immediately after the null check and before the event is raised.
        AuthModuleEventFired?.Invoke(this, e);
    }

    protected async virtual Task<AuthEventEnum> RaiseAuthModuleEventAndProperties(AuthEventEnum r)
    {
        await Task.Delay(0);

        OnAuthModuleEvent(new AuthModuleEventArgs(r));
        // update alert message
        _alertMessage = string.Empty;
        if ((int)r >= (int)AuthEventEnum.Alert) // All enum items after the enum Alert are Alerts
        {
            string message = $"AuthAlertMessages_{r}";
            _alertMessage =
                message == null
                ? r.ToString()
                : message;
        }

        // update process message
        if (string.IsNullOrEmpty(AuthChallengeMessage) || lastAuthChallengeEnum != CurrentChallenge)
        {
            string challengeMessage = $"AuthChallengeMessages_{CurrentChallenge}";
            _authChallengeMessage =
                challengeMessage == null
                ? CurrentChallenge.ToString()
                : challengeMessage;
            lastAuthChallengeEnum = CurrentChallenge;
        }

        // update challenge message
        if (string.IsNullOrEmpty(AuthProcessMessage) || lastAuthProcessEnum != CurrentAuthProcess)
        {
            string processMessage = $"AuthProcessMessages_:{CurrentAuthProcess}";
            _authProcessMessage =
                processMessage == null
                ? CurrentAuthProcess.ToString()
                : processMessage;
            lastAuthProcessEnum = CurrentAuthProcess;
        }

        if (r == AuthEventEnum.SignedIn
            || r == AuthEventEnum.SignedUp
            || r == AuthEventEnum.SignedOut
            || r == AuthEventEnum.PasswordResetDone
            )
        {
            ClearSensitiveFields();
        }

        RaisePropertyChanged(nameof(IsSignedIn));
        RaisePropertyChanged(nameof(IsNotSignedIn));

        if (IsChatty) RaiseAllProperties();
        return r;
    }


    void RaiseAuthModuleEvent(AuthEventEnum r)
    {
        OnAuthModuleEvent(new AuthModuleEventArgs(r));
    }

    public virtual void SetAuthenticator(JObject authenticator)
    {
        _authProvider.SetAuthenticator(authenticator);
    }
    public virtual void SetSignUpAllowed(bool isAllowed)
    {
        _authProvider.SetSignUpAllowed(isAllowed);
    }
    #endregion

}
