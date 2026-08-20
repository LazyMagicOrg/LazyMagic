using Amazon.DynamoDBv2.Model;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace LazyMagic.Service.Test;

/// <summary>
/// Pure unit tests (mocked IAmazonDynamoDB — no AWS, no DynamoDB Local) pinning the
/// ListAndSizeAsync pagination contract:
///
/// - The query is DRAINED across DynamoDB pages by following LastEvaluatedKey. The original
///   implementation declared lastEvaluatedKey but never assigned it from the response, so every
///   List* returned only the first (&lt;=1MB) page as a "complete" 200 — silent truncation that
///   no caller could detect.
/// - A null OR EMPTY LastEvaluatedKey both mean "no more pages".
/// - The 5MB response cap truncates with a 206, never a 200.
/// - A non-zero limit is a TOTAL record budget: page N's query Limit is the REMAINING budget
///   (limit - fetched), not the full limit re-applied per page.
/// - limit == 0 means "no record cap" and must still drain every page.
/// </summary>
public class DYDBRepositoryPaginationTests
{
    private static readonly ICallerInfo Caller = new CallerInfo
    {
        DefaultDB = "unit-test-table",
        LzUserId = "unit-test-user",
        TenantId = "unit-test-tenant",
        SessionId = "unit-test-session",
    };

    private sealed record CapturedQuery(int? Limit, bool HasExclusiveStartKey);

    /// <summary>
    /// Repo over a scripted sequence of QueryResponse pages. Captures the Limit and
    /// ExclusiveStartKey of each QueryRequest AT CALL TIME (the repo mutates one request object
    /// across iterations, so the values must be copied, not the reference).
    /// </summary>
    private static (TestItemRepo repo, List<CapturedQuery> queries) RepoOverPages(params QueryResponse[] pages)
    {
        var queries = new List<CapturedQuery>();
        var remaining = new Queue<QueryResponse>(pages);
        var client = new Mock<IAmazonDynamoDB>(MockBehavior.Loose);
        client
            .Setup(c => c.QueryAsync(It.IsAny<QueryRequest>(), It.IsAny<CancellationToken>()))
            .Callback<QueryRequest, CancellationToken>((request, _) =>
                queries.Add(new CapturedQuery(
                    request.Limit,
                    request.ExclusiveStartKey is { Count: > 0 })))
            .ReturnsAsync(() => remaining.Dequeue());
        return (new TestItemRepo(client.Object), queries);
    }

    private static Dictionary<string, AttributeValue> Row(string id, int padToBytes = 0)
    {
        var padding = padToBytes > 0 ? new string('x', padToBytes) : string.Empty;
        var json = $"{{\"id\":\"{id}\",\"name\":\"item-{id}\",\"description\":\"{padding}\"}}";
        return new Dictionary<string, AttributeValue> { ["Data"] = new AttributeValue { S = json } };
    }

    private static QueryResponse Page(Dictionary<string, AttributeValue>? lastEvaluatedKey, params Dictionary<string, AttributeValue>[] rows)
        => new QueryResponse
        {
            Items = rows.ToList(),
            LastEvaluatedKey = lastEvaluatedKey,
        };

    private static Dictionary<string, AttributeValue> Cursor(string id)
        => new Dictionary<string, AttributeValue>
        {
            ["PK"] = new AttributeValue { S = "TestItem:" },
            ["SK"] = new AttributeValue { S = $"{id}:" },
        };

    private static List<TestItem> ItemsOf(ObjectResult result)
    {
        Assert.NotNull(result.Value);
        return Assert.IsAssignableFrom<IEnumerable<TestItem>>(result.Value!).ToList();
    }

    [Fact]
    public async Task ListAsync_DrainsEveryPage_AndReports200WhenComplete()
    {
        // Page 1 carries a continuation cursor; page 2 is the end of the data.
        var (repo, queries) = RepoOverPages(
            Page(Cursor("b"), Row("a"), Row("b")),
            Page(null, Row("c")));

        var result = await repo.ListAsync(Caller);

        var items = ItemsOf(result);
        // THE regression this file exists for: the unfixed code returned only page 1 (2 items) as a 200.
        Assert.Equal(new[] { "a", "b", "c" }, items.Select(i => i.Id));
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(2, queries.Count);
        Assert.False(queries[0].HasExclusiveStartKey);
        Assert.True(queries[1].HasExclusiveStartKey);
    }

    [Fact]
    public async Task ListAsync_EmptyLastEvaluatedKey_MeansComplete()
    {
        // The SDK signals "done" with an EMPTY key as well as a null one — an empty dictionary
        // must not trigger a second query (or an infinite loop re-querying page 1).
        var (repo, queries) = RepoOverPages(
            Page(new Dictionary<string, AttributeValue>(), Row("a")));

        var result = await repo.ListAsync(Caller);

        Assert.Single(ItemsOf(result));
        Assert.Equal(200, result.StatusCode);
        Assert.Single(queries);
    }

    [Fact]
    public async Task ListAsync_SizeCap_TruncatesWith206()
    {
        // Three ~2MB rows in one page: rows 1-2 fit under the 5MB cap, row 3 crosses it and is
        // dropped. The result must be 206 — a 200 here would present a truncated read as complete.
        const int twoMb = 2 * 1024 * 1024;
        var (repo, _) = RepoOverPages(
            Page(Cursor("c"), Row("a", twoMb), Row("b", twoMb), Row("c", twoMb)));

        var result = await repo.ListAsync(Caller);

        Assert.Equal(2, ItemsOf(result).Count);
        Assert.Equal(206, result.StatusCode);
    }

    [Fact]
    public async Task ListAsync_Limit_IsATotalBudgetAcrossPages()
    {
        // limit=3 with 2 rows on page 1: page 2 must be asked for the REMAINING 1 record, not 3.
        // More data remains after the budget is spent (non-empty cursor) => 206.
        var (repo, queries) = RepoOverPages(
            Page(Cursor("b"), Row("a"), Row("b")),
            Page(Cursor("c"), Row("c")));

        var result = await repo.ListAsync(Caller, limit: 3);

        Assert.Equal(new[] { "a", "b", "c" }, ItemsOf(result).Select(i => i.Id));
        Assert.Equal(206, result.StatusCode);
        Assert.Equal(2, queries.Count);
        Assert.Equal(3, queries[0].Limit);
        Assert.Equal(1, queries[1].Limit);
    }

    [Fact]
    public async Task ListAsync_LimitZero_MeansNoRecordCap_AndStillDrains()
    {
        // limit==0 previously made the loop condition (list.Count < limit) false, so even a fixed
        // cursor assignment would have stopped after one page. It must mean "no cap": no query
        // Limit is set and every page is read.
        var (repo, queries) = RepoOverPages(
            Page(Cursor("a"), Row("a")),
            Page(Cursor("b"), Row("b")),
            Page(null, Row("c")));

        var result = await repo.ListAsync(Caller, limit: 0);

        Assert.Equal(3, ItemsOf(result).Count);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(3, queries.Count);
        Assert.All(queries, q => Assert.True(q.Limit is null or 0));
    }
}
