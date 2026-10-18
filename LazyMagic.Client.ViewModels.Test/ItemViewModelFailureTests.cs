using LazyMagic.Client.ViewModels;
using LazyMagic.Shared;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ReactiveUI.Builder;
using Xunit;

namespace LazyMagic.Client.ViewModels.Test;

// THIS FILE IS COMPILED TWICE: by LazyMagic.Client.ViewModels.Test against LazyMagic.Client.ViewModels, and by
// LazyMagic.Client.ViewModels2.Test, which links it and defines VIEWMODELS2, against LazyMagic.Client.ViewModels2 -
// the same classes with the id's type as a parameter. The two libraries declare the same names in the same
// namespace, so one test project cannot reference both. Every test below runs against each.

// ---- a store and the two view models over it -------------------------------------------------------------------------

public class Thing
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public long UpdateUtcTick { get; set; }
}

public class ThingModel : Thing, IRegisterObservables
{
}

/// <summary>What a service is to these tests: things by id, and a failure to give for any call.</summary>
public sealed class ThingStore
{
    public Dictionary<string, Thing> Things { get; } = new();
    public Func<string, Exception?> FailOn { get; set; } = _ => null;
    public List<string> Calls { get; } = new();

    private void Called(string call)
    {
        Calls.Add(call);
        if (FailOn(call) is { } failure) throw failure;
    }

    public Task<Thing> CreateAsync(Thing thing) { Called("create"); Things[thing.Id] = thing; return Task.FromResult(thing); }
    /// <summary>
    /// When set, the NEXT read or list answers with what is stored at the moment it is asked, and only once this is
    /// released - a slow request overtaken by another. Taken by that read; the ones after it answer at once.
    /// </summary>
    public TaskCompletionSource? HoldNext { get; set; }

    private async Task<T> AnsweredAsync<T>(T answer)
    {
        if (HoldNext is not { } hold) return answer;
        HoldNext = null;
        await hold.Task;
        return answer;
    }

    public Task<Thing> ReadAsync(string id)
    {
        Called("read");
        var stored = Things[id];
        return AnsweredAsync(new Thing { Id = stored.Id, Name = stored.Name, UpdateUtcTick = stored.UpdateUtcTick });
    }
    public Task<Thing> UpdateAsync(Thing thing) { Called("update"); Things[thing.Id] = thing; return Task.FromResult(thing); }
    public Task DeleteAsync(string id) { Called("delete"); Things.Remove(id); return Task.CompletedTask; }
    /// <summary>The order a list is answered in; as stored, unless a test says otherwise.</summary>
    public Func<IEnumerable<Thing>, IEnumerable<Thing>> Arrange { get; set; } = things => things;
    /// <summary>The parent ids the by-parent list was asked for.</summary>
    public List<string> Parents { get; } = new();

    public Task<ICollection<Thing>> ListAsync() { Called("list"); return AnsweredAsync<ICollection<Thing>>(Arrange(Things.Values).Select(t => new Thing { Id = t.Id, Name = t.Name }).ToList()); }
    public Task<ICollection<Thing>> ListOfAsync(string parent) { Parents.Add(parent); return ListAsync(); }
}

#if VIEWMODELS2
public sealed class ThingViewModel : LzItemViewModel<string, Thing, ThingModel>
{
    public ThingViewModel(ILoggerFactory loggerFactory, ThingStore store, ILzParentViewModel<string>? parent, Thing? thing)
#else
public sealed class ThingViewModel : LzItemViewModel<Thing, ThingModel>
{
    public ThingViewModel(ILoggerFactory loggerFactory, ThingStore store, ILzParentViewModel? parent, Thing? thing)
#endif
        : base(loggerFactory, thing)
    {
        ParentViewModel = parent;
        _DTOCreateAsync = store.CreateAsync;
        _DTOReadAsync = store.ReadAsync;
        _DTOUpdateAsync = store.UpdateAsync;
        _DTODeleteAsync = store.DeleteAsync;
    }

    public override string? Id => Data?.Id;
    public override long UpdatedAt => Data?.UpdateUtcTick ?? 0;

