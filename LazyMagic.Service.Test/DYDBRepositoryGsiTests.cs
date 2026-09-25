using Amazon.DynamoDBv2.Model;
using LazyMagic.Service.DynamoDBRepo;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace LazyMagic.Service.Test;

/// <summary>
/// Pure unit tests (mocked IAmazonDynamoDB, no AWS, no DynamoDB Local) pinning what the table kind changes.
///
/// - The level and kind pick the table: a Gsi entity lives in its level's "_gsi" twin.
/// - An Lsi entity's queries are untouched: same request, same single query, no fetch.
/// - A Gsi entity's index query asks the keys-only GSI for keys, then fetches the items with BatchGetItem,
///   consistently by default. The index lags the table, so each fetched item is re-checked against the query
///   (compared by UTF-8 bytes, DynamoDB's order): an item that is gone, or whose key no longer matches, is left
///   out, and the index's order is kept.
/// - A read it cannot finish is never passed off as a short list: throttling, or keys still unprocessed when the
///   retries run out, is 503; a request it cannot re-check is 500.
/// - The batched and consistent reads by key, and ListGreaterThanAsync's limit.
/// </summary>
public class DYDBRepositoryGsiTests
{
    private const string Table = "unit-test-table";
    private const string GsiTable = Table + "_gsi";
    private const string DefaultProjection = "#Data, TypeName, #Status, UpdateUtcTick, CreateUtcTick, #General";

    private static readonly ICallerInfo Caller = new CallerInfo
    {
        DefaultDB = Table,
        SystemDB = "system-table",
        TenantDB = "tenant-table",
        SubtenantDB = "subtenant-table",
        LzUserId = "unit-test-user",
        TenantId = "unit-test-tenant",
        SessionId = "unit-test-session",
    };

    /// <summary>TestItemRepo with the kind and level the test chooses and its protected surface opened.</summary>
    private sealed class KindRepo : TestItemRepo
    {
        private readonly TableKind kind;
        private readonly TableLevel level;

        public KindRepo(IAmazonDynamoDB client, TableKind kind, TableLevel level = TableLevel.Default, string? localTable = null)
            : base(client)
        {
            this.kind = kind;
            this.level = level;
            tablename = localTable;
        }

        protected override TableKind TableKind => kind;
        protected override TableLevel TableLevel => level;

        public List<TimeSpan> Delays { get; } = new();
        protected override Task DelayBeforeRetryAsync(TimeSpan delay)
        {
            Delays.Add(delay);
            return Task.CompletedTask;
        }

        public bool FilterIsDeleted { set => UseIsDeleted = value; }
        public string TableFor(ICallerInfo callerInfo) => GetTableName(callerInfo);
        public Task<ObjectResult> List(QueryRequest request, int limit = 0) => ListAndSizeAsync(request, limit);
        public QueryRequest IndexEquals(string keyField, string value) => QueryEquals(EntityType, keyField, value, callerInfo: Caller);
        public Task<ObjectResult> ReadMany(IReadOnlyList<string> ids) => ReadManyAsync(Caller, ids);
        public Task<ObjectResult> ReadMany(IReadOnlyList<string> ids, bool consistentRead) => ReadManyAsync(Caller, ids, consistentRead);
        public Task<ActionResult<TestItem>> ReadConsistent(string id) => ReadAsync(Caller, id, consistentRead: true);
        public Task<ObjectResult> ListConsistent() => ListAsync(Caller, 0, consistentRead: true);
    }

    private sealed record QuerySnapshot(
        string Table, string Index, string KeyCondition, string Projection,
        Dictionary<string, string>? Names, int? Limit, bool HasStartKey, bool? ConsistentRead);

    private sealed record BatchSnapshot(string Table, List<string> SortKeys, bool? ConsistentRead);

