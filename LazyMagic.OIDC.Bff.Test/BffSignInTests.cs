using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Net.Http.Headers;

namespace LazyMagic.OIDC.Bff.Test;

/// <summary>
/// WHO <c>/bff/user</c> SAYS IS SIGNED IN, after a sign-in walked through the real pieces: the controller, the token
/// client and its token handler, the claims mapper and both cookie codecs. Only the identity provider, the session
/// table and the key ring are stood in for.
///
/// <para>On a deployed system the answer was <c>{"isAuthenticated":true,"claims":{}}</c> for a login in no group
/// (measured 2026-10-05), so a console could greet nobody by name. The token handler renamed the id token's
/// <c>sub</c> and <c>email</c> to its own long claim types as it validated, and the mapper, which asks for them by
/// the names the token uses, found neither. Nothing here ran the two together, which is what these tests do: the id
/// token is signed, served a discovery document and a key set, and validated as it would be.</para>
///
/// <para>The id token carries the claims Cognito documents for a hosted sign-in. It was written from that
/// documentation, not captured from a pool.</para>
/// </summary>
public class BffSignInTests
{
    private const string Subject = "5b1f0c2e-8a41-4d6e-9c3b-2f7a6d1e4c90";
    private const string Address = "owner@example.invalid";

    [Fact]
    public async Task ASignedInLogin_IsNamedBySubjectAndAddress_AndByNothingElseInItsToken()
    {
        using var h = new Harness();

        var session = await h.SignInAsync();
        var user = h.User(session.Cookie);

        Assert.True(user.GetProperty("isAuthenticated").GetBoolean());
        var claims = user.GetProperty("claims");
        Assert.Equal(new[] { "email", "sub" }, claims.EnumerateObject().Select(c => c.Name).Order());
        Assert.Equal(Subject, Only(claims, "sub"));
        Assert.Equal(Address, Only(claims, "email"));
        Assert.Equal("https://match.example.test/seller/", session.ReturnedTo);
        Assert.Equal(Subject, Assert.Single(h.Sessions.Created).Sub);
    }

    [Fact]
    public async Task ALoginWithANameAndGroups_CarriesThemToo()
    {
        using var h = new Harness();
        h.Idp.Extra["name"] = "Ada Owner";
        h.Idp.Extra["preferred_username"] = "ada";
        h.Idp.Extra["cognito:groups"] = new[] { "Admin", "Support" };

        var claims = h.User((await h.SignInAsync()).Cookie).GetProperty("claims");

        Assert.Equal(
            new[] { "email", "name", "preferred_username", "roles", "sub" },
            claims.EnumerateObject().Select(c => c.Name).Order());
        Assert.Equal("Ada Owner", Only(claims, "name"));
        Assert.Equal("ada", Only(claims, "preferred_username"));
        Assert.Equal(new[] { "Admin", "Support" }, claims.GetProperty("roles").EnumerateArray().Select(r => r.GetString()));
    }

    /// <summary>
    /// The mechanism, pinned where it lives: a principal the token client hands on carries the token's own claim
    /// names, and is named by its subject - which <c>NameClaimType = "sub"</c> asked for all along and never got.
    /// </summary>
    [Fact]
    public async Task TheValidatedPrincipal_KeepsTheTokensOwnClaimNames()
    {
        using var h = new Harness();
        var nonce = "n-" + Guid.NewGuid().ToString("N");

        var validated = await h.Tokens.ValidateIdTokenAsync(h.Idp.IdToken(nonce), nonce);

        Assert.DoesNotContain(validated.Principal.Claims, claim => claim.Type.Contains("://"));
        Assert.Equal(Subject, validated.Principal.FindFirst("sub")?.Value);
        Assert.Equal(Address, validated.Principal.FindFirst("email")?.Value);
        Assert.Equal(Subject, validated.Principal.Identity?.Name);
        Assert.Equal(Subject, validated.Sub);
    }

    /// <summary>
    /// What the token client refuses, it still refuses: each id token differs from the one that signs in above in
    /// one thing, and none of them leaves a session or a cookie behind.
    /// </summary>
    [Theory]
    [InlineData("nonce")]
    [InlineData("audience")]
    [InlineData("issuer")]
    [InlineData("expired")]
    [InlineData("another key")]
    [InlineData("unsigned")]
    public async Task AnIdTokenThatIsNotThisSignIns_IsRefused(string wrong)
    {
        using var h = new Harness();
        h.Idp.Wrong = wrong;

        var session = await h.SignInAsync();

        var refused = Assert.IsType<BadRequestObjectResult>(session.Result);
        Assert.Equal("invalid_id_token", JsonSerializer.SerializeToElement(refused.Value).GetProperty("error").GetString());
        Assert.Null(session.Cookie);
        Assert.Empty(h.Sessions.Created);
    }