    public string? Invalid { get; set; }
    public override Task<(bool, string)> ValidateAsync() => Task.FromResult(Invalid == null ? (true, "") : (false, Invalid));
}

#if VIEWMODELS2
public sealed class ThingsViewModel : LzItemsViewModel<string, ThingViewModel, Thing, ThingModel>
#else
public sealed class ThingsViewModel : LzItemsViewModel<ThingViewModel, Thing, ThingModel>
#endif
{
    private readonly ThingStore _store;

    public ThingsViewModel(ThingStore store) : base(NullLoggerFactory.Instance)
    {
        _store = store;
        AutoReadChildren = false;
        _DTOReadListAsync = store.ListAsync;
    }

    public override (ThingViewModel viewmodel, string id) NewViewModel(Thing dto)
        => (new ThingViewModel(NullLoggerFactory.Instance, _store, this, dto), string.Empty);
}

/// <summary>A service's refusal: an exception that carries more than a message.</summary>
public sealed class Refused(int status, string body) : Exception("refused")
{
    public int Status { get; } = status;
    public string Body { get; } = body;
}

/// <summary>ReactiveUI is initialised once: the item view model's constructor calls WhenAnyValue.</summary>
public sealed class ReactiveUi
{
    private static readonly object Gate = new();
    private static bool _done;

    public ReactiveUi()
    {
        lock (Gate)
        {
            if (_done) return;
            RxAppBuilder.CreateReactiveUIBuilder().WithCoreServices().BuildApp();
            _done = true;
        }
    }
}

/// <summary>
/// THE FAILURE IS KEPT. Create, update, delete and read return a message built for a log; LastException is the
/// exception itself, so an application can tell a refusal from an outage and show what the service said.
/// </summary>
public class ItemViewModelFailureTests : IClassFixture<ReactiveUi>
{
    private static (ThingStore Store, ThingViewModel Item) AStoredThing()
    {
        var store = new ThingStore();
        store.Things["a"] = new Thing { Id = "a", Name = "Alpha" };
        return (store, new ThingViewModel(NullLoggerFactory.Instance, store, null, store.Things["a"]));
    }

    [Theory]
    [InlineData("create")]
    [InlineData("update")]
    [InlineData("delete")]
    [InlineData("read")]
    public async Task TheExceptionOfAFailedOperation_IsKept_AsItWasThrown(string operation)
    {
        var (store, item) = AStoredThing();
        if (operation == "create")
            item = new ThingViewModel(NullLoggerFactory.Instance, store, null, null) { State = LzItemViewModelState.New };
        var refusal = new Refused(409, "you already have one called Alpha");
        store.FailOn = call => call == operation ? refusal : null;

        var (succeeded, message) = operation switch
        {
            "create" => await item.CreateAsync(),
            "update" => await item.UpdateAsync(),
            "delete" => await item.DeleteAsync(),
            _ => await item.ReadAsync(),
        };

        Assert.False(succeeded);
        Assert.Same(refusal, item.LastException);                 // its type, its status and its body, not a message
        Assert.Equal(409, Assert.IsType<Refused>(item.LastException).Status);
        Assert.DoesNotContain("you already have one", message.Replace("refused", ""));   // the returned message never carried the body
        Assert.False(item.IsBusy);
    }

    [Fact]
    public async Task ItIsClearedByTheNextOperation_WhetherThatSucceedsOrFailsWithoutAnException()
    {
        var (store, item) = AStoredThing();
        store.FailOn = _ => new Refused(500, "down");
        Assert.False((await item.UpdateAsync()).Item1);
        Assert.NotNull(item.LastException);

        // The next one succeeds.
        store.FailOn = _ => null;
        Assert.True((await item.UpdateAsync()).Item1);
        Assert.Null(item.LastException);

        // One that fails on validation fails with no exception: the message returned is the reason.
        store.FailOn = _ => new Refused(500, "down");
        Assert.False((await item.UpdateAsync()).Item1);
        item.Invalid = "the name is missing";
        var (succeeded, message) = await item.UpdateAsync();
        Assert.False(succeeded);
        Assert.Equal("the name is missing", message);
        Assert.Null(item.LastException);

        // And each of the four clears it.
        item.Invalid = null;
        foreach (var operation in new Func<Task<(bool, string)>>[] { () => item.ReadAsync(), () => item.UpdateAsync(), () => item.DeleteAsync() })
        {
            store.FailOn = _ => new Refused(500, "down");
            await item.UpdateAsync();
            Assert.NotNull(item.LastException);
            store.FailOn = _ => null;
            Assert.True((await operation()).Item1);
            Assert.Null(item.LastException);
        }
        var created = new ThingViewModel(NullLoggerFactory.Instance, store, null, null) { State = LzItemViewModelState.New };
        store.FailOn = _ => new Refused(500, "down");
        await created.CreateAsync();
        Assert.NotNull(created.LastException);
        store.FailOn = _ => null;
        created.Data!.Id = "b";
        Assert.True((await created.CreateAsync()).Item1);
        Assert.Null(created.LastException);
    }

