using System.Net.Http;
using Amazon;
using Amazon.DynamoDBv2;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace LazyMagic.OIDC.Bff;

/// <summary>
/// Registration entry point for the LazyMagic BFF. Wires options, AWS clients, Data
/// Protection (SSM-persisted key ring), the cookie codec / session store / token client,
/// the MVC ApplicationPart for <see cref="BffAuthController"/>, and the IStartupFilter
/// that fronts the pipeline with the cookie→Bearer bridge.
/// </summary>
public static class BffServiceCollectionExtensions
{
    /// <summary>Register all BFF services. Only call when LZ_BFF_ENABLED is true.</summary>
    public static IServiceCollection AddLazyMagicBff(this IServiceCollection services, IConfiguration configuration)
    {
        var options = BffOptions.FromConfiguration(configuration);

        // Strongly-typed options.
        services.AddSingleton(options);
        services.Configure<BffOptions>(o => CopyOptions(options, o));

        // AWS region resolution (explicit option, else ambient SDK default).
        RegionEndpoint? region = null;
        if (!string.IsNullOrWhiteSpace(options.AwsRegion))
            region = RegionEndpoint.GetBySystemName(options.AwsRegion);

        // DynamoDB client (session store).
        services.TryAddSingleton<IAmazonDynamoDB>(_ =>
            region is null ? new AmazonDynamoDBClient() : new AmazonDynamoDBClient(region));

        // Data Protection — SSM-persisted key ring so keys survive Lambda cold starts and
        // are shared across all tasks (§8.4). SetApplicationName MUST match across instances.
        // Use a bootstrap logger (the host's logging isn't built yet at ConfigureServices time).
        using var bootstrapLoggerFactory = LoggerFactory.Create(b => b.AddConsole());
        var dpLogger = bootstrapLoggerFactory.CreateLogger("LazyMagic.OIDC.Bff.DataProtection");

        var isDevelopment = IsDevelopmentEnvironment();
        var dp = services.AddDataProtection();

        if (!string.IsNullOrWhiteSpace(options.DataProtectionSsmParam))
        {
            dp.PersistKeysToAWSSystemsManager(options.DataProtectionSsmParam!);
            dpLogger.LogInformation(
                "BFF Data Protection: SSM-persisted key ring active ({Param}).",
                options.DataProtectionSsmParam);
        }
        else
        {
            // No persisted key ring → ephemeral, per-instance keys. This is BROKEN for
            // Lambda/multi-task: cookies minted on one instance can't be decrypted on another,
            // and cold starts invalidate all sessions. Fail fast outside Development.
            const string msg = "BFF Data Protection key ring is in-memory/ephemeral because " +
                "LZ_BFF_DP_PARAM (DataProtectionSsmParam) is unset. This is only acceptable for a " +
                "single dev task; for Lambda/multi-task it silently invalidates sessions. Set LZ_BFF_DP_PARAM.";
            if (isDevelopment)
            {
                dpLogger.LogWarning(msg);
            }
            else
            {
                dpLogger.LogError(msg);
                throw new InvalidOperationException(msg);
            }
        }

        // Always set a deterministic application name so a future persisted ring stays consistent.
        dp.SetApplicationName(
            !string.IsNullOrWhiteSpace(options.DataProtectionAppName)
                ? options.DataProtectionAppName!
                : "LazyMagic.OIDC.Bff");

        // BFF building blocks.
        services.TryAddSingleton<IBffCookieCodec, BffCookieCodec>();
        services.TryAddSingleton<IBffTransactionCodec, BffTransactionCodec>();
        services.TryAddSingleton<IBffSessionStore, DynamoBffSessionStore>();

        // Token client uses a typed HttpClient for discovery/JWKS/token calls.
        services.AddHttpClient<IBffTokenClient, BffTokenClient>();

        // Cookie auth scheme (present so [Authorize] flows / sign-in plumbing can resolve a
        // scheme if the host opts to use it; the cookie itself is codec-managed on the hot path).
        services.AddAuthentication()
            .AddCookie(BffConstants.CookieScheme, o =>
            {
                o.Cookie.Name = options.CookieName;
                o.Cookie.HttpOnly = true;
                o.Cookie.SecurePolicy = Microsoft.AspNetCore.Http.CookieSecurePolicy.Always;
                o.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax;
                if (!string.IsNullOrWhiteSpace(options.CookieDomain))
                    o.Cookie.Domain = options.CookieDomain;
            });

        // Ensure the controller is discoverable even if the host doesn't auto-scan this assembly.
        services.AddControllers()
            .AddApplicationPart(typeof(BffAuthController).Assembly);

        // Insert the cookie->Bearer middleware at the FRONT of the host pipeline.
        services.AddSingleton<Microsoft.AspNetCore.Hosting.IStartupFilter, BffStartupFilter>();

        return services;
    }

    /// <summary>
    /// True when the host is running in the Development environment, read from the standard
    /// ASP.NET Core / .NET environment variables. Resolved at ConfigureServices time (before
    /// the host — and thus <c>IHostEnvironment</c> — is fully built), so we read the env var
    /// directly rather than resolving the service.
    /// </summary>
    private static bool IsDevelopmentEnvironment()
    {
        var envName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
                      ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
        return string.Equals(envName, "Development", StringComparison.OrdinalIgnoreCase);
    }

    private static void CopyOptions(BffOptions src, BffOptions dest)
    {
        dest.Enabled = src.Enabled;
        dest.Provider = src.Provider;
        dest.Authority = src.Authority;
        dest.MetadataUrl = src.MetadataUrl;
        dest.ClientId = src.ClientId;
        dest.ClientSecret = src.ClientSecret;
        dest.Scopes = src.Scopes;
        dest.CallbackPath = src.CallbackPath;
        dest.CookieName = src.CookieName;
        dest.CookieDomain = src.CookieDomain;
        dest.AccessTokenSkewSeconds = src.AccessTokenSkewSeconds;
        dest.SessionTableName = src.SessionTableName;
        dest.SessionTtlHours = src.SessionTtlHours;
        dest.DataProtectionSsmParam = src.DataProtectionSsmParam;
        dest.DataProtectionAppName = src.DataProtectionAppName;
        dest.AwsRegion = src.AwsRegion;
        dest.AuthName = src.AuthName;
    }
}
