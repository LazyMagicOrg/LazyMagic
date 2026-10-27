using System.Net;
using System.Text;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging.Abstractions;

namespace LazyMagic.OIDC.WASM.Bff.Test;

/// <summary>
/// What <see cref="BffOIDCService.LogoutAsync"/> asks the BFF for (2026-10-26): the configured destination, and - for a
/// client that sends its signed-out users to the system's front door - <c>apex=true</c>, so the BFF leaves them on the
/// apex rather than the subtenant host they logged out from. Then it goes where the BFF says.
/// </summary>
public class BffOIDCServiceLogoutTests
{
    [Fact]
    public async Task AClientSentToTheApex_AsksTheBffForTheApex()
    {
        var (asked, wentTo) = await LogOutAsync(postLogoutRedirectPath: "/", toApex: true);

        Assert.Equal("/bff/logout?returnUrl=%2F&apex=true", asked);
        Assert.Equal("https://idp.example/logout", wentTo);
    }

    [Fact]
    public async Task ByDefault_TheBffIsAskedForThePageLoggedOutFrom_AndNotTheApex()
    {
        var (asked, _) = await LogOutAsync(postLogoutRedirectPath: null, toApex: false);

        Assert.Equal("/bff/logout?returnUrl=%2Fseller%2Fleads", asked);
    }

    private static async Task<(string Asked, string WentTo)> LogOutAsync(string? postLogoutRedirectPath, bool toApex)
    {
        var server = new FakeLogoutServer();
        var http = new HttpClient(server) { BaseAddress = new Uri("https://match.example/seller/") };
        var navigation = new FakeNavigation("https://match.example/seller/leads");
        var service = new BffOIDCService(new Anonymous(), navigation, http, NullLogger<BffOIDCService>.Instance,
            "/bff", postLogoutRedirectPath, toApex);

        await service.LogoutAsync();

        return (server.Asked!, navigation.WentTo!);
    }

    private sealed class FakeLogoutServer : HttpMessageHandler
    {
        public string? Asked;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Asked = request.RequestUri!.PathAndQuery;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"logoutUrl":"https://idp.example/logout"}""", Encoding.UTF8, "application/json"),
            });
        }
    }

    private sealed class FakeNavigation : NavigationManager
    {
        public string? WentTo;

        public FakeNavigation(string uri) => Initialize("https://match.example/", uri);

        protected override void NavigateToCore(string uri, bool forceLoad) => WentTo = uri;
    }

    private sealed class Anonymous : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync()
            => Task.FromResult(new AuthenticationState(new System.Security.Claims.ClaimsPrincipal()));
    }
}
