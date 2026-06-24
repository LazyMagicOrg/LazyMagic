using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LazyMagic.OIDC.Bff;

/// <summary>
/// DynamoDB-backed <see cref="IBffSessionStore"/>. Record shape per §8.11
/// (<c>id="SESSION#{sid}", sk="SESSION"</c>). Uses a conditional UpdateItem for the
/// refresh lock (§8.10/§8.12) so exactly one concurrent request performs the refresh.
/// </summary>
public sealed class DynamoBffSessionStore : IBffSessionStore
{
    // Reuses the existing tenant table key schema: partition key "id", sort key "sk".
    private const string PkName = "id";
    private const string SkName = "sk";
    private const string SkValue = "SESSION";

    private const string AttrRt = "rt";
    private const string AttrRevoked = "revoked";
    private const string AttrLockUntil = "lockUntil";
    private const string AttrLockToken = "lockToken";
    private const string AttrAccessToken = "at";
    private const string AttrAccessTokenExp = "atExp";
    private const string AttrSub = "sub";
    private const string AttrTenant = "tenant";
    private const string AttrPool = "pool";
    private const string AttrTtl = "TTL";

    private readonly IAmazonDynamoDB _ddb;
    private readonly BffOptions _options;
    private readonly ILogger<DynamoBffSessionStore> _logger;

    public DynamoBffSessionStore(
        IAmazonDynamoDB ddb,
        IOptions<BffOptions> options,
        ILogger<DynamoBffSessionStore> logger)
    {
        _ddb = ddb;
        _options = options.Value;
        _logger = logger;
    }

    private static string Pk(string sid) => $"SESSION#{sid}";

    private static long NowEpoch() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    public async Task CreateAsync(BffSessionRecord record, CancellationToken ct = default)
    {
        var item = new Dictionary<string, AttributeValue>
        {
            [PkName] = new AttributeValue { S = Pk(record.Sid) },
            [SkName] = new AttributeValue { S = SkValue },
            [AttrRt] = new AttributeValue { S = record.RefreshToken },
            [AttrRevoked] = new AttributeValue { BOOL = record.Revoked },
            [AttrTtl] = new AttributeValue { N = record.Ttl.ToString() },
        };
        if (!string.IsNullOrEmpty(record.Sub)) item[AttrSub] = new AttributeValue { S = record.Sub };
        if (!string.IsNullOrEmpty(record.Tenant)) item[AttrTenant] = new AttributeValue { S = record.Tenant };
        if (!string.IsNullOrEmpty(record.Pool)) item[AttrPool] = new AttributeValue { S = record.Pool };
        if (record.LockUntil.HasValue) item[AttrLockUntil] = new AttributeValue { N = record.LockUntil.Value.ToString() };

        await _ddb.PutItemAsync(new PutItemRequest
        {
            TableName = _options.SessionTableName,
            Item = item,
        }, ct).ConfigureAwait(false);
    }

    public async Task<BffSessionRecord?> GetAsync(string sid, CancellationToken ct = default)
    {
        var resp = await _ddb.GetItemAsync(new GetItemRequest
        {
            TableName = _options.SessionTableName,
            Key = new Dictionary<string, AttributeValue>
            {
                [PkName] = new AttributeValue { S = Pk(sid) },
                [SkName] = new AttributeValue { S = SkValue },
            },
            ConsistentRead = true,
        }, ct).ConfigureAwait(false);

        if (resp.Item is null || resp.Item.Count == 0)
            return null;

        var item = resp.Item;
        var rec = new BffSessionRecord { Sid = sid };

        if (item.TryGetValue(AttrRt, out var rt)) rec.RefreshToken = rt.S ?? string.Empty;
        if (item.TryGetValue(AttrRevoked, out var rev)) rec.Revoked = rev.BOOL ?? false;
        if (item.TryGetValue(AttrLockUntil, out var lu) && long.TryParse(lu.N, out var luv)) rec.LockUntil = luv;
        if (item.TryGetValue(AttrLockToken, out var lt)) rec.LockToken = lt.S;
        if (item.TryGetValue(AttrAccessToken, out var at)) rec.AccessToken = at.S;
        if (item.TryGetValue(AttrAccessTokenExp, out var ae) && long.TryParse(ae.N, out var aev)) rec.AccessTokenExp = aev;
        if (item.TryGetValue(AttrSub, out var sub)) rec.Sub = sub.S;
        if (item.TryGetValue(AttrTenant, out var t)) rec.Tenant = t.S;
        if (item.TryGetValue(AttrPool, out var p)) rec.Pool = p.S;
        if (item.TryGetValue(AttrTtl, out var ttl) && long.TryParse(ttl.N, out var ttlv)) rec.Ttl = ttlv;

        return rec;
    }

