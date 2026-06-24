using Microsoft.AspNetCore.Hosting;

[assembly: HostingStartup(typeof(LazyMagic.OIDC.Bff.BffHostingStartup))]

namespace LazyMagic.OIDC.Bff;

/// <summary>
/// IHostingStartup entry point. The generated host (which we cannot edit) loads this
/// when <c>ASPNETCORE_HOSTINGSTARTUPASSEMBLIES=LazyMagic.OIDC.Bff</c> is set.
///
/// INERT BY DEFAULT: services are only added when <c>LZ_BFF_ENABLED=true</c>, so merely
/// referencing the package never changes existing host behavior.
/// </summary>
public sealed class BffHostingStartup : IHostingStartup
{
    public void Configure(IWebHostBuilder builder)
    {
        builder.ConfigureServices((ctx, services) =>
        {
            if (BffOptions.IsEnabled(ctx.Configuration))
            {
                services.AddLazyMagicBff(ctx.Configuration);
            }
            // else: no-op. The package is fully inert.
        });
    }
}
