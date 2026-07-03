namespace LazyMagic.OIDC.Bff;

/// <summary>
/// One configured BFF pool instance — e.g. tenantauth mounted at <c>/bff</c>, or consumerauth
/// at <c>/cbff</c>. Per-instance: <see cref="Options"/> (route/cookie/authority/client/session
/// table/authname), <see cref="Store"/> (its session table) and <see cref="Tokens"/> (its OIDC
/// authority/client). The cookie codecs are SHARED across instances (the DP purpose is shared —
/// safe, because the apphost validates the token issuer/pool downstream, so a cookie presented to
/// the wrong instance just fails JWT validation with 401, never escalates).
/// </summary>
public sealed class BffInstance
{
    public required BffOptions Options { get; init; }
    public required IBffSessionStore Store { get; init; }
    public required IBffTokenClient Tokens { get; init; }
    public required IBffCookieCodec Cookie { get; init; }
    public required IBffTransactionCodec Txn { get; init; }

    /// <summary>Instance key / marker = the route-prefix token (e.g. "bff", "cbff").</summary>
    public string Key => Options.InstanceKey;
}

/// <summary>
/// Holds the configured BFF instances. <see cref="Default"/> (index 0) is tenantauth (<c>/bff</c>) —
/// the instance used when no <c>lz-bff-pool</c> marker is present, so legacy single-pool behavior is
/// preserved. Built once at startup from <c>LZ_BFF_*</c> (+ <c>LZ_CBFF_*</c> when present).
/// </summary>
public sealed class BffRegistry
{
    public IReadOnlyList<BffInstance> Instances { get; }

    /// <summary>The default (first) instance — tenantauth/<c>/bff</c>. Used when no marker is supplied.</summary>
    public BffInstance Default => Instances[0];

    public BffRegistry(IReadOnlyList<BffInstance> instances)
    {
        if (instances is null || instances.Count == 0)
            throw new ArgumentException("BffRegistry requires at least one instance.", nameof(instances));
        Instances = instances;
    }

    /// <summary>Resolve by instance key (route token, e.g. "cbff" or the lz-bff-pool marker); falls back to <see cref="Default"/>.</summary>
    public BffInstance ResolveByKey(string? key)
    {
        if (!string.IsNullOrWhiteSpace(key))
        {
            foreach (var i in Instances)
                if (string.Equals(i.Key, key, StringComparison.OrdinalIgnoreCase))
                    return i;
        }
        return Default;
    }
}
