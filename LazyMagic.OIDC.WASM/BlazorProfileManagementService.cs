namespace LazyMagic.OIDC.WASM;

public class BlazorProfileManagementService : IProfileManagementService
{
    private readonly IDynamicConfigurationProvider _dynamicConfig;
    private readonly NavigationManager _navigation;
    private readonly IJSRuntime _js;

    public BlazorProfileManagementService(
        IDynamicConfigurationProvider dynamicConfig,
        NavigationManager navigation,
        IJSRuntime js)
    {
        _dynamicConfig = dynamicConfig;
        _navigation = navigation;
        _js = js;
    }

    public async Task<ProfileManagementResult> GetPasswordChangeUrlAsync()
    {
        var clientId = _dynamicConfig.GetClientId();
        var providerType = _dynamicConfig.GetProviderType();
        
        if (providerType?.ToLower() == "cognito")
        {
            var logoutEndpoint = _dynamicConfig.GetLogoutEndpoint();
            if (!string.IsNullOrEmpty(logoutEndpoint))
            {
                var cognitoDomain = logoutEndpoint.Replace("/logout", "");
                var redirectUri = Uri.EscapeDataString($"{_navigation.BaseUri}AuthPage/login-callback");
                var changePasswordUrl = $"{cognitoDomain}/forgotPassword?client_id={clientId}&response_type=code&redirect_uri={redirectUri}";
                
                return new ProfileManagementResult
                {
                    Success = true,
                    RedirectUrl = changePasswordUrl
                };
            }
        }
        
        return new ProfileManagementResult
        {
            Success = false,
            Message = "Password change is not available for this authentication provider."
        };
    }

    public async Task<ProfileManagementResult> GetProfileUpdateUrlAsync()
    {
        var providerType = _dynamicConfig.GetProviderType();
        
        if (providerType?.ToLower() == "cognito")
        {
            return new ProfileManagementResult
            {
                Success = false,
                Message = "Profile updates require custom implementation using Cognito APIs. " +
                         "For now, you can update your profile through the AWS Cognito console or contact support."
            };
        }
        
        return new ProfileManagementResult
        {
            Success = false,
            Message = "Profile update is not available for this authentication provider."
        };
    }

    public async Task<ProfileManagementResult> GetPasswordResetUrlAsync()
    {
        var clientId = _dynamicConfig.GetClientId();
        var providerType = _dynamicConfig.GetProviderType();
        
        if (providerType?.ToLower() == "cognito")
        {
            var logoutEndpoint = _dynamicConfig.GetLogoutEndpoint();
            if (!string.IsNullOrEmpty(logoutEndpoint))
            {
                var cognitoDomain = logoutEndpoint.Replace("/logout", "");
                var redirectUri = Uri.EscapeDataString($"{_navigation.BaseUri}AuthPage/login");
                var resetUrl = $"{cognitoDomain}/forgotPassword?client_id={clientId}&response_type=code&redirect_uri={redirectUri}";
                
                return new ProfileManagementResult
                {
                    Success = true,
                    RedirectUrl = resetUrl
                };
            }
        }
        
        return new ProfileManagementResult
        {
            Success = false,
            Message = "Password reset is not available for this authentication provider."
        };
    }

    private async Task HandleResult(ProfileManagementResult result)
    {
        if (result.Success && !string.IsNullOrEmpty(result.RedirectUrl))
        {
            Console.WriteLine($"[ProfileManagement] Redirecting to: {result.RedirectUrl}");
            await _js.InvokeVoidAsync("eval", $"window.location.href = '{result.RedirectUrl}'");
        }
        else if (!string.IsNullOrEmpty(result.Message))
        {
            Console.WriteLine($"[ProfileManagement] {result.Message}");
            await _js.InvokeVoidAsync("alert", result.Message);
        }
    }
}