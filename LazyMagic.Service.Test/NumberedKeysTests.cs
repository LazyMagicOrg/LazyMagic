using System.Collections.Concurrent;
using Amazon.DynamoDBv2.Model;
using LazyMagic.Service.DynamoDBRepo;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace LazyMagic.Service.Test;

/// <summary>
/// Pure unit tests (mocked IAmazonDynamoDB) of numbered keys: the key's form, the allocator's counter, and a repository
/// that opts in with a sequence.
///
/// - A key is <c>Prefix-number</c>; a person's typing is read forgivingly, a stored id is checked exactly.
/// - The first number is the allocator's start, numbers are handed out once each - concurrently, in blocks, and across
///   two sequences sharing a counter - and a counter that could not be written hands out nothing.
/// - A numbered repository's create with no id takes the next key; one with an id keeps it; one without an allocator
///   fails closed rather than writing a GUID; and a write that fails leaves a gap.
/// </summary>
public class NumberedKeysTests
{
    private const string Table = "unit-test-table";

    private static readonly ICallerInfo Caller = new CallerInfo
    {
        DefaultDB = Table,
        LzUserId = "unit-test-user",
        TenantId = "unit-test-tenant",
        SessionId = "unit-test-session",
    };

    private static readonly NumberedKeySequence Business = new("account", "B");
    private static readonly NumberedKeySequence Party = new("account", "P");

    /// <summary>A client whose counters live in memory: UpdateItem ADDs, PutItem records or fails as scripted.</summary>
    private sealed class Client
    {
        public Mock<IAmazonDynamoDB> Mock { get; } = new(MockBehavior.Strict);
        public ConcurrentDictionary<string, long> Counters { get; } = new();
        public ConcurrentQueue<UpdateItemRequest> Updates { get; } = new();
        public ConcurrentQueue<PutItemRequest> Puts { get; } = new();
        public Queue<Exception?> PutAnswers { get; } = new();
        public bool CounterFails { get; set; }

