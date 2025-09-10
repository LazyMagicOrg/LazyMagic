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
            
            // Try to disable automatic discovery and metadata loading that might cause delays
            try
            {
                // Set metadata URL to prevent discovery document loading during placeholder phase
                options.ProviderOptions.MetadataUrl = "https://placeholder.authority/.well-known/openid-configuration";
                
                // Additional OIDC client configuration to prevent network delays
                options.ProviderOptions.AdditionalProviderParameters.Add("loadUserInfo", "false");
                options.ProviderOptions.AdditionalProviderParameters.Add("automaticSilentRenew", "false");
                options.ProviderOptions.AdditionalProviderParameters.Add("includeIdTokenInSilentRenew", "false");
                
                // Try to disable metadata discovery entirely during initialization
                options.ProviderOptions.AdditionalProviderParameters.Add("skipDiscoveryDocumentValidation", "true");
                options.ProviderOptions.AdditionalProviderParameters.Add("disableMetadataAutoRefresh", "true");
                
            }
            catch (Exception configEx)
            {
            }
            
        });

        return builder;
    }

}