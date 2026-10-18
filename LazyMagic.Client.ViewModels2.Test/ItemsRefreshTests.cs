using System.Collections.Specialized;
using System.ComponentModel;
using LazyMagic.Client.ViewModels;
using Xunit;

namespace LazyMagic.Client.ViewModels.Test;

/// <summary>
/// A QUIET READ OF A COLLECTION, FOR A POLL (2026-10-17): RefreshAsync reads the list again as it was last read, never
/// sets IsLoading, and replaces nothing - no view model, no notification - when the list that came back is, field for
/// field, the one last applied. ViewModels2 only.
/// </summary>
public class ItemsRefreshTests : IClassFixture<ReactiveUi>
{
    private static async Task<(ThingStore Store, ThingsViewModel Things, List<string> Changed, List<NotifyCollectionChangedAction> Collection)> ReadThreeAsync()
    {
        var store = new ThingStore();
        foreach (var id in new[] { "a", "b", "c" })
            store.Things[id] = new Thing { Id = id, Name = id.ToUpperInvariant() };
        var things = new ThingsViewModel(store) { ReplaceOnRead = true };
        Assert.True((await things.ReadAsync()).Item1);

        var changed = new List<string>();
        ((INotifyPropertyChanged)things).PropertyChanged += (_, e) => changed.Add(e.PropertyName ?? "");
        var collection = new List<NotifyCollectionChangedAction>();
        ((INotifyCollectionChanged)things).CollectionChanged += (_, e) => collection.Add(e.Action);
        return (store, things, changed, collection);
    }

    [Fact]
    public async Task ARefreshOfTheSameList_ReplacesNothing_AndAnnouncesNothing()
    {
        var (_, things, changed, collection) = await ReadThreeAsync();
        var before = things.ViewModels["b"];
        var checkedBefore = things.LastCheckedTick;

        Assert.True((await things.RefreshAsync()).Item1);

        Assert.Empty(changed);                               // not IsLoading, not IsLoaded, not LastLoadTick
        Assert.Empty(collection);
        Assert.Same(before, things.ViewModels["b"]);         // the row's view model is the one it was
        Assert.True(things.LastCheckedTick >= checkedBefore);
    }

    [Fact]
    public async Task ARefreshOfAChangedList_IsAppliedAsAReadIs_WithoutLoading()
    {
        var (store, things, changed, collection) = await ReadThreeAsync();
        store.Things["b"] = new Thing { Id = "b", Name = "Bee" };
        store.Things.Remove("c");

        Assert.True((await things.RefreshAsync()).Item1);

        Assert.Equal(new[] { "a", "b" }, things.ViewModels.Keys.ToArray());
        Assert.Equal("Bee", things.ViewModels["b"].Data!.Name);
        Assert.NotEmpty(collection);
        Assert.DoesNotContain(nameof(things.IsLoading), changed);
    }

    /// <summary>A refresh that started before a read, and lands after it, carries the older list: it is dropped.</summary>
    [Fact]
    public async Task ARefreshOvertakenByARead_IsDropped_AndTheReadsListStands()
    {
        var (store, things, _, _) = await ReadThreeAsync();
        var hold = new TaskCompletionSource();
        store.HoldNext = hold;
        var refresh = things.RefreshAsync();                 // asks now: a, b, c - and is held

        store.Things.Clear();                                // everything is cleared
        Assert.True((await things.ReadAsync(forceload: true)).Item1);
        Assert.Empty(things.ViewModels);

        hold.SetResult();
        Assert.True((await refresh).Item1);
        Assert.Empty(things.ViewModels);                     // the cleared rows do not come back
    }

    /// <summary>One that nothing overtook is applied as before.</summary>
    [Fact]
    public async Task ARefreshNothingOvertook_IsApplied()
    {
        var (store, things, _, _) = await ReadThreeAsync();
        store.Things["b"] = new Thing { Id = "b", Name = "Bee" };
        var hold = new TaskCompletionSource();
        store.HoldNext = hold;
        var refresh = things.RefreshAsync();                 // asks now, and carries the change - held a while

        hold.SetResult();
        Assert.True((await refresh).Item1);
        Assert.Equal("Bee", things.ViewModels["b"].Data!.Name);
    }

    [Fact]
    public async Task ARefreshThatFails_KeepsTheList_AndSaysWhy()
    {
        var (store, things, changed, collection) = await ReadThreeAsync();
        store.FailOn = _ => new Refused(503, "throttled");

        Assert.False((await things.RefreshAsync()).Item1);

        Assert.IsType<Refused>(things.LastException);
        Assert.Equal(3, things.ViewModels.Count);
        Assert.Empty(collection);
        Assert.Equal(new[] { nameof(things.LastException) }, changed);   // a failure is news, and the only news
    }

    /// <summary>A list whose signature cannot be written - as a generated DTO with a DisallowNull member left null cannot.</summary>
    private sealed class UnwritableThingsViewModel : LzItemsViewModel<string, ThingViewModel, Thing, ThingModel>
    {
        private readonly ThingStore _store;
        public UnwritableThingsViewModel(ThingStore store) : base(Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance)
        {
            _store = store;
            AutoReadChildren = false;
            _DTOReadListAsync = store.ListAsync;
        }
        public override (ThingViewModel viewmodel, string id) NewViewModel(Thing dto)
            => (new ThingViewModel(Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance, _store, this, dto), string.Empty);
        protected override string ReadSignature(ICollection<Thing> list)
            => throw new Newtonsoft.Json.JsonSerializationException("Cannot write a null value for property id.");
    }

    [Fact]
    public async Task AListWhoseSignatureCannotBeWritten_IsStillRead_AndARefreshAppliesIt()
    {
        var store = new ThingStore();
        store.Things["a"] = new Thing { Id = "a", Name = "A" };
        var things = new UnwritableThingsViewModel(store) { ReplaceOnRead = true };

        Assert.True((await things.ReadAsync()).Item1);          // a read never fails for want of a comparison
        Assert.Single(things.ViewModels);

        store.Things["b"] = new Thing { Id = "b", Name = "B" };
        Assert.True((await things.RefreshAsync()).Item1);       // no signature: taken to differ, and applied
        Assert.Equal(2, things.ViewModels.Count);
    }

    [Fact]
    public async Task RefreshAsync_IsOnTheInterface()
    {
        var (store, concrete, _, _) = await ReadThreeAsync();
        ILzItemsViewModel<string, ThingViewModel, Thing, ThingModel> things = concrete;
        store.Things["d"] = new Thing { Id = "d", Name = "D" };

        Assert.True((await things.RefreshAsync()).Item1);
        Assert.Equal(4, concrete.ViewModels.Count);
        Assert.True(things.LastCheckedTick > 0);
    }
}