    private static string? Only(JsonElement claims, string name)
        => Assert.Single(claims.GetProperty(name).EnumerateArray()).GetString();

    /// <summary>What a sign-in ended with: the callback's result, and the session cookie and destination when it gave them.</summary>
    private sealed record SignIn(IActionResult Result, string? Cookie, string? ReturnedTo);

    /// <summary>
    /// One BFF instance as a deployment runs it behind a parent domain: the callback is the apex's, and a sign-in
    /// started on a subtenant host is sent back to that host.
    /// </summary>
    private sealed class Harness : IDisposable
    {
        private const string Host = "match.example.test";

        private readonly BffRegistry _registry;

        public FakeIdentityProvider Idp { get; } = new();
        public FakeSessionStore Sessions { get; } = new();
        public BffTokenClient Tokens { get; }

        public Harness()
        {
            var options = new BffOptions
            {
                Enabled = true,
                Provider = BffProvider.Cognito,
                Authority = FakeIdentityProvider.Issuer,
                ClientId = FakeIdentityProvider.ClientId,
                ClientSecret = "not-a-secret",
                CookieDomain = ".example.test",
            };
            var keys = new EphemeralDataProtectionProvider();
            Tokens = new BffTokenClient(
                new HttpClient(Idp), Microsoft.Extensions.Options.Options.Create(options), NullLogger<BffTokenClient>.Instance);
            _registry = new BffRegistry(new[]
            {
                new BffInstance
                {
                    Options = options,
                    Store = Sessions,
                    Tokens = Tokens,
                    Cookie = new BffCookieCodec(keys, NullLogger<BffCookieCodec>.Instance),
                    Txn = new BffTransactionCodec(keys, NullLogger<BffTransactionCodec>.Instance),
                },
            });
        }

        /// <summary>/bff/login, the identity provider's redirect back, and /bff/callback - each a request of its own.</summary>
        public async Task<SignIn> SignInAsync()
        {
            var (login, loginResponse) = Request();
            var toProvider = Assert.IsType<RedirectResult>(await login.Login("/seller/", CancellationToken.None));
            var asked = QueryHelpers.ParseQuery(new Uri(toProvider.Url).Query);
            Assert.Equal("https://example.test/bff/callback", asked["redirect_uri"].ToString());
            Idp.Nonce = asked["nonce"].ToString();

            var (callback, callbackResponse) = Request(("__bff_txn", SetCookie(loginResponse, "__bff_txn")!));
            var result = await callback.Callback("code-1", asked["state"].ToString(), null, CancellationToken.None);

            return new SignIn(result, SetCookie(callbackResponse, "__bff"), (result as RedirectResult)?.Url);
        }

        /// <summary>GET /bff/user with a session cookie, as the envelope a console reads.</summary>
        public JsonElement User(string? sessionCookie)
        {
            Assert.NotNull(sessionCookie);
            var (controller, _) = Request(("__bff", sessionCookie));
            return JsonSerializer.SerializeToElement(Assert.IsType<OkObjectResult>(controller.GetUser()).Value);
        }

        private (BffAuthController Controller, HttpResponse Response) Request(params (string Name, string Value)[] cookies)
        {
            var http = new DefaultHttpContext();
            http.Request.Scheme = "https";
            http.Request.Host = new HostString("internal-alb.example.internal");
            http.Request.Headers["lz-tenantid"] = Host;
            if (cookies.Length > 0)
                http.Request.Headers.Cookie = string.Join("; ", cookies.Select(c => $"{c.Name}={c.Value}"));

            var routeData = new RouteData();
            routeData.Values["bffSeg"] = "bff";
            var controller = new BffAuthController(_registry, NullLogger<BffAuthController>.Instance)
            {
                ControllerContext = new ControllerContext { HttpContext = http, RouteData = routeData },
            };
            return (controller, http.Response);
        }

        /// <summary>The value a response set a cookie to, or null when it set none or cleared it.</summary>
        private static string? SetCookie(HttpResponse response, string name)
        {
            var set = SetCookieHeaderValue.ParseList(response.Headers.SetCookie.ToArray())
                .LastOrDefault(c => c.Name == name);
            return set is null || set.Value.Length == 0 ? null : set.Value.ToString();
        }

        public void Dispose() => Idp.Dispose();
    }

    /// <summary>
    /// The identity provider's three documents: discovery, the key set, and the token endpoint's answer to a code.
    /// </summary>
    private sealed class FakeIdentityProvider : HttpMessageHandler
    {
        public const string Issuer = "https://idp.example.test/pool-1";
        public const string ClientId = "bff-client-1";
        private const string KeyId = "key-1";

        private readonly RSA _key = RSA.Create(2048);
        private readonly RSA _anotherKey = RSA.Create(2048);