    /// <summary>
    /// A mocked client over a table of items and a script of index pages. BatchGetItem answers from the items,
    /// in reverse order (BatchGetItem promises none), and leaves unprocessed whatever <see cref="Unprocessed"/>
    /// names for that call. Requests are copied when made: the repo mutates one QueryRequest across pages.
    /// </summary>
    private sealed class Store
    {
        public Mock<IAmazonDynamoDB> Client { get; } = new(MockBehavior.Loose);
        public Dictionary<string, Dictionary<string, AttributeValue>> Items { get; } = new();
        public Queue<QueryResponse> Pages { get; } = new();
        public List<QuerySnapshot> Queries { get; } = new();
        public List<BatchSnapshot> Batches { get; } = new();
        public List<GetItemRequest> Gets { get; } = new();
        /// <summary>(call number from 0, the sort keys asked) => the sort keys to leave unprocessed.</summary>
        public Func<int, IReadOnlyList<string>, IEnumerable<string>> Unprocessed { get; set; } = (_, _) => [];

        public Store()
        {
            Client
                .Setup(c => c.QueryAsync(It.IsAny<QueryRequest>(), It.IsAny<CancellationToken>()))
                .Callback<QueryRequest, CancellationToken>((r, _) => Queries.Add(new QuerySnapshot(
                    r.TableName, r.IndexName, r.KeyConditionExpression, r.ProjectionExpression,
                    r.ExpressionAttributeNames is null ? null : new Dictionary<string, string>(r.ExpressionAttributeNames),
                    r.Limit, r.ExclusiveStartKey is { Count: > 0 }, r.ConsistentRead)))
                .ReturnsAsync(() => Pages.Dequeue());
            Client
                .Setup(c => c.BatchGetItemAsync(It.IsAny<BatchGetItemRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((BatchGetItemRequest r, CancellationToken _) => Answer(r));
            Client
                .Setup(c => c.GetItemAsync(It.IsAny<GetItemRequest>(), It.IsAny<CancellationToken>()))
                .Callback<GetItemRequest, CancellationToken>((r, _) => Gets.Add(r))
                .ReturnsAsync((GetItemRequest r, CancellationToken _) => new GetItemResponse
                {
                    Item = Items.TryGetValue(r.Key["SK"].S, out var item) ? item : new Dictionary<string, AttributeValue>()
                });
        }

        private BatchGetItemResponse Answer(BatchGetItemRequest request)
        {
            var (table, asked) = request.RequestItems.Single();
            var sortKeys = asked.Keys.Select(k => k["SK"].S).ToList();
            var call = Batches.Count;
            Batches.Add(new BatchSnapshot(table, sortKeys, asked.ConsistentRead));
            var unprocessed = Unprocessed(call, sortKeys).ToHashSet();
            var found = sortKeys.Where(sk => !unprocessed.Contains(sk) && Items.ContainsKey(sk))
                .Select(sk => Items[sk]).Reverse().ToList();
            var response = new BatchGetItemResponse
            {
                Responses = new Dictionary<string, List<Dictionary<string, AttributeValue>>> { [table] = found }
            };
            if (unprocessed.Count > 0)
                response.UnprocessedKeys = new Dictionary<string, KeysAndAttributes>
                {
                    [table] = new KeysAndAttributes
                    {
                        Keys = asked.Keys.Where(k => unprocessed.Contains(k["SK"].S)).ToList(),
                        ConsistentRead = asked.ConsistentRead
                    }
                };
            return response;
        }

        public void Put(string id, string? sk1, int padToBytes = 0)
        {
            var padding = padToBytes > 0 ? new string('x', padToBytes) : string.Empty;
            var item = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new AttributeValue { S = "TestItem:" },
                ["SK"] = new AttributeValue { S = $"{id}:" },
                ["Data"] = new AttributeValue { S = $"{{\"id\":\"{id}\",\"name\":\"{sk1}\",\"description\":\"{padding}\"}}" },
            };
            if (sk1 is not null)
                item["SK1"] = new AttributeValue { S = sk1 };
            Items[$"{id}:"] = item;
        }

        /// <summary>An index page of keys-only entries (PK, SK, SK1), as the GSI returns them.</summary>
        public void Page(Dictionary<string, AttributeValue>? lastEvaluatedKey, params (string Id, string Sk1)[] entries)
            => Pages.Enqueue(new QueryResponse
            {
                Items = entries.Select(e => new Dictionary<string, AttributeValue>
                {
                    ["PK"] = new AttributeValue { S = "TestItem:" },
                    ["SK"] = new AttributeValue { S = $"{e.Id}:" },
                    ["SK1"] = new AttributeValue { S = e.Sk1 },
                }).ToList(),
                LastEvaluatedKey = lastEvaluatedKey,
            });

        public KindRepo Repo(TableKind kind = TableKind.Gsi) => new(Client.Object, kind);
    }

