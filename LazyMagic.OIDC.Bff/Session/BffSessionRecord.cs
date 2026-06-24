namespace LazyMagic.OIDC.Bff;

/// <summary>
/// Server-side session row (the "split" half that never touches the browser).
/// Per MultiTenantAuth.md §8.11:
/// <code>
/// id="SESSION#&lt;sid&gt;", sk="SESSION",
///   rt=&lt;refresh_token&gt;, revoked=false, lockUntil=&lt;epoch|absent&gt;,
///   sub, tenant, pool, TTL=&lt;epoch&gt;
/// </code>
/// </summary>
public sealed class BffSessionRecord
{
    /// <summary>Opaque session id (CSPRNG, base64url). The cookie carries this, never the refresh token.</summary>
    public string Sid { get; set; } = string.Empty;

    /// <summary>Refresh token (durable credential). Server-side only.</summary>
    public string RefreshToken { get; set; } = string.Empty;

    /// <summary>Revocation flag. Logout / panic sets this (or deletes the row).</summary>
    public bool Revoked { get; set; }

    /// <summary>Epoch seconds until which a refresh lock is held; absent when unlocked.</summary>
    public long? LockUntil { get; set; }

    /// <summary>
    /// Fencing token (unique per lock acquisition). The winner writes it on acquire and
    /// conditions its rt/at persistence on it still being present, so a merely-expired lock
    /// cannot be mistaken for a completed refresh and a stale winner cannot clobber a newer
    /// rotation. Absent when unlocked.
    /// </summary>
    public string? LockToken { get; set; }

    /// <summary>
    /// The freshly minted access token published by the lock winner so concurrent siblings
    /// can adopt it WITHOUT re-rotating the refresh token (avoids double-rotation). Server-side only.
    /// </summary>
    public string? AccessToken { get; set; }

    /// <summary>Expiry (epoch seconds) of <see cref="AccessToken"/>; absent when none published.</summary>
    public long? AccessTokenExp { get; set; }

    /// <summary>Subject (user id) from the id_token.</summary>
    public string? Sub { get; set; }

    /// <summary>Tenant key the session belongs to.</summary>
    public string? Tenant { get; set; }

    /// <summary>Pool key (tenantauth/consumerauth/etc.) the session authenticated against.</summary>
    public string? Pool { get; set; }

    /// <summary>Absolute expiry epoch seconds — mirrored into the DynamoDB TTL attribute.</summary>
    public long Ttl { get; set; }
}
