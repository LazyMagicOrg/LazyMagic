namespace LazyMagic.OIDC.Bff;

/// <summary>
/// Server-side store for the refresh token + revocation state (§8.4). Read only on
/// the refresh path (≈ per access-token TTL), never on the authenticated hot path.
/// </summary>
public interface IBffSessionStore
{
    /// <summary>Create a new session row (PutItem). Sets revoked=false and the TTL.</summary>
    Task CreateAsync(BffSessionRecord record, CancellationToken ct = default);

    /// <summary>Load a session by sid, or null if absent/expired. Honors the revoked flag (returns the row; caller checks).</summary>
    Task<BffSessionRecord?> GetAsync(string sid, CancellationToken ct = default);

    /// <summary>
    /// Try to acquire the refresh lock via a conditional UpdateItem:
    /// <c>attribute_not_exists(lockUntil) OR lockUntil &lt; :now</c>, setting
    /// <c>lockUntil = now + lockSeconds</c> and a unique fencing <c>lockToken</c>.
    /// Returns the fencing token if this caller won the lock, or <c>null</c> otherwise.
    /// </summary>
    Task<string?> TryAcquireRefreshLockAsync(string sid, int lockSeconds, CancellationToken ct = default);

    /// <summary>
    /// Store the rotated refresh token AND publish the freshly minted access token (+ its
    /// expiry) so concurrent siblings can adopt it without re-rotating, then clear the lock —
    /// all in ONE conditional UpdateItem that requires <paramref name="lockToken"/> to still
    /// be the live fencing token (lock-fencing). Throws if the condition fails (lost/expired lock).
    /// </summary>
    Task UpdateRefreshAsync(
        string sid,
        string newRefreshToken,
        string accessToken,
        long accessTokenExp,
        string lockToken,
        CancellationToken ct = default);

    /// <summary>Delete the session row (revocation, effective within the access-token TTL).</summary>
    Task DeleteAsync(string sid, CancellationToken ct = default);
}