    public async Task<string?> TryAcquireRefreshLockAsync(string sid, int lockSeconds, CancellationToken ct = default)
    {
        var now = NowEpoch();
        var lockUntil = now + lockSeconds;
        // Unique fencing token written under the lock; the rt/at persistence later conditions
        // on this exact value still being present, so an EXPIRED lock (different/no token)
        // cannot be mistaken for a completed refresh and a stale winner cannot clobber a newer one.
        var lockToken = Guid.NewGuid().ToString("N");
        try
        {
            await _ddb.UpdateItemAsync(new UpdateItemRequest
            {
                TableName = _options.SessionTableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    [PkName] = new AttributeValue { S = Pk(sid) },
                    [SkName] = new AttributeValue { S = SkValue },
                },
                // Acquire only if no live lock exists.
                ConditionExpression = "attribute_not_exists(#lock) OR #lock < :now",
                UpdateExpression = "SET #lock = :until, #token = :token",
                ExpressionAttributeNames = new Dictionary<string, string>
                {
                    ["#lock"] = AttrLockUntil,
                    ["#token"] = AttrLockToken,
                },
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":now"] = new AttributeValue { N = now.ToString() },
                    [":until"] = new AttributeValue { N = lockUntil.ToString() },
                    [":token"] = new AttributeValue { S = lockToken },
                },
            }, ct).ConfigureAwait(false);
            return lockToken;
        }
        catch (ConditionalCheckFailedException)
        {
            // Another request holds the lock; caller should re-read.
            return null;
        }
    }

    public async Task UpdateRefreshAsync(
        string sid,
        string newRefreshToken,
        string accessToken,
        long accessTokenExp,
        string lockToken,
        CancellationToken ct = default)
    {
        await _ddb.UpdateItemAsync(new UpdateItemRequest
        {
            TableName = _options.SessionTableName,
            Key = new Dictionary<string, AttributeValue>
            {
                [PkName] = new AttributeValue { S = Pk(sid) },
                [SkName] = new AttributeValue { S = SkValue },
            },
            // Lock-fencing: only the holder of the live fencing token may rotate rt, publish the
            // fresh access token, and clear the lock — all atomically in this single UpdateItem.
            ConditionExpression = "#token = :token",
            UpdateExpression = "SET #rt = :rt, #at = :at, #atExp = :atExp REMOVE #lock, #token",
            ExpressionAttributeNames = new Dictionary<string, string>
            {
                ["#rt"] = AttrRt,
                ["#at"] = AttrAccessToken,
                ["#atExp"] = AttrAccessTokenExp,
                ["#lock"] = AttrLockUntil,
                ["#token"] = AttrLockToken,
            },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":rt"] = new AttributeValue { S = newRefreshToken },
                [":at"] = new AttributeValue { S = accessToken },
                [":atExp"] = new AttributeValue { N = accessTokenExp.ToString() },
                [":token"] = new AttributeValue { S = lockToken },
            },
        }, ct).ConfigureAwait(false);
    }

    public async Task DeleteAsync(string sid, CancellationToken ct = default)
    {
        await _ddb.DeleteItemAsync(new DeleteItemRequest
        {
            TableName = _options.SessionTableName,
            Key = new Dictionary<string, AttributeValue>
            {
                [PkName] = new AttributeValue { S = Pk(sid) },
                [SkName] = new AttributeValue { S = SkValue },
            },
        }, ct).ConfigureAwait(false);
    }
}