    private static Dictionary<string, AttributeValue> Cursor(string id) => new()
    {
        ["PK"] = new AttributeValue { S = "TestItem:" },
        ["SK"] = new AttributeValue { S = $"{id}:" },
        ["SK1"] = new AttributeValue { S = "cursor" },
    };

    private static List<string> IdsOf(ObjectResult result)
    {
        Assert.NotNull(result.Value);
        return Assert.IsAssignableFrom<IEnumerable<TestItem>>(result.Value!).Select(i => i.Id).ToList();
    }

    // ---- WHICH TABLE ---------------------------------------------------------------------------

    [Theory]
    [InlineData(TableLevel.Default, TableKind.Lsi, "unit-test-table")]
    [InlineData(TableLevel.Default, TableKind.Gsi, "unit-test-table_gsi")]
    [InlineData(TableLevel.Subtenant, TableKind.Gsi, "subtenant-table_gsi")]
    [InlineData(TableLevel.Tenant, TableKind.Lsi, "tenant-table")]
    [InlineData(TableLevel.Tenant, TableKind.Gsi, "tenant-table_gsi")]
    [InlineData(TableLevel.System, TableKind.Lsi, "system-table")]
    [InlineData(TableLevel.Local, TableKind.Gsi, "local-table_gsi")]
    public void TheLevelAndKindPickTheTable(TableLevel level, TableKind kind, string expected)
    {
        var repo = new KindRepo(new Mock<IAmazonDynamoDB>().Object, kind, level, localTable: "local-table");

        Assert.Equal(expected, repo.TableFor(Caller));
    }

    [Fact]
    public void AGsiEntityWithNoTableNameStillHasNone()
    {
        // "_gsi" on its own would name a table nobody created and hide the missing configuration.
        var repo = new KindRepo(new Mock<IAmazonDynamoDB>().Object, TableKind.Gsi);

        Assert.Equal(string.Empty, repo.TableFor(new CallerInfo { DefaultDB = "" }));
        Assert.Null(repo.TableFor(new CallerInfo()));
    }

    [Fact]
    public async Task ARepoThatOverridesNeither_ReadsTheDefaultTable_OnTheLsiPath()
    {
        // TestItemRepo overrides neither property, like a repo generated before the schema keys existed.
        var store = new Store();
        store.Pages.Enqueue(new QueryResponse
        {
            Items = [new Dictionary<string, AttributeValue> { ["Data"] = new AttributeValue { S = "{\"id\":\"a\"}" } }],
        });

        var result = await new TestItemRepo(store.Client.Object).ListAsync(Caller, "SK1", "x");

        Assert.Equal(new[] { "a" }, IdsOf(result));
        Assert.Equal(Table, store.Queries.Single().Table);
        Assert.Equal(DefaultProjection, store.Queries.Single().Projection);
        Assert.Empty(store.Batches);
    }

    // ---- AN LSI ENTITY IS UNTOUCHED ------------------------------------------------------------

