namespace LazyMagic.OIDC.Base;

public class ProfileManagementResult
{
    public bool Success { get; set; }
    public string? RedirectUrl { get; set; }
    public string? Message { get; set; }
}

public interface IProfileManagementService
{
    Task<ProfileManagementResult> GetPasswordChangeUrlAsync();
    Task<ProfileManagementResult> GetProfileUpdateUrlAsync();
    Task<ProfileManagementResult> GetPasswordResetUrlAsync();
}