        /// <summary>The nonce of the sign-in under way, which the id token must repeat.</summary>
        public string Nonce { get; set; } = string.Empty;

        /// <summary>Claims a test adds to the id token.</summary>
        public Dictionary<string, object> Extra { get; } = new();

        /// <summary>The one thing wrong with the id token the token endpoint hands out; null for none.</summary>
        public string? Wrong { get; set; }

        public string IdToken(string nonce)
        {
            var now = DateTimeOffset.UtcNow;
            var issued = Wrong == "expired" ? now.AddHours(-2) : now.AddSeconds(-5);
            var payload = new Dictionary<string, object>
            {
                ["at_hash"] = "sQ3mZ0fUu0xq0dGmVhL0Vw",
                ["sub"] = Subject,
                ["email_verified"] = true,
                ["iss"] = Wrong == "issuer" ? "https://idp.example.test/pool-2" : Issuer,
                ["cognito:username"] = Subject,
                ["nonce"] = Wrong == "nonce" ? "somebody-elses-nonce" : nonce,
                ["origin_jti"] = "0c6a5f1e-0d0b-4f0c-8d5e-7a8b9c0d1e2f",
                ["aud"] = Wrong == "audience" ? "another-client" : ClientId,
                ["event_id"] = "7d3c2b1a-4e5f-4a6b-8c7d-9e0f1a2b3c4d",
                ["token_use"] = "id",
                ["auth_time"] = issued.ToUnixTimeSeconds(),
                ["exp"] = issued.AddHours(1).ToUnixTimeSeconds(),
                ["iat"] = issued.ToUnixTimeSeconds(),
                ["jti"] = "1a2b3c4d-5e6f-4a7b-8c9d-0e1f2a3b4c5d",
                ["email"] = Address,
            };
            foreach (var (name, value) in Extra)
                payload[name] = value;

            var handler = new JsonWebTokenHandler { SetDefaultTimesOnTokenCreation = false };
            var json = JsonSerializer.Serialize(payload);
            if (Wrong == "unsigned")
                return handler.CreateToken(json);

            var key = new RsaSecurityKey(Wrong == "another key" ? _anotherKey : _key) { KeyId = KeyId };
            return handler.CreateToken(json, new SigningCredentials(key, SecurityAlgorithms.RsaSha256));
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri!.AbsoluteUri;
            if (url == Issuer + "/.well-known/openid-configuration")
                return Json(new
                {
                    issuer = Issuer,
                    authorization_endpoint = "https://signin.example.test/oauth2/authorize",
                    token_endpoint = "https://signin.example.test/oauth2/token",
                    jwks_uri = Issuer + "/.well-known/jwks.json",
                });

            if (url == Issuer + "/.well-known/jwks.json")
            {
                var key = _key.ExportParameters(false);
                return Json(new
                {
                    keys = new[]
                    {
                        new
                        {
                            kty = "RSA", use = "sig", alg = "RS256", kid = KeyId,
                            n = Base64UrlEncoder.Encode(key.Modulus), e = Base64UrlEncoder.Encode(key.Exponent),
                        },
                    },
                });
            }

            if (url == "https://signin.example.test/oauth2/token" && request.Method == HttpMethod.Post)
                return Json(new
                {
                    access_token = "access-token-1",
                    id_token = IdToken(Nonce),
                    refresh_token = "refresh-token-1",
                    expires_in = 3600,
                    token_type = "Bearer",
                });

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }

        private static Task<HttpResponseMessage> Json(object body)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
            });

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _key.Dispose();
                _anotherKey.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>The session table: it keeps what a sign-in created, and a sign-in asks it for nothing else.</summary>
    private sealed class FakeSessionStore : IBffSessionStore
    {
        public List<BffSessionRecord> Created { get; } = new();

        public Task CreateAsync(BffSessionRecord record, CancellationToken ct = default)
        {
            Created.Add(record);
            return Task.CompletedTask;
        }

        public Task<BffSessionRecord?> GetAsync(string sid, CancellationToken ct = default)
            => Task.FromResult(Created.FirstOrDefault(r => r.Sid == sid));

        public Task<string?> TryAcquireRefreshLockAsync(string sid, int lockSeconds, CancellationToken ct = default)
            => throw new NotSupportedException("a sign-in refreshes nothing");

        public Task UpdateRefreshAsync(
            string sid, string newRefreshToken, string accessToken, long accessTokenExp, string lockToken, CancellationToken ct = default)
            => throw new NotSupportedException("a sign-in refreshes nothing");

        public Task DeleteAsync(string sid, CancellationToken ct = default)
        {
            Created.RemoveAll(r => r.Sid == sid);
            return Task.CompletedTask;
        }
    }
}
