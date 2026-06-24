namespace LazyMagic.OIDC.Bff;

/// <summary>
/// Identity-provider flavor the BFF talks to. Drives branching in token-client
/// (logout URL shape, scope defaults) and claim mapping (groups -> roles).
/// </summary>
public enum BffProvider
{
    /// <summary>AWS Cognito user pool. Logout via <c>/logout?client_id&amp;logout_uri</c>; roles in <c>cognito:groups</c>.</summary>
    Cognito = 0,

    /// <summary>Keycloak realm. Logout via discovery <c>end_session_endpoint</c>; roles in <c>realm_access.roles</c>.</summary>
    Keycloak = 1,
}
