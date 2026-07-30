using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LazyMagic.OIDC.Bff;

/// <summary>
/// Origin-verification gate for hosts fronted by a CDN with a PUBLIC origin.
///
/// WHY: the lambda-cognito-dynamodb topology exposes the AppHost through a Lambda
/// Function URL. CloudFront's OAC (SigV4) signing for Function URL origins cannot
/// carry REST writes — PUT/PATCH/DELETE are rejected outright and POST requires the
/// VIEWER to send x-amz-content-sha256 (verified empirically 2026-07-03; see
/// Platform/LambdaTopology.md). The workable pattern is the standard "origin verify"
/// header: the Function URL is public (AuthType=NONE), CloudFront injects a secret
/// custom header on the api origin, and this middleware rejects any request that
/// does not carry it — so direct-to-origin calls are refused while every verb/body
/// combination works through the CDN.
///
/// INERT BY DEFAULT: active only when <c>LZ_ORIGIN_VERIFY</c> is set (the deployer
/// sets it on the function and on the CloudFront origin). /health is exempt — the
/// Lambda Web Adapter readiness probe calls it from inside the container, without
/// CloudFront in the path.
/// </summary>
public sealed class OriginVerifyMiddleware
{
    /// <summary>Env/config key holding the shared secret.</summary>
    public const string ConfigKey = "LZ_ORIGIN_VERIFY";

    /// <summary>Header CloudFront injects on the api origin (origin custom header).</summary>
    public const string HeaderName = "x-origin-verify";

    /// <summary>
    /// Response header echoed on the exempt /health path: "true"/"false" for whether
    /// the REQUEST carried a matching secret. Lets an edge probe (`lz verify`
    /// api-health) prove the CloudFront-injected header actually matches
    /// LZ_ORIGIN_VERIFY — without it, edge/origin secret drift is invisible: /health
    /// is the one path the gate never rejects, yet it is the only unauthenticated
    /// path a smoke check can safely exercise, so drift would 403 every real route
    /// while the probe stayed green. Leaks only a boolean the reject path already
    /// reveals on every other route.
    /// </summary>
    public const string VerifiedHeaderName = "x-origin-verified";

    private readonly RequestDelegate _next;
    private readonly byte[] _secretUtf8;
    private readonly ILogger<OriginVerifyMiddleware> _logger;

    public OriginVerifyMiddleware(RequestDelegate next, IConfiguration config, ILogger<OriginVerifyMiddleware> logger)
    {
        _next = next;
        _logger = logger;
        _secretUtf8 = System.Text.Encoding.UTF8.GetBytes(config[ConfigKey] ?? string.Empty);
    }

    public static bool IsConfigured(IConfiguration config) => !string.IsNullOrEmpty(config[ConfigKey]);

    public async Task InvokeAsync(HttpContext context)
    {
        var presented = context.Request.Headers[HeaderName].FirstOrDefault() ?? string.Empty;
        var presentedUtf8 = System.Text.Encoding.UTF8.GetBytes(presented);
        var verified = _secretUtf8.Length > 0 &&
            System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(presentedUtf8, _secretUtf8);

        // LWA readiness / infra probes hit the origin directly (no CloudFront hop),
        // so /health passes without the header — but the verdict is echoed (see
        // VerifiedHeaderName) so the edge smoke probe can assert the accept path.
        if (context.Request.Path.StartsWithSegments("/health"))
        {
            context.Response.Headers[VerifiedHeaderName] = verified ? "true" : "false";
            await _next(context).ConfigureAwait(false);
            return;
        }

        if (!verified)
        {
            _logger.LogWarning("origin-verify rejected {Method} {Path} (header {State})",
                context.Request.Method, context.Request.Path,
                presented.Length == 0 ? "missing" : "mismatch");
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new { error = "origin_verification_failed" }).ConfigureAwait(false);
            return;
        }

        await _next(context).ConfigureAwait(false);
    }
}

/// <summary>
/// Inserts <see cref="OriginVerifyMiddleware"/> at the FRONT of the pipeline (before
/// routing/CORS/auth) so unverified traffic is refused before touching anything else.
/// Registered by <see cref="BffHostingStartup"/> when LZ_ORIGIN_VERIFY is configured.
/// </summary>
public sealed class OriginVerifyStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return app =>
        {
            app.UseMiddleware<OriginVerifyMiddleware>();
            next(app);
        };
    }
}