    [Fact]
    public async Task AListThatCouldNotBeRead_KeepsItsException_AndTheNextReadClearsIt()
    {
        var store = new ThingStore();
        store.Things["a"] = new Thing { Id = "a", Name = "Alpha" };
        var things = new ThingsViewModel(store);
        var outage = new Refused(503, "throttled");
        store.FailOn = _ => outage;

        var (read, _) = await things.ReadAsync();

        Assert.False(read);
        Assert.Same(outage, things.LastException);
        Assert.False(things.IsLoaded);                            // a list that could not be read is not an empty one
        Assert.False(things.IsLoading);

        store.FailOn = _ => null;
        Assert.True((await things.ReadAsync()).Item1);
        Assert.Null(things.LastException);
        Assert.True(things.IsLoaded);
        Assert.Single(things.ViewModels);
    }
}

/// <summary>
/// REPLACE ON READ. By default a read adds and replaces and never removes - as it always did. With ReplaceOnRead, the
/// collection is what was read.
/// </summary>
public class ItemsViewModelReplaceOnReadTests : IClassFixture<ReactiveUi>
{
    private static (ThingStore Store, ThingsViewModel Things) Three(bool replaceOnRead)
    {
        var store = new ThingStore();
        foreach (var id in new[] { "a", "b", "c" })
            store.Things[id] = new Thing { Id = id, Name = id.ToUpperInvariant() };
        return (store, new ThingsViewModel(store) { ReplaceOnRead = replaceOnRead });
    }

    [Fact]
    public async Task ByDefault_AReadAddsAndReplaces_AndNeverRemoves()
    {
        var (store, things) = Three(replaceOnRead: false);
        await things.ReadAsync();

        store.Things.Remove("b");
        store.Things["d"] = new Thing { Id = "d", Name = "D" };
        store.Things["a"].Name = "Alpha";
        await things.ReadAsync();

        Assert.Equal(new[] { "a", "b", "c", "d" }, things.ViewModels.Keys.OrderBy(k => k));   // b is still there
        Assert.Equal("Alpha", things.ViewModels["a"].Data!.Name);
    }

    [Fact]
    public async Task WithReplaceOnRead_TheCollectionIsWhatWasRead()
    {
        var (store, things) = Three(replaceOnRead: true);
        await things.ReadAsync();
        things.CurrentViewModel = things.ViewModels["b"];
        things.EditViewModel = things.ViewModels["b"];
        Assert.Same(things.ViewModels["b"], things.LastViewModel);

        store.Things.Remove("b");
        store.Things["d"] = new Thing { Id = "d", Name = "D" };
        store.Things["a"].Name = "Alpha";
        await things.ReadAsync();

        Assert.Equal(new[] { "a", "c", "d" }, things.ViewModels.Keys.OrderBy(k => k));
        Assert.Equal("Alpha", things.ViewModels["a"].Data!.Name);
        // The one that went is not the current one, the last one or the one being edited any longer.
        Assert.Null(things.CurrentViewModel);
        Assert.Null(things.LastViewModel);
        Assert.Null(things.EditViewModel);

        // A read that returns nothing empties it, and it is a list that was read.
        store.Things.Clear();
        Assert.True((await things.ReadAsync()).Item1);
        Assert.Empty(things.ViewModels);
        Assert.True(things.IsLoaded);
    }

    [Fact]
    public async Task AReadThatFails_RemovesNothing_AndWhatIsCurrentStaysCurrent()
    {
        var (store, things) = Three(replaceOnRead: true);
        await things.ReadAsync();
        things.CurrentViewModel = things.ViewModels["c"];
        store.Things.Clear();
        store.FailOn = _ => new Refused(503, "throttled");

        Assert.False((await things.ReadAsync()).Item1);

        Assert.Equal(3, things.ViewModels.Count);
        Assert.Same(things.ViewModels["c"], things.CurrentViewModel);
    }

    [Fact]
    public async Task OneThatIsKept_StaysTheCurrentOne_OnlyIfItIsTheSameViewModel()
    {
        var (_, things) = Three(replaceOnRead: true);
        await things.ReadAsync();
        var current = things.CurrentViewModel = things.ViewModels["a"];

        await things.ReadAsync();

        // A read makes a new view model for every item, as it always did; what was current is not dropped for that.
        Assert.Same(current, things.CurrentViewModel);
        Assert.Equal(3, things.ViewModels.Count);
    }
}
