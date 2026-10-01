using Amazon.DynamoDBv2.Model;
using LazyMagic.Service.DynamoDBRepo;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace LazyMagic.Service.Test;

/// <summary>
/// Pure unit tests (mocked IAmazonDynamoDB) of DYDBTransaction: writes that land together or not at all.
///
/// - Each action is the item the repo's own write would store, under that write's condition: a create refuses an
///   existing item, an update refuses an item written since it was read, and a check holds or refuses.
/// - A cancellation is read by its reasons: a failed condition is a refusal naming the action (409), a conflict or a
///   throttle passes (503) and is retried with the same token, and a malformed request is 400.
/// - Mistakes DynamoDB would report without saying which item - the same item twice, two clients, a 101st action -
///   are refused before anything is sent.
/// - A single-item write that collides with a transaction in flight is 503, not "bad request".
/// </summary>
public class DYDBTransactionTests
{
    private const string Table = "unit-test-table";

    private static readonly ICallerInfo Caller = new CallerInfo
    {
        DefaultDB = Table,
        LzUserId = "unit-test-user",
        TenantId = "unit-test-tenant",
        SessionId = "unit-test-session",
    };

    private sealed class TxRepo : TestItemRepo
    {
        private readonly TableKind kind;

        public TxRepo(IAmazonDynamoDB client, TableKind kind = TableKind.Gsi, bool softDelete = false, bool notify = false)
            : base(client)
        {
            this.kind = kind;
            UseSoftDelete = softDelete;
            UseNotifications = notify;
        }

        protected override TableKind TableKind => kind;

        public List<(string Sk, string Action)> Notified { get; } = new();

        protected override Task WriteNotificationAsync(ICallerInfo callerInfo, Dictionary<string, AttributeValue> dbrecord, string action)
        {
            Notified.Add((dbrecord["SK"].S, action));
            return Task.CompletedTask;
        }
    }

    /// <summary>A client whose TransactWriteItems answers from a script, recording each request.</summary>
    private sealed class Client
    {
        public Mock<IAmazonDynamoDB> Mock { get; } = new(MockBehavior.Loose);
        public List<TransactWriteItemsRequest> Sent { get; } = new();
        public Queue<Exception?> Answers { get; } = new();

        public Client()
        {
            Mock.Setup(c => c.TransactWriteItemsAsync(It.IsAny<TransactWriteItemsRequest>(), It.IsAny<CancellationToken>()))
                .Returns((TransactWriteItemsRequest r, CancellationToken _) =>
                {
                    Sent.Add(r);
                    var answer = Answers.Count > 0 ? Answers.Dequeue() : null;
                    return answer is null
                        ? Task.FromResult(new TransactWriteItemsResponse())
                        : Task.FromException<TransactWriteItemsResponse>(answer);
                });
        }

        public TxRepo Repo(TableKind kind = TableKind.Gsi, bool softDelete = false, bool notify = false)
            => new(Mock.Object, kind, softDelete, notify);
    }

    private static TestItem Item(string id, long updateUtcTick = 0)
        => new() { Id = id, Name = $"name-{id}", Description = "d", UpdateUtcTick = updateUtcTick };

    private static TransactionCanceledException Cancelled(params string[] codes)
        => new("cancelled") { CancellationReasons = codes.Select(c => new CancellationReason { Code = c }).ToList() };

    // ---- WHAT IT SENDS -------------------------------------------------------------------------

