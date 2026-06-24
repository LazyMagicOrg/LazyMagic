using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace LazyMagic.OIDC.Bff;

/// <summary>
/// Inserts <see cref="BffCookieToBearerMiddleware"/> at the FRONT of the pipeline so it
/// runs before the host's routing/CORS/JWT/auth middleware. Setting Authorization +
/// lz-authname here lets the host's existing multi-scheme JWT middleware authenticate
/// the proxied call with no controller changes.
/// </summary>
public sealed class BffStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return app =>
        {
            app.UseMiddleware<BffCookieToBearerMiddleware>();
            next(app);
        };
    }
}