        public Client()
        {
            Mock.Setup(c => c.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), It.IsAny<CancellationToken>()))
                .Returns((UpdateItemRequest r, CancellationToken _) =>
                {
                    Updates.Enqueue(r);
                    if (CounterFails)
                        throw new AmazonDynamoDBException("throttled");
                    var add = long.Parse(r.ExpressionAttributeValues[":count"].N);
                    var key = r.TableName + "|" + r.Key["PK"].S + "|" + r.Key["SK"].S;
                    var value = Counters.AddOrUpdate(key, add, (_, old) => old + add);
                    return Task.FromResult(new UpdateItemResponse
                    {
                        Attributes = new Dictionary<string, AttributeValue>
                        {
                            [DYDBNumberedKeyAllocator.CounterAttribute] = new() { N = value.ToString() },
                        },
                    });
                });
            Mock.Setup(c => c.PutItemAsync(It.IsAny<PutItemRequest>(), It.IsAny<CancellationToken>()))
                .Returns((PutItemRequest r, CancellationToken _) =>
                {
                    Puts.Enqueue(r);
                    var answer = PutAnswers.Count > 0 ? PutAnswers.Dequeue() : null;
                    if (answer != null)
                        throw answer;
                    return Task.FromResult(new PutItemResponse());
                });
        }
    }

    /// <summary>A repository numbered from <see cref="Business"/>, in the level's GSI table.</summary>
    private sealed class NumberedRepo : TestItemRepo
    {
        public NumberedRepo(IAmazonDynamoDB client) : base(client) { }
        protected override TableKind TableKind => TableKind.Gsi;
        protected override NumberedKeySequence? KeySequence => Business;
    }

    // ---------------------------------------------------------------------------------------------- the key's form

    [Theory]
    [InlineData("B", 1042, "B-1042")]
    [InlineData("SA", 100000, "SA-100000")]
    [InlineData("ABCD", 1, "ABCD-1")]
    public void Format_writes_prefix_hyphen_number(string prefix, long number, string expected)
        => Assert.Equal(expected, NumberedKey.Format(prefix, number));

    [Theory]
    [InlineData("b", 1)]
    [InlineData("", 1)]
    [InlineData("ABCDE", 1)]
    [InlineData("B1", 1)]
    [InlineData("B", 0)]
    [InlineData("B", -3)]
    public void Format_refuses_a_bad_prefix_or_number(string prefix, long number)
        => Assert.ThrowsAny<ArgumentException>(() => NumberedKey.Format(prefix, number));

    [Theory]
    [InlineData("B-1042", true)]
    [InlineData("SA-7", true)]
    [InlineData("b-1042", false)]
    [InlineData("B1042", false)]
    [InlineData("B-01042", false)]
    [InlineData("B-0", false)]
    [InlineData("B-", false)]
    [InlineData("-1042", false)]
    [InlineData("B-10a", false)]
    [InlineData("B-1042:", false)]
    [InlineData("3f9a1c2e-0000-4000-8000-000000000000", false)]
    [InlineData("regbiz-3f9a", false)]
    [InlineData(null, false)]
    public void IsCanonical_is_exactly_what_Format_writes(string? key, bool expected)
        => Assert.Equal(expected, NumberedKey.IsCanonical(key));

    [Theory]
    [InlineData("B-1042", "B-1042")]
    [InlineData("b-1042", "B-1042")]
    [InlineData("B1042", "B-1042")]
    [InlineData(" b 1042 ", "B-1042")]
    [InlineData("sa 7", "SA-7")]
    [InlineData("Sa-100000", "SA-100000")]
    public void TryRead_forgives_case_spaces_and_a_missing_hyphen(string typed, string expected)
    {
        Assert.True(NumberedKey.TryRead(typed, out var key));
        Assert.Equal(expected, key);
    }

    [Theory]
    [InlineData("B-01042")]
    [InlineData("B--1042")]
    [InlineData("1042")]
    [InlineData("ABCDE-1")]
    [InlineData("B-")]
    [InlineData("B-1O42")]
    [InlineData("3f9a1c2e-0000-4000-8000-000000000000")]
    [InlineData("")]
    [InlineData(null)]
    public void TryRead_refuses_anything_else(string? typed)
        => Assert.False(NumberedKey.TryRead(typed, out _));

    [Theory]
    [InlineData("", "B")]
    [InlineData("a:b", "B")]
    [InlineData("account", "b")]
    [InlineData("account", "")]
    public void A_sequence_refuses_a_bad_name_or_prefix(string name, string prefix)
        => Assert.ThrowsAny<ArgumentException>(() => new NumberedKeySequence(name, prefix));

    // ------------------------------------------------------------------------------------------------ the allocator

    [Fact]
    public async Task The_first_number_is_the_start_and_the_counter_is_one_row_in_the_callers_default_table()
    {
        var client = new Client();
        var allocator = new DYDBNumberedKeyAllocator(client.Mock.Object, start: 100000);

        Assert.Equal("B-100000", await allocator.NextKeyAsync(Caller, Business));
        Assert.Equal("B-100001", await allocator.NextKeyAsync(Caller, Business));

        var update = Assert.Single(client.Updates.Take(1));
        Assert.Equal(Table, update.TableName);
        Assert.Equal(DYDBNumberedKeyAllocator.CounterPartitionKey, update.Key["PK"].S);
        Assert.Equal("account:", update.Key["SK"].S);
        Assert.Equal("ADD #next :count", update.UpdateExpression);
        Assert.Equal(ReturnValue.UPDATED_NEW, update.ReturnValues);
        Assert.Equal(2, client.Updates.Count);
    }

    [Fact]
    public async Task Concurrent_callers_are_each_handed_a_different_number_with_none_skipped()
    {
        var client = new Client();
        var allocator = new DYDBNumberedKeyAllocator(client.Mock.Object, start: 1);

        var numbers = await Task.WhenAll(Enumerable.Range(0, 200)
            .Select(_ => Task.Run(() => allocator.NextNumberAsync(Caller, Business))));

        Assert.Equal(Enumerable.Range(1, 200).Select(i => (long)i), numbers.OrderBy(n => n));
    }

    [Fact]
    public async Task A_block_is_reserved_with_one_counter_write_and_handed_out_in_order()
    {
        var client = new Client();
        var allocator = new DYDBNumberedKeyAllocator(client.Mock.Object, start: 100000);
        var bulk = Business.WithBlockSize(100);

        var numbers = new List<long>();
        for (var i = 0; i < 250; i++)
            numbers.Add(await allocator.NextNumberAsync(Caller, bulk));

        Assert.Equal(Enumerable.Range(100000, 250).Select(i => (long)i), numbers);
        Assert.Equal(3, client.Updates.Count);
        Assert.All(client.Updates, u => Assert.Equal("100", u.ExpressionAttributeValues[":count"].N));
    }

    [Fact]
    public async Task Two_processes_each_reserving_a_block_never_hand_out_the_same_number()
    {
        var client = new Client();
        var first = new DYDBNumberedKeyAllocator(client.Mock.Object, start: 1);
        var second = new DYDBNumberedKeyAllocator(client.Mock.Object, start: 1);
        var bulk = Business.WithBlockSize(10);

        var handed = new List<long>();
        for (var i = 0; i < 15; i++)
        {
            handed.Add(await first.NextNumberAsync(Caller, bulk));
            handed.Add(await second.NextNumberAsync(Caller, bulk));
        }

        Assert.Equal(handed.Count, handed.Distinct().Count());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    public async Task Sequences_sharing_a_counter_share_its_numbers_in_order(int blockSize)
    {
        var client = new Client();
        var allocator = new DYDBNumberedKeyAllocator(client.Mock.Object, start: 100000);

        Assert.Equal("B-100000", await allocator.NextKeyAsync(Caller, Business.WithBlockSize(blockSize)));
        Assert.Equal("P-100001", await allocator.NextKeyAsync(Caller, Party.WithBlockSize(blockSize)));
        Assert.Equal("B-100002", await allocator.NextKeyAsync(Caller, Business.WithBlockSize(blockSize)));
    }

    [Fact]
    public async Task Different_counters_and_different_tables_count_on_their_own()
    {
        var client = new Client();
        var allocator = new DYDBNumberedKeyAllocator(client.Mock.Object, start: 100000);
        var requests = new NumberedKeySequence("request", "R");
        var other = new CallerInfo { DefaultDB = "other-table", LzUserId = "u", TenantId = "t", SessionId = "s" };

        Assert.Equal("B-100000", await allocator.NextKeyAsync(Caller, Business));
        Assert.Equal("R-100000", await allocator.NextKeyAsync(Caller, requests));
        Assert.Equal("B-100000", await allocator.NextKeyAsync(other, Business));
        Assert.Equal("B-100001", await allocator.NextKeyAsync(Caller, Business));
    }

    [Fact]
    public async Task A_counter_that_cannot_be_written_hands_out_nothing_and_the_next_call_carries_on()
    {
        var client = new Client();
        var allocator = new DYDBNumberedKeyAllocator(client.Mock.Object, start: 100000);
        Assert.Equal(100000, await allocator.NextNumberAsync(Caller, Business));

        client.CounterFails = true;
        await Assert.ThrowsAsync<AmazonDynamoDBException>(() => allocator.NextNumberAsync(Caller, Business));

        client.CounterFails = false;
        Assert.Equal(100001, await allocator.NextNumberAsync(Caller, Business));
    }

    [Fact]
    public async Task A_counter_table_can_be_named_for_the_caller()
    {
        var client = new Client();
        var allocator = new DYDBNumberedKeyAllocator(client.Mock.Object, start: 1, counterTable: c => c.TenantId + "_counters");

        await allocator.NextKeyAsync(Caller, Business);

        Assert.Equal("unit-test-tenant_counters", Assert.Single(client.Updates).TableName);
    }

    [Fact]
    public void The_start_is_at_least_one()
        => Assert.Throws<ArgumentOutOfRangeException>(() => new DYDBNumberedKeyAllocator(new Client().Mock.Object, start: 0));

    // ------------------------------------------------------------------------------------------------ the repository

    [Fact]
    public async Task A_create_with_no_id_takes_the_next_key_and_is_written_under_it()
    {
        var client = new Client();
        var repo = new NumberedRepo(client.Mock.Object) { KeyAllocator = new DYDBNumberedKeyAllocator(client.Mock.Object, start: 100000) };

        var created = await repo.CreateAsync(Caller, new TestItem { Name = "Mesa Solar" });

        Assert.Equal("B-100000", created.Value?.Id);
        var put = Assert.Single(client.Puts);
        Assert.Equal("B-100000:", put.Item["SK"].S);
        Assert.Equal(Table + "_gsi", put.TableName);
        Assert.Equal(Table, Assert.Single(client.Updates).TableName);   // the counter is in the default table
    }

    [Fact]
    public async Task A_create_that_carries_an_id_keeps_it_and_takes_no_number()
    {
        var client = new Client();
        var repo = new NumberedRepo(client.Mock.Object) { KeyAllocator = new DYDBNumberedKeyAllocator(client.Mock.Object, start: 100000) };

        var created = await repo.CreateAsync(Caller, new TestItem { Id = "P-100042", Name = "a party" });

        Assert.Equal("P-100042", created.Value?.Id);
        Assert.Empty(client.Updates);
    }

    [Fact]
    public async Task A_numbered_repository_with_no_allocator_fails_closed_and_writes_nothing()
    {
        var client = new Client();
        var repo = new NumberedRepo(client.Mock.Object);

        var created = await repo.CreateAsync(Caller, new TestItem { Name = "Mesa Solar" });

        Assert.Null(created.Value);
        Assert.Equal(500, Assert.IsAssignableFrom<ObjectResult>(created.Result).StatusCode);
        Assert.Empty(client.Puts);
    }

    [Fact]
    public async Task A_counter_that_cannot_be_written_is_503_and_nothing_is_written()
    {
        var client = new Client { CounterFails = true };
        var repo = new NumberedRepo(client.Mock.Object) { KeyAllocator = new DYDBNumberedKeyAllocator(client.Mock.Object, start: 100000) };

        var created = await repo.CreateAsync(Caller, new TestItem { Name = "Mesa Solar" });

        Assert.Null(created.Value);
        Assert.Equal(503, Assert.IsAssignableFrom<StatusCodeResult>(created.Result).StatusCode);
        Assert.Empty(client.Puts);
    }

    [Fact]
    public async Task A_write_that_fails_leaves_a_gap_and_the_next_create_takes_the_next_number()
    {
        var client = new Client();
        client.PutAnswers.Enqueue(new AmazonDynamoDBException("throttled"));
        var repo = new NumberedRepo(client.Mock.Object) { KeyAllocator = new DYDBNumberedKeyAllocator(client.Mock.Object, start: 100000) };

        var failed = await repo.CreateAsync(Caller, new TestItem { Name = "first" });
        var created = await repo.CreateAsync(Caller, new TestItem { Name = "second" });

        Assert.Null(failed.Value);
        Assert.Equal("B-100001", created.Value?.Id);
    }

    [Fact]
    public async Task A_repository_with_no_sequence_still_writes_a_guid_and_takes_no_number()
    {
        var client = new Client();
        var repo = new TestItemRepo(client.Mock.Object) { KeyAllocator = new DYDBNumberedKeyAllocator(client.Mock.Object, start: 100000) };

        var created = await repo.CreateAsync(Caller, new TestItem { Name = "plain" });

        Assert.True(Guid.TryParse(created.Value?.Id, out _));
        Assert.Empty(client.Updates);
    }

    [Fact]
    public async Task NextKeyAsync_hands_a_key_to_a_caller_that_needs_it_before_the_write()
    {
        var client = new Client();
        var repo = new NumberedRepo(client.Mock.Object) { KeyAllocator = new DYDBNumberedKeyAllocator(client.Mock.Object, start: 100000) };

        Assert.Equal("B-100000", (await repo.NextKeyAsync(Caller)).Value);
        Assert.Equal(500, Assert.IsAssignableFrom<ObjectResult>((await new TestItemRepo(client.Mock.Object).NextKeyAsync(Caller)).Result).StatusCode);
    }
}
