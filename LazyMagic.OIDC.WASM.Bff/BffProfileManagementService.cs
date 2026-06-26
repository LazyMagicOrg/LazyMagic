using LazyMagic.OIDC.Base;

namespace LazyMagic.OIDC.WASM.Bff;

/// <summary>
/// BFF-mode <see cref="IProfileManagementService"/>.
///
/// In BFF mode the browser does NOT hold the OIDC client config (the server owns the
/// confidential client), so the SPA's <c>BlazorProfileManagementService</c> — which
/// builds Cognito <c>/forgotPassword</c> URLs from the client-side dynamic config —
/// does not apply. Password/profile management in BFF mode is a server concern (a
/// future <c>/bff/*</c> endpoint or the IdP's hosted UI). For now these return a
/// "not available in-app" result so the shared <c>LoginDisplay</c> resolves the
/// dependency and renders. (Registered by <c>AddLazyMagicOIDCWASMBff</c>.)
/// </summary>
public sealed class BffProfileManagementService : IProfileManagementService
{
    private static Task<ProfileManagementResult> NotAvailable(string what) =>
        Task.FromResult(new ProfileManagementResult
        {
            Success = false,
            Message = $"{what} is managed by your identity provider and is not available in-app in BFF mode yet.",
        });

    public Task<ProfileManagementResult> GetPasswordChangeUrlAsync() => NotAvailable("Password change");
    public Task<ProfileManagementResult> GetProfileUpdateUrlAsync() => NotAvailable("Profile update");
    public Task<ProfileManagementResult> GetPasswordResetUrlAsync() => NotAvailable("Password reset");
}
