using LazyMagic.OIDC.Base;

namespace LazyMagic.OIDC.WASM.Bff;

/// <summary>
/// BFF-mode <see cref="IRememberMeService"/> — a no-op by design.
///
/// In BFF mode the browser holds NO tokens (only the HttpOnly <c>__bff</c> session
/// cookie); the access/refresh tokens live server-side and the session lifetime is
/// governed by the cookie's absolute TTL + the server-side refresh. There is nothing
/// to persist, move between storages, or clear client-side — the SPA's
/// <c>BlazorRememberMeService</c> (which shuffles <c>oidc.user:</c> tokens between
/// local/session storage) has no analog here. "Remember me" maps to the server
/// session TTL. The shared <c>LoginDisplay</c> injects this, so it must be registered.
/// </summary>
public sealed class BffRememberMeService : IRememberMeService
{
    public Task<bool> GetRememberMeAsync() => Task.FromResult(false);
    public Task SetRememberMeAsync(bool rememberMe) => Task.CompletedTask;
    public Task ClearTokensAsync() => Task.CompletedTask;
    public Task<bool> HasTokensAsync() => Task.FromResult(false);
    public Task InitializeAuthenticationAsync() => Task.CompletedTask;
    public Task<string?> GetIdTokenAsync() => Task.FromResult<string?>(null);
}