    [Fact]
    public async Task AnLsiIndexQuery_IsTheRequestItWasBefore_AndFetchesNothing()
    {
        var store = new Store();
        store.Pages.Enqueue(new QueryResponse
        {
            Items = [new Dictionary<string, AttributeValue> { ["Data"] = new AttributeValue { S = "{\"id\":\"a\"}" } }],
        });

        var result = await store.Repo(TableKind.Lsi).ListAsync(Caller, "SK1", "x");

        Assert.Equal(new[] { "a" }, IdsOf(result));
        Assert.Equal(200, result.StatusCode);
        var query = store.Queries.Single();
        Assert.Equal(Table, query.Table);
        Assert.Equal("PK-SK1-Index", query.Index);
        Assert.Equal("PK = :PKval and SK1 = :SKval", query.KeyCondition);
        Assert.Equal(DefaultProjection, query.Projection);
        Assert.Equal(new[] { "#Data", "#General", "#Status" }, query.Names!.Keys.Order());
        Assert.Null(query.ConsistentRead);
        Assert.Empty(store.Batches);
    }

    // ---- A GSI ENTITY: KEYS, THEN FETCH --------------------------------------------------------

    [Fact]
    public async Task AGsiIndexQuery_AsksTheTwinForKeysOnly_WithNoAttributeNames()
    {
        var store = new Store();
        store.Put("a", "x");
        store.Page(null, ("a", "x"));

        var result = await store.Repo().ListAsync(Caller, "SK1", "x");

        Assert.Equal(new[] { "a" }, IdsOf(result));
        var query = store.Queries.Single();
        Assert.Equal(GsiTable, query.Table);
        Assert.Equal("PK-SK1-Index", query.Index);
        Assert.Equal("PK = :PKval and SK1 = :SKval", query.KeyCondition);
        Assert.Equal("PK, SK", query.Projection);
        // Null, not an empty map: DynamoDB refuses an empty ExpressionAttributeNames.
        Assert.Null(query.Names);
        Assert.Equal(GsiTable, store.Batches.Single().Table);
    }

    [Fact]
    public async Task TheFetchIsConsistent_AndTheIndexOrderIsKept()
    {
        // The store answers BatchGetItem in reverse; the list must still come back in the index's order.
        var store = new Store();
        foreach (var id in new[] { "c", "a", "b" })
            store.Put(id, "x");
        store.Page(null, ("c", "x"), ("a", "x"), ("b", "x"));

        var result = await store.Repo().ListAsync(Caller, "SK1", "x");

        Assert.Equal(new[] { "c", "a", "b" }, IdsOf(result));
        Assert.Equal(200, result.StatusCode);
        var batch = store.Batches.Single();
        Assert.Equal(new[] { "c:", "a:", "b:" }, batch.SortKeys);
        Assert.True(batch.ConsistentRead);
    }

    [Fact]
    public async Task AnItemTheIndexStillListsButTheTableNoLongerHas_IsLeftOut()
    {
        var store = new Store();
        store.Put("a", "x");
        store.Put("c", "x");
        store.Page(null, ("a", "x"), ("deleted", "x"), ("c", "x"));

        var result = await store.Repo().ListAsync(Caller, "SK1", "x");

        Assert.Equal(new[] { "a", "c" }, IdsOf(result));
        Assert.Equal(200, result.StatusCode);
    }

    [Fact]
    public async Task AnItemWithNoData_IsGone_AsReadAsyncs404Is()
    {
        var store = new Store();
        store.Put("a", "x");
        store.Items["a:"]["Data"] = new AttributeValue { S = "" };
        store.Page(null, ("a", "x"));

        var result = await store.Repo().ListAsync(Caller, "SK1", "x");

        Assert.Empty(IdsOf(result));
        Assert.Equal(200, result.StatusCode);
    }

    [Fact]
    public async Task AnItemWhoseKeyChangedSinceItWasIndexed_IsLeftOut()
    {
        // The entry still says "x"; the item now says "y". The item is the truth.
        var store = new Store();
        store.Put("a", "x");
        store.Put("b", "y");
        store.Page(null, ("a", "x"), ("b", "x"));

        var result = await store.Repo().ListAsync(Caller, "SK1", "x");

        Assert.Equal(new[] { "a" }, IdsOf(result));
    }

    [Fact]
    public async Task AnItemThatNoLongerCarriesTheKey_IsLeftOut()
    {
        var store = new Store();
        store.Put("a", "x");
        store.Put("b", null);
        store.Page(null, ("a", "x"), ("b", "x"));

        var result = await store.Repo().ListAsync(Caller, "SK1", "x");

        Assert.Equal(new[] { "a" }, IdsOf(result));
    }

