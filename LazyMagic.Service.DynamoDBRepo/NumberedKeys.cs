#nullable enable
using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Threading;

namespace LazyMagic.Service.DynamoDBRepo;

/// <summary>
/// A sequence numbered keys are taken from: the counter's name, the prefix its keys carry, and how many numbers a
/// process reserves at a time.
///
/// <para>Two sequences with the same <see cref="Name"/> are one counter, whatever their prefixes: that is how records
/// of different types share numbers (a business <c>B-1042</c> and its party <c>P-1042</c>).</para>
///
/// <para><see cref="BlockSize"/> above 1 reserves that many numbers with one counter write and hands them out in
/// memory, for a writer that creates many records at once. Numbers a process reserved and did not use are never
/// handed out: they are gaps, like the number of a write that failed.</para>
/// </summary>
public sealed record NumberedKeySequence
{
    public NumberedKeySequence(string name, string prefix, int blockSize = 1)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Contains(':'))
            throw new ArgumentException("a sequence's name is non-empty and has no ':'", nameof(name));
        if (!NumberedKey.IsPrefix(prefix))
            throw new ArgumentException("a prefix is one to four upper-case letters A-Z", nameof(prefix));
        if (blockSize < 1)
            throw new ArgumentOutOfRangeException(nameof(blockSize), "a block holds at least one number");
        Name = name;
        Prefix = prefix;
        BlockSize = blockSize;
    }

    /// <summary>The counter: one row per name, per table.</summary>
    public string Name { get; }

    /// <summary>What a key from this sequence starts with: <c>B</c> in <c>B-1042</c>.</summary>
    public string Prefix { get; }

    /// <summary>Numbers reserved with each counter write.</summary>
    public int BlockSize { get; }

    /// <summary>The same counter and prefix, reserving <paramref name="blockSize"/> at a time.</summary>
    public NumberedKeySequence WithBlockSize(int blockSize) => new(Name, Prefix, blockSize);
}

/// <summary>
/// PURE. The form of a numbered key - <c>&lt;Prefix&gt;-&lt;number&gt;</c>, an upper-case prefix of one to four letters,
/// a hyphen, and a decimal number with no sign, no padding and no leading zero - and reading one back.
/// </summary>
public static class NumberedKey
{
    /// <summary>The canonical key: <c>Format("B", 1042)</c> is <c>B-1042</c>.</summary>
    public static string Format(string prefix, long number)
    {
        if (!IsPrefix(prefix))
            throw new ArgumentException("a prefix is one to four upper-case letters A-Z", nameof(prefix));
        if (number < 1)
            throw new ArgumentOutOfRangeException(nameof(number), "a numbered key's number is at least 1");
        return prefix + "-" + number.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>One to four upper-case ASCII letters.</summary>
    public static bool IsPrefix(string? prefix)
        => prefix is { Length: >= 1 and <= 4 } && prefix.All(c => c is >= 'A' and <= 'Z');

    /// <summary>
    /// Is <paramref name="key"/> a numbered key in its canonical form, exactly as <see cref="Format"/> writes it?
    /// For checking a stored id; a person's typing goes through <see cref="TryRead"/>.
    /// </summary>
    public static bool IsCanonical(string? key)
        => TryParseCanonical(key, out _, out _);

    /// <summary>The parts of a canonical key, or false.</summary>
    public static bool TryParseCanonical(string? key, out string prefix, out long number)
    {
        prefix = string.Empty;
        number = 0;
        if (string.IsNullOrEmpty(key))
            return false;
        var dash = key.IndexOf('-');
        if (dash < 1 || dash == key.Length - 1)
            return false;
        var head = key[..dash];
        var digits = key[(dash + 1)..];
        if (!IsPrefix(head) || digits[0] == '0' || !digits.All(char.IsAsciiDigit))
            return false;
        if (!long.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out number) || number < 1)
            return false;
        prefix = head;
        return true;
    }

    /// <summary>
    /// Reads what a person typed, forgivingly: case does not matter, spaces are dropped, and the hyphen may be missing -
    /// <c>b 1042</c>, <c>B1042</c> and <c>b-1042</c> are all <c>B-1042</c>. Leading zeros are refused rather than dropped,
    /// so <c>B-01042</c> is not quietly another record. False for anything else.
    /// </summary>
    public static bool TryRead(string? text, out string key)
    {
        key = string.Empty;
        if (string.IsNullOrWhiteSpace(text))
            return false;
        var cleaned = new StringBuilder(text.Length);
        foreach (var c in text.Trim())
            if (c != ' ')
                cleaned.Append(char.ToUpperInvariant(c));
        var s = cleaned.ToString();

        var letters = 0;
        while (letters < s.Length && s[letters] is >= 'A' and <= 'Z')
            letters++;
        if (letters == 0 || letters > 4)
            return false;
        var rest = s[letters..];
        if (rest.StartsWith('-'))
            rest = rest[1..];
        var candidate = s[..letters] + "-" + rest;
        if (!IsCanonical(candidate))
            return false;
        key = candidate;
        return true;
    }
}

/// <summary>
/// Hands out numbered keys. Register ONE per process (a singleton): it holds the blocks a process has reserved, and two
/// allocators in one process would each reserve their own.
/// </summary>
public interface INumberedKeyAllocator
{
    /// <summary>The next number of <paramref name="sequence"/> for the caller's table. Throws when the counter could not
    /// be written; nothing is handed out then.</summary>
    Task<long> NextNumberAsync(ICallerInfo callerInfo, NumberedKeySequence sequence, CancellationToken cancellationToken = default);

