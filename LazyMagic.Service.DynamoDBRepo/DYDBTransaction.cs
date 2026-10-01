#nullable enable
using System.Threading;

namespace LazyMagic.Service.DynamoDBRepo;

/// <summary>One action of a <see cref="DYDBTransaction"/>, in the order it was added.</summary>
public class TxAction
{
    internal TxAction(int index, string? label) { Index = index; Label = label; }

    /// <summary>Its place in the transaction, from 0: DynamoDB reports a cancellation's reasons in this order.</summary>
    public int Index { get; }

    /// <summary>What the caller called it, for a refusal it reports.</summary>
    public string? Label { get; }

    public override string ToString() => Label ?? $"action {Index}";
}

/// <summary>A create or an update of a transaction: <see cref="Value"/> is the item as stored, once it commits.</summary>
public sealed class TxItem<T> : TxAction where T : class
{
    internal TxItem(int index, string? label) : base(index, label) { }

    /// <summary>The item the transaction wrote, with its new ticks - what CreateAsync or UpdateAsync would return.
    /// Null until the transaction commits.</summary>
    public T? Value { get; internal set; }
}

public enum TransactionOutcome
{
    /// <summary>Every action was written.</summary>
    Committed,

    /// <summary>A condition failed, so nothing was written: an item to create exists, an item to update was written
    /// since it was read, or a check did not hold. <see cref="TransactionResult.RefusedBy"/> names the first such
    /// action. 409, as CreateAsync and UpdateAsync answer one item's.</summary>
    Refused,

    /// <summary>Nothing was written, for a reason that passes: another transaction held an item, or DynamoDB
    /// throttled. 503. Committing the same transaction again is safe - it resends its own token.</summary>
    Unavailable,

    /// <summary>DynamoDB refused the request itself: a malformed item, more than 4 MB, a token reused for another
    /// request. 400. Trying again does not help.</summary>
    Invalid,

    /// <summary>Anything else. 500. Whether it was written is unknown, so committing it again - with its token -
    /// is how a caller finds out.</summary>
    Failed
}

public sealed class TransactionResult
{
    internal TransactionResult(TransactionOutcome outcome, TxAction? refusedBy = null, string? detail = null)
    { Outcome = outcome; RefusedBy = refusedBy; Detail = detail; }

    public TransactionOutcome Outcome { get; }

    public bool Committed => Outcome == TransactionOutcome.Committed;

    /// <summary>The first action whose condition failed, when <see cref="Outcome"/> is Refused.</summary>
    public TxAction? RefusedBy { get; }

    /// <summary>What DynamoDB said, for a log. Never shown to a caller: it may quote an item.</summary>
    public string? Detail { get; }

    /// <summary>The status a single-item write would have answered: 200, 409, 503, 400 or 500.</summary>
    public int StatusCode => Outcome switch
    {
        TransactionOutcome.Committed => 200,
        TransactionOutcome.Refused => 409,
        TransactionOutcome.Unavailable => 503,
        TransactionOutcome.Invalid => 400,
        _ => 500
    };

    public override string ToString()
        => Outcome == TransactionOutcome.Refused ? $"Refused by {RefusedBy}" : Outcome.ToString();
}

/// <summary>
/// WRITES THAT LAND TOGETHER OR NOT AT ALL, across repos and tables: one DynamoDB TransactWriteItems.
///
/// <para>Each repo builds its own item, exactly as its CreateAsync or UpdateAsync would (DYDBRepository.BuildRecord),
/// under the same condition: a create refuses an existing item, an update refuses an item written since it was read.
/// So an entity and the pointer item that finds it by key - a login's party, a business's party - are written as one,
/// and a second writer racing on the pointer is refused with nothing written, rather than leaving an entity no
/// pointer reaches. A count and the entry it counts are written the same way.</para>
///
/// <para><b>What it does not do.</b> It runs no logic a repo's CreateAsync or UpdateAsync OVERRIDE adds - validation,
/// derived fields: the caller applies those first. It reads nothing: an update writes what the caller read, and the
/// condition refuses it if that is stale. And it notifies (UseNotifications) only after the commit.</para>
///
/// <para><b>DynamoDB's limits</b>: at most 100 actions, on 100 distinct items (one item twice is refused here, before
/// it is sent), 4 MB in all; each action costs twice a plain write's capacity, cancelled or not; and a GSI reflects
/// the items only after the commit, as for any write.</para>
///
/// <para><b>Retrying.</b> The transaction carries one client request token, made when it is created unless the caller
/// passes one. Committing it again after an Unavailable or Failed outcome resends the same items with the same token,
/// so DynamoDB applies them once, however many times they are sent within ten minutes.</para>
///
/// <para>Single use: once it commits, or is refused, it is spent.</para>
/// </summary>
public sealed class DYDBTransaction
{
    /// <summary>DynamoDB's limit on the actions of one TransactWriteItems.</summary>
    public const int MaxActions = 100;