    [Theory]
    [InlineData("<", "m", "a", true)]
    [InlineData("<", "m", "m", false)]
    [InlineData("<=", "m", "m", true)]
    [InlineData("<=", "m", "n", false)]
    [InlineData(">", "m", "n", true)]
    [InlineData(">", "m", "m", false)]
    [InlineData(">=", "m", "m", true)]
    [InlineData(">=", "m", "l", false)]
    [InlineData("=", "m", "m", true)]
    [InlineData("=", "m", "n", false)]
    [InlineData("begins_with", "ab", "abc", true)]
    [InlineData("begins_with", "ab", "ab", true)]
    [InlineData("begins_with", "ab", "b", false)]
    public async Task EachOperatorIsReCheckedAgainstTheFetchedItem(string op, string queried, string itemValue, bool kept)
    {
        // The entry claims a value that satisfies the query; the fetched item holds itemValue.
        var store = new Store();
        store.Put("a", itemValue);
        store.Page(null, ("a", queried));
        var repo = store.Repo();

        var result = op switch
        {
            "<" => await repo.ListLessThanAsync(Caller, "SK1", queried),
            "<=" => await repo.ListLessThanOrEqualAsync(Caller, "SK1", queried),
            ">" => await repo.ListGreaterThanAsync(Caller, "SK1", queried),
            ">=" => await repo.ListGreaterThanOrEqualAsync(Caller, "SK1", queried),
            "=" => await repo.ListAsync(Caller, "SK1", queried),
            "begins_with" => await repo.ListBeginsWithAsync(Caller, "SK1", queried),
            _ => throw new ArgumentOutOfRangeException(nameof(op)),
        };

        Assert.Equal(kept ? new[] { "a" } : Array.Empty<string>(), IdsOf(result));
    }

    [Fact]
    public async Task BetweenIsInclusiveAtBothEnds_AndReChecked()
    {
        var store = new Store();
        store.Put("lower", "b");
        store.Put("upper", "d");
        store.Put("moved", "e");
        store.Page(null, ("lower", "b"), ("upper", "d"), ("moved", "c"));

        var result = await store.Repo().ListBetweenAsync(Caller, "SK1", "b", "d");

        Assert.Equal(new[] { "lower", "upper" }, IdsOf(result));
        Assert.Equal("PK = :PKval and SK1 between :SKStart and :SKEnd", store.Queries.Single().KeyCondition);
    }

    [Fact]
    public async Task TheReCheckComparesUtf8Bytes_NotUtf16()
    {
        // U+1F600 is F0 9F 98 80 in UTF-8 and D83D DE00 in UTF-16; U+FF5E is EF BD 9E and FF5E. DynamoDB, comparing
        // UTF-8 bytes, puts the emoji AFTER U+FF5E; string.CompareOrdinal puts it before, and would drop it.
        var store = new Store();
        store.Put("a", "\U0001F600");
        store.Page(null, ("a", "\U0001F600"));

        var result = await store.Repo().ListGreaterThanAsync(Caller, "SK1", "～");

        Assert.Equal(new[] { "a" }, IdsOf(result));
        Assert.True(string.CompareOrdinal("\U0001F600", "～") < 0, "the premise: UTF-16 order disagrees");
    }

    [Fact]
    public async Task AnItemListedOnTwoPages_IsFetchedAndListedOnce()
    {
        // An item whose key changed while the query paged can appear on both sides of the move.
        var store = new Store();
        foreach (var id in new[] { "a", "b", "c" })
            store.Put(id, "x");
        store.Page(Cursor("b"), ("a", "x"), ("b", "x"));
        store.Page(null, ("b", "x"), ("c", "x"));

        var result = await store.Repo().ListAsync(Caller, "SK1", "x");

        Assert.Equal(new[] { "a", "b", "c" }, IdsOf(result));
        Assert.Equal(new[] { "a:", "b:" }, store.Batches[0].SortKeys);
        Assert.Equal(new[] { "c:" }, store.Batches[1].SortKeys);
        Assert.True(store.Queries[1].HasStartKey);
    }

