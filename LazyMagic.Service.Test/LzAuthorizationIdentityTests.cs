using System.Security.Claims;
using System.Text;
using LazyMagic.Service.Authorization;
using Microsoft.AspNetCore.Http;

namespace LazyMagic.Service.Test;

/// <summary>
/// Pins where <see cref="LzAuthorization.GetUserInfo(HttpRequest)"/> takes the caller's identity
/// from: the VALIDATED principal in HttpContext.User, and nothing else (security note M0-1).
///
/// It used to parse the raw Authorization / lz-config-identity header with ReadJwtToken, which checks
/// no signature, so an unsigned alg:none token with any sub became the caller. These tests send
/// exactly that token and require it to be refused, and require the validated principal to win
/// over a header that disagrees with it.
/// </summary>
public class LzAuthorizationIdentityTests
{
    private sealed class TestAuthorization : LzAuthorization
    {
        public TestAuthorization(bool authenticate = true) => this.authenticate = authenticate;
    }

    /// <summary>An unsigned token: base64url(header) . base64url(payload) . (empty signature)</summary>
    private static string ForgedToken(string sub) =>
        $"{B64Url("{\"alg\":\"none\",\"typ\":\"JWT\"}")}.{B64Url($"{{\"sub\":\"{sub}\",\"username\":\"{sub}\",\"cognito:groups\":[\"admin\"]}}")}.";

    private static string B64Url(string s) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(s)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static HttpRequest Request(ClaimsPrincipal? user = null, string? authorization = null, string? configIdentity = null)
    {
        var context = new DefaultHttpContext();
        if (user is not null) context.User = user;
        if (authorization is not null) context.Request.Headers["Authorization"] = authorization;
        if (configIdentity is not null) context.Request.Headers["lz-config-identity"] = configIdentity;
        return context.Request;
    }

    /// <summary>What JwtBearer leaves in HttpContext.User after it validates a token.</summary>
    private static ClaimsPrincipal Validated(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, authenticationType: "Bearer"));

    [Fact]
    public void ForgedBearerToken_OnAnUnauthenticatedRequest_IsRefused()
    {
        var request = Request(authorization: $"Bearer {ForgedToken("victim")}");

        var ex = Assert.Throws<Exception>(() => new TestAuthorization().GetUserInfo(request));
        Assert.Contains("not authenticated", ex.Message);
    }

    [Fact]
    public void ForgedConfigIdentityHeader_OnAnUnauthenticatedRequest_IsRefused()
    {
        var request = Request(authorization: "AWS4-HMAC-SHA256 Credential=x", configIdentity: ForgedToken("victim"));

        Assert.Throws<Exception>(() => new TestAuthorization().GetUserInfo(request));
    }

    [Fact]
    public void ValidatedPrincipal_WinsOverAHeaderThatDisagrees()
    {
        var request = Request(
            Validated(new Claim("sub", "alice"), new Claim("username", "alice-name")),
            authorization: $"Bearer {ForgedToken("victim")}");

        var (lzUserId, userName) = new TestAuthorization().GetUserInfo(request);

        Assert.Equal("alice", lzUserId);
        Assert.Equal("alice-name", userName);
    }

    [Fact]
    public void SubRemappedToNameIdentifier_ByJwtBearersInboundClaimMap_IsStillRead()
    {
        var request = Request(Validated(
            new Claim(ClaimTypes.NameIdentifier, "bob"),
            new Claim("cognito:username", "bob-name")));

        var (lzUserId, userName) = new TestAuthorization().GetUserInfo(request);

        Assert.Equal("bob", lzUserId);
        Assert.Equal("bob-name", userName);
    }

    [Fact]
    public void ValidatedPrincipal_WithNoSub_IsRefused()
    {
        var request = Request(Validated(new Claim("username", "nobody")));

        Assert.Throws<Exception>(() => new TestAuthorization().GetUserInfo(request));
    }

    [Fact]
    public void UnauthenticatedModule_ReturnsNoIdentity_WithoutAPrincipal()
    {
        // A public module (authenticate = false) never asks who the caller is.
        var request = Request(authorization: $"Bearer {ForgedToken("victim")}");

        Assert.Equal(("", ""), new TestAuthorization(authenticate: false).GetUserInfo(request));
    }
}