    private readonly List<TransactWriteItem> items = new();
    private readonly List<TxAction> actions = new();
    private readonly List<Action> onCommit = new();
    private readonly List<Func<Task>> afterCommit = new();
    private readonly HashSet<string> keys = new(StringComparer.Ordinal);
    private readonly long now = DateTime.UtcNow.Ticks;
    private IAmazonDynamoDB? client;
    private bool spent;

    /// <param name="clientRequestToken">The idempotency token, at most 36 characters; one is made when omitted.
    /// Pass one only to make two separate transactions the same request.</param>
    public DYDBTransaction(string? clientRequestToken = null)
    {
        ClientRequestToken = clientRequestToken ?? Guid.NewGuid().ToString("N");
    }

    public string ClientRequestToken { get; }

    public int Count => items.Count;

    public IReadOnlyList<TxAction> Actions => actions;

    /// <summary>Create <paramref name="data"/>, refused if an item with its key exists.</summary>
    public TxItem<T> Create<T>(IDocumentRepo<T> repo, ICallerInfo callerInfo, T data, string? label = null)
        where T : class, IItem, new()
    {
        var store = Store(repo);
        callerInfo ??= new CallerInfo();
        var (action, written, record) = store.TransactCreate(callerInfo, data, now);
        var item = Add(store, action.Put.TableName, record, action, i => new TxItem<T>(i, label));
        onCommit.Add(() => item.Value = written());
        afterCommit.Add(() => store.AfterTransactionAsync(callerInfo, record, "Create"));
        return item;
    }

    /// <summary>Write <paramref name="data"/> over its item, refused if the item was written since
    /// <paramref name="data"/> was read (its UpdateUtcTick) - unless forced, as UpdateAsync's forceUpdate.</summary>
    public TxItem<T> Update<T>(IDocumentRepo<T> repo, ICallerInfo callerInfo, T data, bool forceUpdate = false,
        string? label = null)
        where T : class, IItem, new()
    {
        var store = Store(repo);
        callerInfo ??= new CallerInfo();
        var (action, written, record) = store.TransactUpdate(callerInfo, data, now, forceUpdate);
        var item = Add(store, action.Put.TableName, record, action, i => new TxItem<T>(i, label));
        onCommit.Add(() => item.Value = written());
        afterCommit.Add(() => store.AfterTransactionAsync(callerInfo, record, "Update"));
        return item;
    }

    /// <summary>Delete the item <paramref name="id"/>; deleting an absent item is no refusal, as with DeleteItem.
    /// A repo that soft-deletes cannot take part.</summary>
    public TxAction Delete<T>(IDocumentRepo<T> repo, ICallerInfo callerInfo, string id, string? label = null)
        where T : class, IItem, new()
    {
        var store = Store(repo);
        callerInfo ??= new CallerInfo();
        var action = store.TransactDelete(callerInfo, id);
        return Add(store, action.Delete.TableName, action.Delete.Key, action, i => new TxAction(i, label));
    }

    /// <summary>Refuse the whole transaction unless the item <paramref name="id"/> exists. Writes nothing.</summary>
    public TxAction RequireExists<T>(IDocumentRepo<T> repo, ICallerInfo callerInfo, string id, string? label = null)
        where T : class, IItem, new()
        => Check(repo, callerInfo, id, mustExist: true, label);

    /// <summary>Refuse the whole transaction if the item <paramref name="id"/> exists. Writes nothing.</summary>
    public TxAction RequireAbsent<T>(IDocumentRepo<T> repo, ICallerInfo callerInfo, string id, string? label = null)
        where T : class, IItem, new()
        => Check(repo, callerInfo, id, mustExist: false, label);

    private TxAction Check<T>(IDocumentRepo<T> repo, ICallerInfo callerInfo, string id, bool mustExist, string? label)
        where T : class, IItem, new()
    {
        var store = Store(repo);
        callerInfo ??= new CallerInfo();
        var action = store.TransactCheck(callerInfo, id, mustExist);
        return Add(store, action.ConditionCheck.TableName, action.ConditionCheck.Key, action, i => new TxAction(i, label));
    }

