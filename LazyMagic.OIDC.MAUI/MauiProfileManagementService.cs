namespace LazyMagic.OIDC.MAUI;

public class MauiProfileManagementService : IProfileManagementService
{
    private readonly IDynamicConfigurationProvider _dynamicConfig;
    private readonly IWebViewAuthenticationProvider? _webViewProvider;
    private readonly ILogger<MauiProfileManagementService> _logger;

    public MauiProfileManagementService(
        IDynamicConfigurationProvider dynamicConfig, 
        IWebViewAuthenticationProvider? webViewProvider = null,
        ILogger<MauiProfileManagementService>? logger = null)
    {
        _dynamicConfig = dynamicConfig;
        _webViewProvider = webViewProvider;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<MauiProfileManagementService>.Instance;
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
                var redirectUri = Uri.EscapeDataString("awsloginmaui://auth-callback");
                var changePasswordUrl = $"{cognitoDomain}/forgotPassword?client_id={clientId}&response_type=code&redirect_uri={redirectUri}";
                
                if (_webViewProvider != null)
                {
                    try
                    {
                        _logger.LogInformation("Opening password change in WebView: {Url}", changePasswordUrl);
                        
                        // Use the dedicated password operation method for proper UI
                        var result = await _webViewProvider.HandlePasswordOperationAsync(changePasswordUrl, "awsloginmaui://", "Password Change");
                        
                        if (!string.IsNullOrEmpty(result))
                        {
                            _logger.LogInformation("Password change flow completed with callback: {Result}", result);
                            return new ProfileManagementResult
                            {
                                Success = true,
                                Message = "Password change flow completed. Please check your email for further instructions."
                            };
                        }
                        else
                        {
                            _logger.LogInformation("Password change flow was cancelled by user");
                            return new ProfileManagementResult
                            {
                                Success = false,
                                Message = "Password change was cancelled."
                            };
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error during password change flow");
                        return new ProfileManagementResult
                        {
                            Success = false,
                            Message = "An error occurred during password change. Please try again."
                        };
                    }
                }
                else
                {
                    // Fallback: return URL for external browser (old behavior)
                    return new ProfileManagementResult
                    {
                        Success = true,
                        RedirectUrl = changePasswordUrl
                    };
                }
            }
        }
        
        return new ProfileManagementResult
        {
            Success = false,
            Message = "Password change is not available for this authentication provider."
        };
    }

    public Task<ProfileManagementResult> GetProfileUpdateUrlAsync()
    {
        var providerType = _dynamicConfig.GetProviderType();
        
        if (providerType?.ToLower() == "cognito")
        {
            return Task.FromResult(new ProfileManagementResult
            {
                Success = false,
                Message = "Profile updates require custom implementation using Cognito APIs. " +
                         "For now, you can update your profile through the AWS Cognito console or contact support."
            });
        }
        
        return Task.FromResult(new ProfileManagementResult
        {
            Success = false,
            Message = "Profile update is not available for this authentication provider."
        });
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
                var redirectUri = Uri.EscapeDataString("awsloginmaui://auth-callback");
                var resetUrl = $"{cognitoDomain}/forgotPassword?client_id={clientId}&response_type=code&redirect_uri={redirectUri}";
                
                if (_webViewProvider != null)
                {
                    try
                    {
                        _logger.LogInformation("Opening password reset in WebView: {Url}", resetUrl);
                        
                        // Use the dedicated password operation method for proper UI
                        var result = await _webViewProvider.HandlePasswordOperationAsync(resetUrl, "awsloginmaui://", "Password Reset");
                        
                        if (!string.IsNullOrEmpty(result))
                        {
                            _logger.LogInformation("Password reset flow completed with callback: {Result}", result);
                            return new ProfileManagementResult
                            {
                                Success = true,
                                Message = "Password reset flow completed. Please check your email for further instructions."
                            };
                        }
                        else
                        {
                            _logger.LogInformation("Password reset flow was cancelled by user");
                            return new ProfileManagementResult
                            {
                                Success = false,
                                Message = "Password reset was cancelled."
                            };
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error during password reset flow");
                        return new ProfileManagementResult
                        {
                            Success = false,
                            Message = "An error occurred during password reset. Please try again."
                        };
                    }
                }
                else
                {
                    // Fallback: return URL for external browser (old behavior)
                    return new ProfileManagementResult
                    {
                        Success = true,
                        RedirectUrl = resetUrl
                    };
                }
            }
        }
        
        return new ProfileManagementResult
        {
            Success = false,
            Message = "Password reset is not available for this authentication provider."
        };
    }
}