    /// <summary>The next key of <paramref name="sequence"/>: its prefix and <see cref="NextNumberAsync"/>.</summary>
    Task<string> NextKeyAsync(ICallerInfo callerInfo, NumberedKeySequence sequence, CancellationToken cancellationToken = default);
}

/// <summary>
/// The DynamoDB allocator. A sequence's counter is one row - <c>PK = LzCounter</c>, <c>SK = &lt;name&gt;:</c> - in the
/// table <see cref="CounterTable"/> names for the caller, incremented with <c>ADD</c>. The first number a sequence hands
/// out is <see cref="Start"/>, so an environment that starts at 100000 never hands out a number below it.
///
/// <para><b>ONE TABLE FOR EVERY SEQUENCE OF A CALLER</b>, by default its <c>DefaultDB</c>, whichever table the records
/// themselves live in: records in two tables can then share one sequence and still never be handed the same
/// number.</para>
///
/// <para><b>A NUMBER IS TAKEN BEFORE ITS RECORD IS WRITTEN</b>, and never handed back. A write that fails, or a block a
/// process did not finish, leaves a gap. Read a number as a name, never as a count.</para>
/// </summary>
public sealed class DYDBNumberedKeyAllocator : INumberedKeyAllocator
{
    /// <summary>The partition every counter row is written under.</summary>
    public const string CounterPartitionKey = "LzCounter";

    /// <summary>The attribute that holds a counter's value: how many numbers the sequence has handed out.</summary>
    public const string CounterAttribute = "next";

    private readonly IAmazonDynamoDB client;
    private readonly ConcurrentDictionary<string, Block> blocks = new(StringComparer.Ordinal);

    /// <param name="client">The DynamoDB client.</param>
    /// <param name="start">The first number every sequence hands out: an environment's range.</param>
    /// <param name="counterTable">The table a caller's counters live in; <c>DefaultDB</c> when null.</param>
    public DYDBNumberedKeyAllocator(IAmazonDynamoDB client, long start = 1, Func<ICallerInfo, string>? counterTable = null)
    {
        if (start < 1)
            throw new ArgumentOutOfRangeException(nameof(start), "a sequence's first number is at least 1");
        this.client = client ?? throw new ArgumentNullException(nameof(client));
        Start = start;
        CounterTable = counterTable ?? (callerInfo => callerInfo.DefaultDB ?? string.Empty);
    }

    /// <summary>The first number every sequence hands out.</summary>
    public long Start { get; }

    /// <summary>The table a caller's counters live in.</summary>
    public Func<ICallerInfo, string> CounterTable { get; }

    public async Task<string> NextKeyAsync(ICallerInfo callerInfo, NumberedKeySequence sequence, CancellationToken cancellationToken = default)
        => NumberedKey.Format(sequence.Prefix, await NextNumberAsync(callerInfo, sequence, cancellationToken));

    public async Task<long> NextNumberAsync(ICallerInfo callerInfo, NumberedKeySequence sequence, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(callerInfo);
        ArgumentNullException.ThrowIfNull(sequence);
        var table = CounterTable(callerInfo);
        if (string.IsNullOrWhiteSpace(table))
            throw new InvalidOperationException("the caller names no table for its counters");

        // A BLOCK PER TABLE AND COUNTER. Uniqueness does not rest on this - the counter is the one row every block is
        // reserved from, so two blocks never overlap, in one process or in two. Keying by the counter's name only makes
        // sequences that share a counter (B- and P-) draw on one block: fewer counter writes, and numbers handed out in
        // the order they were asked for.
        var block = blocks.GetOrAdd(table + "|" + sequence.Name, _ => new Block());
        await block.Gate.WaitAsync(cancellationToken);
        try
        {
            if (block.Next >= block.End)
            {
                var handedOut = await AddAsync(table, sequence.Name, sequence.BlockSize, cancellationToken);
                block.End = Start + handedOut;
                block.Next = block.End - sequence.BlockSize;
            }
            return block.Next++;
        }
        finally
        {
            block.Gate.Release();
        }
    }

    /// <summary>Adds <paramref name="count"/> to the counter and answers its new value: how many numbers the sequence has
    /// now handed out, these included. A counter that does not exist yet starts from zero.</summary>
    private async Task<long> AddAsync(string table, string name, int count, CancellationToken cancellationToken)
    {
        var response = await client.UpdateItemAsync(new UpdateItemRequest
        {
            TableName = table,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new AttributeValue { S = CounterPartitionKey },
                ["SK"] = new AttributeValue { S = name + ":" },
            },
            UpdateExpression = "ADD #next :count",
            ExpressionAttributeNames = new Dictionary<string, string> { ["#next"] = CounterAttribute },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":count"] = new AttributeValue { N = count.ToString(CultureInfo.InvariantCulture) },
            },
            ReturnValues = ReturnValue.UPDATED_NEW,
        }, cancellationToken);

        if (response?.Attributes == null
            || !response.Attributes.TryGetValue(CounterAttribute, out var value)
            || !long.TryParse(value.N, NumberStyles.None, CultureInfo.InvariantCulture, out var handedOut)
            || handedOut < count)
            throw new InvalidOperationException($"the counter {name} answered no value it could have been given");
        return handedOut;
    }

    private sealed class Block
    {
        public SemaphoreSlim Gate { get; } = new(1, 1);
        public long Next;
        public long End;
    }
}
