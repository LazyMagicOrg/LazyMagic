using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using LazyMagic.OIDC.WASM.Services;

namespace LazyMagic.OIDC.WASM;

/// <summary>
/// WebAssembly-specific extensions for dynamic OIDC configuration
/// </summary>
public static class ConfigureBlazorServices
{
    /// <summary>
    /// Adds dynamic OIDC authentication with truly lazy-loaded configuration
    /// Configuration is NOT loaded during startup - it's deferred until authentication is actually needed
    /// This allows for faster app startup and proper error handling
    /// </summary>
    /// <returns>The builder for chaining</returns>
    public static WebAssemblyHostBuilder AddLazyMagicOIDCWASMBuilder(
        this WebAssemblyHostBuilder builder)
    {
        // Configure OIDC authentication with minimal placeholder config
        // All real configuration comes from dynamic loading
        builder.Services.AddOidcAuthentication(options =>
        {
            var provider = builder.Services.BuildServiceProvider();
            var lzHost = provider.GetService<ILzHost>();
            // Note: This will be a placeholder - real URL comes from dynamic config
            // We can't get the actual current URL here during service setup
            var completeAppUrl = lzHost!.AppUrl.TrimEnd('/') + lzHost.AppPath;
            
            // Placeholder values - will be overridden by dynamic config
            options.ProviderOptions.Authority = "https://placeholder.authority";
            options.ProviderOptions.ClientId = "placeholder-client-id";
            options.ProviderOptions.ResponseType = "code";
            
            // Calculate redirect URIs using complete app URL (AppUrl + AppPath)
            options.ProviderOptions.RedirectUri = $"{completeAppUrl.TrimEnd('/')}/authentication/login-callback";
            options.ProviderOptions.PostLogoutRedirectUri = completeAppUrl.TrimEnd('/');
            
            // Standard OIDC scopes
            options.ProviderOptions.DefaultScopes.Add("openid");
            options.ProviderOptions.DefaultScopes.Add("profile");
            options.ProviderOptions.DefaultScopes.Add("email");
            
            // Standard claims
            options.UserOptions.NameClaim = "name";
            options.UserOptions.RoleClaim = "cognito:groups";
            
            // Set metadata URL to prevent discovery document loading during placeholder phase
            options.ProviderOptions.MetadataUrl = "https://placeholder.authority/.well-known/openid-configuration";

            // NOTE: AdditionalProviderParameters previously held oidc-client-ts
            // SDK config flags (loadUserInfo, automaticSilentRenew,
            // includeIdTokenInSilentRenew, skipDiscoveryDocumentValidation,
            // disableMetadataAutoRefresh). That collection appends entries to
            // the QUERY STRING of every /authorize request — it is not the
            // right channel for SDK-internal config. The flags didn't take
            // effect on the SDK side, AND they polluted OAuth requests with
            // garbage params (visible as e.g. `…&automaticSilentRenew=false&
            // skipDiscoveryDocumentValidation=true` on /authorize URLs).
            // Removed. If those SDK behaviours need to change, configure them
            // through the oidc-client-ts settings object directly, or via a
            // post-init hook on the JS-side AuthenticationService.

        });

        // Add authorization services that AuthorizeView components need
        // This method is WASM-specific, so this ensures AuthorizeView works properly
        builder.Services.AddAuthorizationCore();
        
        // TEMPORARILY DISABLED - debugging race condition in login callback
        // Replace the AuthenticationStateProvider with our Cognito-optimized version
        // This provides fast authentication while avoiding circular dependencies
        // builder.Services.Replace(ServiceDescriptor.Scoped<AuthenticationStateProvider, CognitoRemoteAuthenticationService>());

        // Register our fast authentication service for optional use by components
        // This provides cached authentication state queries while keeping Microsoft's architecture intact
        builder.Services.TryAddScoped<IFastAuthenticationService, FastAuthenticationService>();

        return builder;
    }

}