    /// <summary>
    /// Send it. Committed: every item is written, and each <see cref="TxItem{T}.Value"/> is set. Refused, Invalid:
    /// nothing was written, and the transaction is spent. Unavailable, Failed: commit it again to retry.
    /// </summary>
    public async Task<TransactionResult> CommitAsync(CancellationToken cancellationToken = default)
    {
        if (spent) throw new InvalidOperationException("This transaction has already committed or been refused.");
        if (items.Count == 0) throw new InvalidOperationException("A transaction needs at least one action.");

        var request = new TransactWriteItemsRequest
        {
            TransactItems = items,
            ClientRequestToken = ClientRequestToken
        };
        TransactionResult result;
        try
        {
            await client!.TransactWriteItemsAsync(request, cancellationToken);
            result = new TransactionResult(TransactionOutcome.Committed);
        }
        catch (TransactionCanceledException ex)
        {
            result = Cancelled(ex);
        }
        catch (Exception ex) when (ex is TransactionInProgressException or TransactionConflictException
                                       or ProvisionedThroughputExceededException or RequestLimitExceededException
                                       or InternalServerErrorException)
        {
            result = new TransactionResult(TransactionOutcome.Unavailable, detail: $"{ex.GetType().Name}: {ex.Message}");
        }
        catch (IdempotentParameterMismatchException ex)
        {
            result = new TransactionResult(TransactionOutcome.Invalid, detail: $"{ex.GetType().Name}: {ex.Message}");
        }
        catch (AmazonDynamoDBException ex) when (ex.ErrorCode is "ValidationException")
        {
            result = new TransactionResult(TransactionOutcome.Invalid, detail: $"{ex.GetType().Name}: {ex.Message}");
        }
        catch (AmazonServiceException ex) when (ex.ErrorCode is "ThrottlingException")
        {
            result = new TransactionResult(TransactionOutcome.Unavailable, detail: $"{ex.GetType().Name}: {ex.Message}");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Unknown: it may have committed. Always logged, as ReadAsync's catch-all is: its type identifies it.
            Console.WriteLine($"DYDBTransaction.CommitAsync() catch all. {ex.GetType().Name}: {ex.Message}");
            result = new TransactionResult(TransactionOutcome.Failed, detail: $"{ex.GetType().Name}: {ex.Message}");
        }

        if (result.Outcome is TransactionOutcome.Committed or TransactionOutcome.Refused or TransactionOutcome.Invalid)
            spent = true;
        if (result.Committed)
        {
            foreach (var set in onCommit) set();
            foreach (var notify in afterCommit) await notify();
        }
        return result;
    }

    /// <summary>
    /// A cancellation's reasons, one per action in order. A failed condition anywhere is the answer - the caller's
    /// conflict, which retrying does not change; otherwise a conflict or throttle passes, and anything else is the
    /// request's own fault.
    /// </summary>
    private TransactionResult Cancelled(TransactionCanceledException ex)
    {
        var reasons = ex.CancellationReasons ?? new List<CancellationReason>();
        var detail = $"TransactionCanceledException: [{string.Join(", ", reasons.Select(r => r?.Code ?? "?"))}]";
        for (var i = 0; i < reasons.Count && i < actions.Count; i++)
            if (reasons[i]?.Code == "ConditionalCheckFailed")
                return new TransactionResult(TransactionOutcome.Refused, actions[i], detail);
        if (reasons.Any(r => r?.Code is "TransactionConflict" or "ThrottlingError" or "ProvisionedThroughputExceeded"))
            return new TransactionResult(TransactionOutcome.Unavailable, detail: detail);
        if (reasons.Any(r => r?.Code is "ValidationError" or "ItemCollectionSizeLimitExceeded"))
            return new TransactionResult(TransactionOutcome.Invalid, detail: detail);
        Console.WriteLine($"DYDBTransaction.CommitAsync() cancelled for no reason it knows. {detail}");
        return new TransactionResult(TransactionOutcome.Failed, detail: detail);
    }

    private DYDBRepository<T> Store<T>(IDocumentRepo<T> repo) where T : class, IItem, new()
    {
        if (spent) throw new InvalidOperationException("This transaction has already committed or been refused.");
        if (repo is not DYDBRepository<T> store)
            throw new ArgumentException($"{repo?.GetType().Name ?? "null"} is not a DynamoDB repository; it cannot take part in a transaction.", nameof(repo));
        if (items.Count == MaxActions)
            throw new InvalidOperationException($"A DynamoDB transaction takes at most {MaxActions} actions.");
        var theirs = store.TransactionClient;
        if (client == null) client = theirs;
        else if (!ReferenceEquals(client, theirs))
            throw new InvalidOperationException(
                $"{store.GetType().Name} writes through another DynamoDB client: one transaction is one request, through one client.");
        return store;
    }

    private TAction Add<TAction>(object store, string table, Dictionary<string, AttributeValue> keyOrRecord,
        TransactWriteItem action, Func<int, TAction> make) where TAction : TxAction
    {
        // DynamoDB refuses a transaction that touches one item twice, with a ValidationException that does not say
        // which. Refuse it here, naming the item: it is the caller's mistake, never a race.
        var key = $"{table}\u0000{keyOrRecord["PK"].S}\u0000{keyOrRecord["SK"].S}";
        if (!keys.Add(key))
            throw new InvalidOperationException(
                $"{store.GetType().Name} {keyOrRecord["SK"].S} is already in this transaction: DynamoDB takes each item once.");
        var made = make(items.Count);
        items.Add(action);
        actions.Add(made);
        return made;
    }
}