    [Fact]
    public async Task TheLimitIsATotalBudget_AndADroppedItemIsReplacedFromTheNextPage()
    {
        var store = new Store();
        store.Put("a", "x");
        store.Put("b", "moved");
        store.Put("c", "x");
        store.Page(Cursor("b"), ("a", "x"), ("b", "x"));
        store.Page(Cursor("c"), ("c", "x"));

        var result = await store.Repo().ListAsync(Caller, "SK1", "x", limit: 2);

        Assert.Equal(new[] { "a", "c" }, IdsOf(result));
        // More remains past the limit: 206, as on the LSI path.
        Assert.Equal(206, result.StatusCode);
        Assert.Equal(new int?[] { 2, 1 }, store.Queries.Select(q => q.Limit));
    }

    [Fact]
    public async Task TheSizeCapTruncatesWith206()
    {
        const int twoMb = 2 * 1024 * 1024;
        var store = new Store();
        foreach (var id in new[] { "a", "b", "c" })
            store.Put(id, "x", twoMb);
        store.Page(null, ("a", "x"), ("b", "x"), ("c", "x"));

        var result = await store.Repo().ListAsync(Caller, "SK1", "x");

        Assert.Equal(new[] { "a", "b" }, IdsOf(result));
        Assert.Equal(206, result.StatusCode);
    }

    [Fact]
    public async Task KeysAreFetchedAHundredAtATime()
    {
        var store = new Store();
        var entries = Enumerable.Range(0, 250).Select(i => ($"id{i:D3}", "x")).ToArray();
        foreach (var (id, sk1) in entries)
            store.Put(id, sk1);
        store.Page(null, entries);

        var result = await store.Repo().ListAsync(Caller, "SK1", "x");

        Assert.Equal(250, IdsOf(result).Count);
        Assert.Equal(new[] { 100, 100, 50 }, store.Batches.Select(b => b.SortKeys.Count));
        Assert.Equal(entries.Select(e => e.Item1), IdsOf(result));
    }

    // ---- UNPROCESSED KEYS AND THROTTLING -------------------------------------------------------

    [Fact]
    public async Task UnprocessedKeysAreAskedAgainAfterAWait_AndThenServed()
    {
        var store = new Store();
        store.Put("a", "x");
        store.Put("b", "x");
        store.Page(null, ("a", "x"), ("b", "x"));
        store.Unprocessed = (call, _) => call == 0 ? ["b:"] : [];
        var repo = store.Repo();

        var result = await repo.ListAsync(Caller, "SK1", "x");

        Assert.Equal(new[] { "a", "b" }, IdsOf(result));
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(new[] { "b:" }, store.Batches[1].SortKeys);
        Assert.True(store.Batches[1].ConsistentRead);
        Assert.Single(repo.Delays);
    }

