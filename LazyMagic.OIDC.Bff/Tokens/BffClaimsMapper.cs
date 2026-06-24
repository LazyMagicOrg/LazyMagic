using System.Security.Claims;
using System.Text.Json;

namespace LazyMagic.OIDC.Bff;

/// <summary>
/// Provider-aware claim extraction for the minimal claims dict stored in the cookie
/// and surfaced by /bff/user. Cognito roles live in <c>cognito:groups</c>; Keycloak
/// roles live in <c>realm_access.roles</c> (§ provider-aware).
/// </summary>
public static class BffClaimsMapper
{
    /// <summary>
    /// Build the minimal claims dictionary (sub, name, email, roles/groups) from a
    /// validated id_token principal, branching on provider for the role source.
    /// </summary>
    public static Dictionary<string, List<string>> BuildMinimalClaims(ClaimsPrincipal principal, BffProvider provider)
    {
        var claims = new Dictionary<string, List<string>>();

        Copy(principal, claims, "sub");
        Copy(principal, claims, "name");
        Copy(principal, claims, "email");
        Copy(principal, claims, "preferred_username");

        var roles = ExtractRoles(principal, provider);
        if (roles.Count > 0)
            claims["roles"] = roles;

        return claims;
    }

    /// <summary>Extract role/group strings per provider.</summary>
    public static List<string> ExtractRoles(ClaimsPrincipal principal, BffProvider provider)
    {
        var roles = new List<string>();

        if (provider == BffProvider.Cognito)
        {
            // cognito:groups appears as repeated claims.
            roles.AddRange(principal.FindAll("cognito:groups").Select(c => c.Value));
        }
        else // Keycloak
        {
            // realm_access is a JSON object: { "roles": ["..."] }.
            var realmAccess = principal.FindFirst("realm_access")?.Value;
            if (!string.IsNullOrEmpty(realmAccess))
            {
                try
                {
                    using var doc = JsonDocument.Parse(realmAccess);
                    if (doc.RootElement.TryGetProperty("roles", out var arr) && arr.ValueKind == JsonValueKind.Array)
                        roles.AddRange(arr.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.String).Select(e => e.GetString()!));
                }
                catch (JsonException)
                {
                    // Ignore malformed realm_access; no roles.
                }
            }
        }

        return roles.Where(r => !string.IsNullOrWhiteSpace(r)).Distinct().ToList();
    }

    private static void Copy(ClaimsPrincipal principal, Dictionary<string, List<string>> dict, string type)
    {
        var v = principal.FindFirst(type)?.Value;
        if (!string.IsNullOrEmpty(v))
            dict[type] = new List<string> { v };
    }
}