    [Fact]
    public async Task ACreate_IsTheItemCreateAsyncWouldStore_UnderTheCreateCondition()
    {
        var client = new Client();
        PutItemRequest? single = null;
        client.Mock.Setup(c => c.PutItemAsync(It.IsAny<PutItemRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PutItemRequest, CancellationToken>((r, _) => single = r)
            .ReturnsAsync(new PutItemResponse());
        var repo = client.Repo();
        await repo.CreateAsync(Caller, Item("a"));

        var tx = new DYDBTransaction();
        tx.Create(repo, Caller, Item("a"));
        await tx.CommitAsync();

        var put = Assert.Single(client.Sent).TransactItems.Single().Put;
        Assert.NotNull(single);
        Assert.Equal(single!.TableName, put.TableName);
        Assert.Equal("unit-test-table_gsi", put.TableName);
        Assert.Equal(single.ConditionExpression, put.ConditionExpression);
        Assert.Equal("attribute_not_exists(PK)", put.ConditionExpression);
        Assert.Equal(single.Item.Keys.OrderBy(k => k), put.Item.Keys.OrderBy(k => k));
        foreach (var attribute in new[] { "PK", "SK", "SK1" })
            Assert.Equal(single.Item[attribute].S, put.Item[attribute].S);
    }

    [Fact]
    public async Task AnUpdate_IsRefusedIfTheItemWasWrittenSinceItWasRead()
    {
        var client = new Client();
        var tx = new DYDBTransaction();
        tx.Update(client.Repo(), Caller, Item("a", updateUtcTick: 42));
        await tx.CommitAsync();

        var put = client.Sent.Single().TransactItems.Single().Put;
        Assert.Equal("UpdateUtcTick = :OldUpdateUtcTick", put.ConditionExpression);
        Assert.Equal("42", put.ExpressionAttributeValues[":OldUpdateUtcTick"].N);
        Assert.NotEqual("42", put.Item["UpdateUtcTick"].N);
    }

    [Fact]
    public async Task AForcedUpdate_WritesUnconditionally_AsUpdateAsyncDoes()
    {
        var client = new Client();
        var tx = new DYDBTransaction();
        tx.Update(client.Repo(), Caller, Item("a", updateUtcTick: 42), forceUpdate: true);
        await tx.CommitAsync();

        var put = client.Sent.Single().TransactItems.Single().Put;
        Assert.True(string.IsNullOrEmpty(put.ConditionExpression));
    }

    [Fact]
    public async Task DeletesAndChecks_AddressTheItemByItsKey()
    {
        var client = new Client();
        var repo = client.Repo();
        var tx = new DYDBTransaction();
        tx.Delete(repo, Caller, "gone");
        tx.RequireExists(repo, Caller, "there");
        tx.RequireAbsent(repo, Caller, "free");
        await tx.CommitAsync();

        var items = client.Sent.Single().TransactItems;
        Assert.Equal("gone:", items[0].Delete.Key["SK"].S);
        Assert.Equal("TestItem:", items[0].Delete.Key["PK"].S);
        Assert.Equal("unit-test-table_gsi", items[0].Delete.TableName);
        Assert.Equal("there:", items[1].ConditionCheck.Key["SK"].S);
        Assert.Equal("attribute_exists(PK)", items[1].ConditionCheck.ConditionExpression);
        Assert.Equal("free:", items[2].ConditionCheck.Key["SK"].S);
        Assert.Equal("attribute_not_exists(PK)", items[2].ConditionCheck.ConditionExpression);
    }

    [Fact]
    public async Task OneTransaction_SpansBothTablesOfALevel()
    {
        var client = new Client();
        var tx = new DYDBTransaction();
        tx.Create(client.Repo(TableKind.Lsi), Caller, Item("a"));
        tx.Create(client.Repo(TableKind.Gsi), Caller, Item("b"));
        await tx.CommitAsync();

        Assert.Equal(new[] { "unit-test-table", "unit-test-table_gsi" },
            client.Sent.Single().TransactItems.Select(i => i.Put.TableName));
    }

    [Fact]
    public void ASoftDeletingRepo_CannotDeleteInATransaction()
    {
        var client = new Client();
        Assert.Throws<InvalidOperationException>(
            () => new DYDBTransaction().Delete(client.Repo(softDelete: true), Caller, "a"));
    }

    // ---- WHAT IT ANSWERS -----------------------------------------------------------------------

    [Fact]
    public async Task Committed_SetsEachWrittenItem_WithItsNewTicks()
    {
        var client = new Client();
        var tx = new DYDBTransaction();
        var a = tx.Create(client.Repo(), Caller, Item("a"));
        var b = tx.Update(client.Repo(), Caller, Item("b", updateUtcTick: 42));
        Assert.Null(a.Value);

        var result = await tx.CommitAsync();

        Assert.True(result.Committed);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal("a", a.Value!.Id);
        Assert.NotEqual(0, a.Value.CreateUtcTick);
        Assert.Equal("b", b.Value!.Id);
        Assert.NotEqual(42, b.Value.UpdateUtcTick);
        Assert.Equal(a.Value.UpdateUtcTick, b.Value.UpdateUtcTick); // one transaction, one moment
    }

    [Fact]
    public async Task AFailedCondition_IsARefusal_NamingTheFirstActionThatFailed()
    {
        var client = new Client();
        client.Answers.Enqueue(Cancelled("None", "ConditionalCheckFailed", "ConditionalCheckFailed"));
        var tx = new DYDBTransaction();
        var party = tx.Create(client.Repo(), Caller, Item("party"), label: "party");
        var pointer = tx.Create(client.Repo(), Caller, Item("pointer"), label: "pointer");
        tx.RequireAbsent(client.Repo(), Caller, "other");

        var result = await tx.CommitAsync();

        Assert.Equal(TransactionOutcome.Refused, result.Outcome);
        Assert.Equal(409, result.StatusCode);
        Assert.Same(pointer, result.RefusedBy);
        Assert.Null(party.Value);
        Assert.Null(pointer.Value);
    }

    [Fact]
    public async Task ARefusedTransaction_IsSpent()
    {
        var client = new Client();
        client.Answers.Enqueue(Cancelled("ConditionalCheckFailed"));
        var tx = new DYDBTransaction();
        tx.Create(client.Repo(), Caller, Item("a"));
        await tx.CommitAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => tx.CommitAsync());
        Assert.Throws<InvalidOperationException>(() => tx.Create(client.Repo(), Caller, Item("b")));
    }

    [Theory]
    [InlineData("TransactionConflict")]
    [InlineData("ThrottlingError")]
    [InlineData("ProvisionedThroughputExceeded")]
    public async Task AConflictOrThrottle_Passes_AndTheRetryResendsTheSameToken(string code)
    {
        var client = new Client();
        client.Answers.Enqueue(Cancelled("None", code));
        var tx = new DYDBTransaction();
        var a = tx.Create(client.Repo(), Caller, Item("a"));
        tx.Create(client.Repo(), Caller, Item("b"));

        var first = await tx.CommitAsync();
        Assert.Equal(TransactionOutcome.Unavailable, first.Outcome);
        Assert.Equal(503, first.StatusCode);
        Assert.Null(a.Value);

        var second = await tx.CommitAsync();
        Assert.True(second.Committed);
        Assert.Equal(2, client.Sent.Count);
        Assert.Equal(client.Sent[0].ClientRequestToken, client.Sent[1].ClientRequestToken);
        Assert.False(string.IsNullOrEmpty(client.Sent[0].ClientRequestToken));
        Assert.True(client.Sent[0].ClientRequestToken.Length <= 36);
        Assert.Equal(client.Sent[0].TransactItems[0].Put.Item["UpdateUtcTick"].N,
            client.Sent[1].TransactItems[0].Put.Item["UpdateUtcTick"].N); // the same items, not rebuilt
        Assert.NotNull(a.Value);
    }

    [Fact]
    public async Task AConditionFailure_OutranksAConflictInTheSameCancellation()
    {
        var client = new Client();
        client.Answers.Enqueue(Cancelled("TransactionConflict", "ConditionalCheckFailed"));
        var tx = new DYDBTransaction();
        tx.Create(client.Repo(), Caller, Item("a"));
        var b = tx.Create(client.Repo(), Caller, Item("b"));

        var result = await tx.CommitAsync();

        Assert.Equal(TransactionOutcome.Refused, result.Outcome);
        Assert.Same(b, result.RefusedBy);
    }

    [Fact]
    public async Task TheSameTransactionStillInFlight_Passes()
    {
        var client = new Client();
        client.Answers.Enqueue(new TransactionInProgressException("in progress"));
        var tx = new DYDBTransaction();
        tx.Create(client.Repo(), Caller, Item("a"));

        Assert.Equal(TransactionOutcome.Unavailable, (await tx.CommitAsync()).Outcome);
    }

    [Fact]
    public async Task AMalformedRequest_IsInvalid_AndSpent()
    {
        var client = new Client();
        client.Answers.Enqueue(Cancelled("ValidationError"));
        var tx = new DYDBTransaction();
        tx.Create(client.Repo(), Caller, Item("a"));

        var result = await tx.CommitAsync();

        Assert.Equal(TransactionOutcome.Invalid, result.Outcome);
        Assert.Equal(400, result.StatusCode);
        await Assert.ThrowsAsync<InvalidOperationException>(() => tx.CommitAsync());
    }

    [Fact]
    public async Task AnUnknownFailure_IsFailed_AndMayBeCommittedAgain()
    {
        var client = new Client();
        client.Answers.Enqueue(new HttpRequestException("connection refused"));
        var tx = new DYDBTransaction();
        tx.Create(client.Repo(), Caller, Item("a"));

        var result = await tx.CommitAsync();

        Assert.Equal(TransactionOutcome.Failed, result.Outcome);
        Assert.Equal(500, result.StatusCode);
        Assert.True((await tx.CommitAsync()).Committed);
    }

    [Fact]
    public async Task Notifications_FollowTheCommit_AndNeverARefusal()
    {
        var client = new Client();
        var repo = client.Repo(notify: true);
        client.Answers.Enqueue(Cancelled("ConditionalCheckFailed", "None"));
        var refused = new DYDBTransaction();
        refused.Create(repo, Caller, Item("a"));
        refused.Update(repo, Caller, Item("b", 7));
        await refused.CommitAsync();
        Assert.Empty(repo.Notified);

        var tx = new DYDBTransaction();
        tx.Create(repo, Caller, Item("a"));
        tx.Update(repo, Caller, Item("b", 7));
        await tx.CommitAsync();

        Assert.Equal(new[] { ("a:", "Create"), ("b:", "Update") }, repo.Notified);
    }

    // ---- MISTAKES, REFUSED BEFORE ANYTHING IS SENT --------------------------------------------

    [Fact]
    public void TheSameItemTwice_IsRefused_NamingIt()
    {
        var client = new Client();
        var tx = new DYDBTransaction();
        tx.Create(client.Repo(), Caller, Item("a"));

        var ex = Assert.Throws<InvalidOperationException>(() => tx.RequireAbsent(client.Repo(), Caller, "a"));
        Assert.Contains("a:", ex.Message);
        Assert.Equal(1, tx.Count);
    }

    [Fact]
    public void TheSameIdInTheOtherTable_IsAnotherItem()
    {
        var client = new Client();
        var tx = new DYDBTransaction();
        tx.Create(client.Repo(TableKind.Lsi), Caller, Item("a"));
        tx.Create(client.Repo(TableKind.Gsi), Caller, Item("a"));
        Assert.Equal(2, tx.Count);
    }

    [Fact]
    public void ReposOnTwoClients_CannotShareATransaction()
    {
        var tx = new DYDBTransaction();
        tx.Create(new Client().Repo(), Caller, Item("a"));
        Assert.Throws<InvalidOperationException>(() => tx.Create(new Client().Repo(), Caller, Item("b")));
    }

    [Fact]
    public void AHundredActions_IsTheLimit()
    {
        var client = new Client();
        var repo = client.Repo();
        var tx = new DYDBTransaction();
        for (var i = 0; i < DYDBTransaction.MaxActions; i++)
            tx.RequireExists(repo, Caller, $"item-{i}");

        Assert.Throws<InvalidOperationException>(() => tx.RequireExists(repo, Caller, "one-more"));
    }

    [Fact]
    public void ARepoThatIsNotDynamoDB_CannotTakePart()
    {
        var fake = new Mock<IDocumentRepo<TestItem>>().Object;
        Assert.Throws<ArgumentException>(() => new DYDBTransaction().Create(fake, Caller, Item("a")));
    }

    [Fact]
    public async Task AnEmptyTransaction_IsAMistake()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => new DYDBTransaction().CommitAsync());
    }

    // ---- SINGLE-ITEM WRITES THAT MEET A TRANSACTION -------------------------------------------

    [Fact]
    public async Task ACreate_MeetingATransactionInFlight_Is503()
    {
        var client = new Client();
        client.Mock.Setup(c => c.PutItemAsync(It.IsAny<PutItemRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TransactionConflictException("conflict"));

        var result = await client.Repo().CreateAsync(Caller, Item("a"));

        Assert.Equal(503, Assert.IsType<StatusCodeResult>(result.Result).StatusCode);
    }

    [Fact]
    public async Task AnUpdate_MeetingATransactionInFlight_Is503()
    {
        var client = new Client();
        client.Mock.Setup(c => c.PutItemAsync(It.IsAny<PutItemRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TransactionConflictException("conflict"));

        var result = await client.Repo().UpdateAsync(Caller, Item("a", 42));

        Assert.Equal(503, Assert.IsType<StatusCodeResult>(result.Result).StatusCode);
    }

    [Fact]
    public async Task ADelete_MeetingATransactionInFlight_Is503()
    {
        var client = new Client();
        client.Mock.Setup(c => c.DeleteItemAsync(It.IsAny<DeleteItemRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TransactionConflictException("conflict"));

        var result = await client.Repo().DeleteAsync(Caller, "a");

        Assert.Equal(503, result.StatusCode);
    }

    [Fact]
    public async Task ACreate_ThatFindsTheItem_IsStill409()
    {
        var client = new Client();
        client.Mock.Setup(c => c.PutItemAsync(It.IsAny<PutItemRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConditionalCheckFailedException("exists"));

        var result = await client.Repo().CreateAsync(Caller, Item("a"));

        Assert.IsType<ConflictResult>(result.Result);
    }
}