    [Fact]
    public async Task KeysThatStayUnprocessed_Are503_NeverGone()
    {
        // A key the fetch could not read is unknown. Listing without it would present a short list as the answer.
        var store = new Store();
        store.Put("a", "x");
        store.Put("b", "x");
        store.Page(null, ("a", "x"), ("b", "x"));
        store.Unprocessed = (_, asked) => asked;
        var repo = store.Repo();

        var result = await repo.ListAsync(Caller, "SK1", "x");

        Assert.Equal(503, result.StatusCode);
        Assert.Null(result.Value);
        Assert.Equal(6, store.Batches.Count);
        Assert.Equal(5, repo.Delays.Count);
        Assert.All(repo.Delays, d => Assert.InRange(d, TimeSpan.Zero, TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public async Task ProgressResetsTheCount_SoASlowButMovingReadFinishes()
    {
        // Seven keys, one settled per response: seven responses with keys left over, none of them without progress.
        var store = new Store();
        var ids = Enumerable.Range(0, 7).Select(i => $"k{i}").ToArray();
        foreach (var id in ids)
            store.Put(id, "x");
        store.Page(null, ids.Select(id => (id, "x")).ToArray());
        store.Unprocessed = (_, asked) => asked.Skip(1);

        var result = await store.Repo().ListAsync(Caller, "SK1", "x");

        Assert.Equal(ids, IdsOf(result));
        Assert.Equal(7, store.Batches.Count);
    }

    [Fact]
    public async Task AThrottledIndexQuery_Is503()
    {
        var store = new Store();
        store.Client
            .Setup(c => c.QueryAsync(It.IsAny<QueryRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProvisionedThroughputExceededException("slow down"));

        var result = await store.Repo().ListAsync(Caller, "SK1", "x");

        Assert.Equal(503, result.StatusCode);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task AThrottledFetch_Is503()
    {
        var store = new Store();
        store.Page(null, ("a", "x"));
        store.Client
            .Setup(c => c.BatchGetItemAsync(It.IsAny<BatchGetItemRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestLimitExceededException("slow down"));

        var result = await store.Repo().ListAsync(Caller, "SK1", "x");

        Assert.Equal(503, result.StatusCode);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task AnyOtherDynamoDbRefusal_Is500()
    {
        var store = new Store();
        store.Client
            .Setup(c => c.QueryAsync(It.IsAny<QueryRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonDynamoDBException("ValidationException"));

        var result = await store.Repo().ListAsync(Caller, "SK1", "x");

        Assert.Equal(500, result.StatusCode);
    }

    // ---- REQUESTS IT CANNOT RE-CHECK -----------------------------------------------------------

    [Fact]
    public async Task AKeyConditionTheBuildersDoNotMake_Is500_AndQueriesNothing()
    {
        var store = new Store();
        var request = store.Repo().IndexEquals("SK1", "x");
        request.KeyConditionExpression = "PK = :PKval and SK1 = :other";

        var result = await store.Repo().List(request);

        Assert.Equal(500, result.StatusCode);
        Assert.Empty(store.Queries);
    }

    [Fact]
    public async Task AFilter_Is500_BecauseItWouldRunOnKeysOnly()
    {
        var store = new Store();
        var request = store.Repo().IndexEquals("SK1", "x");
        request.FilterExpression = "#Status = :s";

        var result = await store.Repo().List(request);

        Assert.Equal(500, result.StatusCode);
        Assert.Empty(store.Queries);
    }

    [Fact]
    public async Task AConsistentIndexQuery_Is500_BecauseAGsiCannotGiveOne()
    {
        var store = new Store();
        var request = store.Repo().IndexEquals("SK1", "x");
        request.ConsistentRead = true;

        var result = await store.Repo().List(request);

        Assert.Equal(500, result.StatusCode);
        Assert.Empty(store.Queries);
    }

    [Fact]
    public async Task UseIsDeleted_OnAGsiIndexQuery_Throws()
    {
        var store = new Store();
        var repo = store.Repo();
        repo.FilterIsDeleted = true;

        await Assert.ThrowsAsync<NotSupportedException>(() => repo.ListAsync(Caller, "SK1", "x"));
    }

    [Fact]
    public async Task AnIndexQueryOnEveryEntry_KeepsItemsThatStillCarryTheKey()
    {
        // No builder makes it, but it is the natural "whole index, in value order" read.
        var store = new Store();
        store.Put("a", "x");
        store.Put("b", null);
        store.Page(null, ("a", "x"), ("b", "y"));
        var request = store.Repo().IndexEquals("SK1", "x");
        request.KeyConditionExpression = "PK = :PKval";
        request.ExpressionAttributeValues.Remove(":SKval");

        var result = await store.Repo().List(request);

        Assert.Equal(new[] { "a" }, IdsOf(result));
    }

    // ---- BASE-TABLE QUERIES ON A GSI ENTITY ----------------------------------------------------

    [Fact]
    public async Task AGsiEntitysWholeTypeList_ReadsTheTwinsBaseTable_WithoutAFetch()
    {
        var store = new Store();
        store.Pages.Enqueue(new QueryResponse
        {
            Items = [new Dictionary<string, AttributeValue> { ["Data"] = new AttributeValue { S = "{\"id\":\"a\"}" } }],
        });

        var result = await store.Repo().ListAsync(Caller);

        Assert.Equal(new[] { "a" }, IdsOf(result));
        var query = store.Queries.Single();
        Assert.Equal(GsiTable, query.Table);
        Assert.Null(query.Index);
        Assert.Equal(DefaultProjection, query.Projection);
        Assert.Empty(store.Batches);
    }

    [Fact]
    public async Task TheConsistentWholeTypeList_AsksForAConsistentRead()
    {
        var store = new Store();
        store.Page(null);

        await store.Repo().ListConsistent();

        var query = store.Queries.Single();
        Assert.True(query.ConsistentRead);
        Assert.Null(query.Index);
    }

    // ---- READS BY KEY --------------------------------------------------------------------------

    [Fact]
    public async Task ReadMany_ReturnsTheIdsOrder_EachOnce_LeavingOutTheGone()
    {
        var store = new Store();
        foreach (var id in new[] { "a", "b", "c" })
            store.Put(id, "x");
        store.Put("empty", "x");
        store.Items["empty:"]["Data"] = new AttributeValue { S = "" };

        var result = await store.Repo(TableKind.Lsi).ReadMany(["c", "a", "c", "missing", "empty", "b"]);

        Assert.Equal(200, result.StatusCode);
        Assert.Equal(new[] { "c", "a", "b" }, IdsOf(result));
        var batch = store.Batches.Single();
        Assert.Equal(Table, batch.Table);
        Assert.Equal(new[] { "c:", "a:", "missing:", "empty:", "b:" }, batch.SortKeys);
        Assert.True(batch.ConsistentRead);
    }

    [Fact]
    public async Task ReadMany_CanReadEventuallyConsistently_WhenAsked()
    {
        var store = new Store();
        store.Put("a", "x");

        await store.Repo().ReadMany(["a"], consistentRead: false);

        Assert.False(store.Batches.Single().ConsistentRead);
        Assert.Equal(GsiTable, store.Batches.Single().Table);
    }

    [Fact]
    public async Task ReadMany_WhenKeysStayUnprocessed_Is503()
    {
        var store = new Store();
        store.Put("a", "x");
        store.Unprocessed = (_, asked) => asked;

        var result = await store.Repo().ReadMany(["a"]);

        Assert.Equal(503, result.StatusCode);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task ReadMany_WhenThrottled_Is503()
    {
        var store = new Store();
        store.Client
            .Setup(c => c.BatchGetItemAsync(It.IsAny<BatchGetItemRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ThrottlingException("slow down"));

        var result = await store.Repo().ReadMany(["a"]);

        Assert.Equal(503, result.StatusCode);
    }

    [Fact]
    public async Task TheConsistentReadAsync_AsksForOne_AndThePublicReadStillDoesNot()
    {
        var store = new Store();
        store.Put("a", "x");
        var repo = store.Repo();

        var consistent = await repo.ReadConsistent("a");
        var plain = await repo.ReadAsync(Caller, "a");

        Assert.Equal("a", consistent.Value!.Id);
        Assert.Equal("a", plain.Value!.Id);
        Assert.True(store.Gets[0].ConsistentRead);
        Assert.Null(store.Gets[1].ConsistentRead);
        Assert.All(store.Gets, g => Assert.Equal(GsiTable, g.TableName));
    }

    // ---- LISTGREATERTHANASYNC ------------------------------------------------------------------

    [Fact]
    public async Task ListGreaterThanAsync_PassesItsLimit()
    {
        // It built the query and then called ListAndSizeAsync without the limit, so the limit was ignored.
        var store = new Store();
        store.Page(null);

        await store.Repo(TableKind.Lsi).ListGreaterThanAsync(Caller, "SK1", "m", limit: 2);

        Assert.Equal(2, store.Queries.Single().Limit);
    }
}
