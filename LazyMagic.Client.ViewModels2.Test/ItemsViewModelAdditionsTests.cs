using System.Collections.Specialized;
using LazyMagic.Client.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LazyMagic.Client.ViewModels.Test;

/// <summary>A collection read by its parent's id, as a child collection is.</summary>
public sealed class ThingsOfViewModel : LzItemsViewModel<string, ThingViewModel, Thing, ThingModel>
{
    private readonly ThingStore _store;

    public ThingsOfViewModel(ThingStore store) : base(NullLoggerFactory.Instance)
    {
        _store = store;
        _DTOReadListIdAsync = store.ListOfAsync;
    }

    public override (ThingViewModel viewmodel, string id) NewViewModel(Thing dto)
        => (new ThingViewModel(NullLoggerFactory.Instance, _store, this, dto), string.Empty);
}

/// <summary>
/// FOUR ADDITIONS TO LzItemsViewModel, in ViewModels2 only (2026-10-13): reading again after a change, starting a
/// new item, keeping the order a read returned, and saying when the collection changed. Each is off, or changes
/// nothing, unless asked for - except the notifications, which a collection never gave before.
/// </summary>
public class ItemsViewModelAdditionsTests : IClassFixture<ReactiveUi>
{
    private static (ThingStore Store, ThingsViewModel Things) Three()
    {
        var store = new ThingStore();
        foreach (var id in new[] { "a", "b", "c" })
            store.Things[id] = new Thing { Id = id, Name = id.ToUpperInvariant() };
        return (store, new ThingsViewModel(store));
    }

    private static int Lists(ThingStore store) => store.Calls.Count(c => c == "list");

    // ---- all of it is on the interfaces ----------------------------------------------------------------------------

    [Fact]
    public async Task WhatWasAdded_CanBeUsedThroughTheInterfaces()
    {
        var (store, concrete) = Three();
        ILzItemsViewModel<string, ThingViewModel, Thing, ThingModel> things = concrete;
        things.ReplaceOnRead = true;
        things.ReadAfterChange = true;
        Assert.True((await things.ReadAsync()).Item1);
        Assert.Null(things.LastException);

        ILzItemViewModel<string, ThingModel> made = things.NewItem(new Thing { Id = "d", Name = "D" });
        Assert.Same(made, things.EditViewModel);
        var lists = Lists(store);
        Assert.True((await made.CreateAsync()).Item1);       // and the collection read again after it
        Assert.Equal(lists + 1, Lists(store));
        Assert.Null(made.LastException);

        store.FailOn = _ => new Refused(503, "throttled");
        Assert.False((await things.ReadAgainAsync()).Item1);
        Assert.IsType<Refused>(things.LastException);
        Assert.False((await made.UpdateAsync()).Item1);
        Assert.IsType<Refused>(made.LastException);
    }

    // ---- 1. read after change --------------------------------------------------------------------------------------

    [Fact]
    public async Task ByDefault_AnItemsChange_ReadsNothing()
    {
        var (store, things) = Three();
        await things.ReadAsync();
        var lists = Lists(store);

        await things.ViewModels["a"].UpdateAsync();
        await things.ViewModels["b"].DeleteAsync();
        var made = things.NewItem(new Thing { Id = "d", Name = "D" });
        await made.CreateAsync();

        Assert.Equal(lists, Lists(store));
        Assert.Equal(new[] { "a", "c", "d" }, things.ViewModels.Keys.OrderBy(k => k));
    }

    [Fact]
    public async Task WithReadAfterChange_EachOfTheThreeIsFollowedByARead_AndTheOthersAreAsTheServiceHasThem()
    {
        var (store, things) = Three();
        things.ReadAfterChange = true;
        things.ReplaceOnRead = true;
        await things.ReadAsync();

        // A write to one changes another on the service - the flag only one may carry, say.
        var lists = Lists(store);
        things.ViewModels["a"].Data!.Name = "Alpha";
        store.FailOn = call => { if (call == "update") store.Things["b"].Name = "changed by a's update"; return null; };
        Assert.True((await things.ViewModels["a"].UpdateAsync()).Item1);
        Assert.Equal(lists + 1, Lists(store));
        Assert.Equal("changed by a's update", things.ViewModels["b"].Data!.Name);
        store.FailOn = _ => null;

        Assert.True((await things.ViewModels["c"].DeleteAsync()).Item1);
        Assert.Equal(lists + 2, Lists(store));
        Assert.Equal(new[] { "a", "b" }, things.ViewModels.Keys.OrderBy(k => k));

        var made = things.NewItem(new Thing { Id = "d", Name = "D" });
        Assert.True((await made.CreateAsync()).Item1);
        Assert.Equal(lists + 3, Lists(store));
        Assert.Equal(new[] { "a", "b", "d" }, things.ViewModels.Keys.OrderBy(k => k));
        Assert.Null(things.EditViewModel);
    }

    [Fact]
    public async Task AReadAfterAChangeThatFails_DoesNotUndoTheChange()
    {
        var (store, things) = Three();
        things.ReadAfterChange = true;
        await things.ReadAsync();
        store.FailOn = call => call == "list" ? new Refused(503, "throttled") : null;

        var (updated, _) = await things.ViewModels["a"].UpdateAsync();

        Assert.True(updated);                                // the item was saved; that the list could not be read is the list's
        Assert.Null(things.ViewModels["a"].LastException);
        Assert.IsType<Refused>(things.LastException);
        Assert.Equal(3, things.ViewModels.Count);
    }

    [Fact]
    public async Task ReadingAgain_IsTheReadItWasLastGiven_AndBeforeAnyRead_ByItsOwnId()
    {
        var store = new ThingStore();
        store.Things["a"] = new Thing { Id = "a", Name = "A" };
        var things = new ThingsOfViewModel(store) { ReadAfterChange = true, Id = "parent-own" };

        await things.ReadAgainAsync();                       // never read: by the collection's own id
        Assert.Equal(new[] { "parent-own" }, store.Parents);

        await things.ReadAsync("parent-7");
        await things.ViewModels["a"].UpdateAsync();          // read again after the change: the same parent
        Assert.Equal(new[] { "parent-own", "parent-7", "parent-7" }, store.Parents);

        // A collection read with no id is read again with none.
        var (plainStore, plain) = Three();
        plain.ReadAfterChange = true;
        plain.Id = "ignored-once-read";
        await plain.ReadAsync();
        var lists = Lists(plainStore);
        Assert.True((await plain.ReadAgainAsync()).Item1);
        Assert.Equal(lists + 1, Lists(plainStore));
    }

    // ---- 2. a new item ---------------------------------------------------------------------------------------------

    [Fact]
    public async Task ANewItem_IsNew_NotLoaded_TheOneBeingEdited_AndNotInTheCollectionUntilItIsCreated()
    {
        var (_, things) = Three();
        await things.ReadAsync();

        var made = things.NewItem(new Thing { Id = "d", Name = "" });

        Assert.Equal(LzItemViewModelState.New, made.State);
        Assert.True(made.IsNew);
        Assert.False(made.IsLoaded);
        Assert.Same(made, things.EditViewModel);
        Assert.Same(things, made.ParentViewModel);           // made as every other is, by NewViewModel
        Assert.False(things.ViewModels.ContainsKey("d"));

        // Cancelled, it is still new - not an item that exists - and it is nobody's edit.
        await made.OpenEditAsync();
        made.Data!.Name = "typed";
        Assert.True((await made.CancelEditAsync()).Item1);
        Assert.Equal(LzItemViewModelState.New, made.State);
        Assert.Null(things.EditViewModel);
        Assert.False(things.ViewModels.ContainsKey("d"));

        // Created, it is in the collection and no longer being edited.
        var again = things.NewItem(new Thing { Id = "d", Name = "D" });
        Assert.True((await again.CreateAsync()).Item1);
        Assert.Same(again, things.ViewModels["d"]);
        Assert.Equal(LzItemViewModelState.Current, again.State);
        Assert.Null(things.EditViewModel);
    }

    // ---- 3. the order a read returned ------------------------------------------------------------------------------

    [Fact]
    public async Task WithReplaceOnRead_TheCollectionIsInTheOrderTheReadReturned()
    {
        var (store, things) = Three();
        things.ReplaceOnRead = true;
        await things.ReadAsync();
        Assert.Equal(new[] { "a", "b", "c" }, things.ViewModels.Keys);
        Assert.Equal(new[] { "A", "B", "C" }, things.ViewModels.Values.Select(v => v.Data!.Name));

        // The service now answers in another order - one became the main one, say - with one gone and one new.
        store.Things.Remove("b");
        store.Things["d"] = new Thing { Id = "d", Name = "D" };
        store.Arrange = all => all.OrderByDescending(t => t.Id);
        await things.ReadAsync();

        Assert.Equal(new[] { "d", "c", "a" }, things.ViewModels.Keys);
        Assert.Equal(new[] { "D", "C", "A" }, things.ViewModels.Values.Select(v => v.Data!.Name));

        // An id returned twice is one item, where it first came.
        store.Arrange = all => all.OrderBy(t => t.Id).Concat(all.Where(t => t.Id == "a"));
        Assert.True((await things.ReadAsync()).Item1);       // and the read does not fail on it
        Assert.Equal(new[] { "a", "c", "d" }, things.ViewModels.Keys);
    }

    [Fact]
    public async Task WithoutReplaceOnRead_NothingIsMoved()
    {
        var (store, things) = Three();
        await things.ReadAsync();

        store.Arrange = all => all.OrderByDescending(t => t.Id);
        await things.ReadAsync();

        Assert.Equal(new[] { "a", "b", "c" }, things.ViewModels.Keys);   // as it always was: added and replaced in place
    }

    // ---- 4. saying that it changed ---------------------------------------------------------------------------------

    [Fact]
    public async Task TheCollectionSaysWhenItChanged_AfterARead_ACreate_ADelete_AndAClear()
    {
        var (_, things) = Three();
        var events = new List<NotifyCollectionChangedAction>();
        var changes = 0;
        things.CollectionChanged += (sender, e) => { Assert.Same(things, sender); events.Add(e.Action); };
        things.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(things.IsChanged)) changes++; };

        void Said(string after)
        {
            Assert.True(events.Count == 1 && changes == 1, $"{after}: {events.Count} event(s), {changes} property change(s)");
            Assert.Equal(NotifyCollectionChangedAction.Reset, events[0]);
            Assert.True(things.IsChanged);
            events.Clear(); changes = 0;
        }

        await things.ReadAsync();
        Said("a read");

        var made = things.NewItem(new Thing { Id = "d", Name = "D" });
        Assert.True(events.Count == 0 && changes == 0);      // a new item is not in the collection yet
        await made.CreateAsync();
        Said("a create");

        await things.ViewModels["a"].DeleteAsync();
        Said("a delete");

        await things.ViewModels["b"].UpdateAsync();
        Assert.True(events.Count == 0 && changes == 0);      // an update changes an item, not the collection

        things.Clear();
        Said("a clear");
    }

    [Fact]
    public async Task WheneverTheCollectionAnnouncesAnything_EveryItemInItHasItsData()
    {
        // A component draws each item's Data when the collection says something changed. A deleted item has none,
        // so it must be out of the collection before anything is announced - whichever item is deleted, with or
        // without the read after it.
        foreach (var readAfterChange in new[] { false, true })
            foreach (var deleted in new[] { "a", "b", "c" })
            {
                var (_, things) = Three();
                things.ReadAfterChange = things.ReplaceOnRead = readAfterChange;
                await things.ReadAsync();
                things.CurrentViewModel = things.ViewModels[deleted];
                var announcements = 0;
                things.PropertyChanged += (_, e) =>
                {
                    announcements++;
                    Assert.All(things.ViewModels.Values, item => Assert.True(item.Data != null, $"{e.PropertyName} was announced with a deleted item still in the collection"));
                };

                Assert.True((await things.ViewModels[deleted].DeleteAsync()).Item1);

                Assert.True(announcements > 0);
                Assert.Equal(2, things.ViewModels.Count);
                Assert.False(things.ViewModels.ContainsKey(deleted));
                Assert.NotNull(things.CurrentViewModel);           // a neighbour is the current one now
                Assert.NotNull(things.CurrentViewModel!.Data);
            }
    }

    [Fact]
    public async Task TheOneBeingEdited_IsAnnouncedWhenItIsSetAndWhenItIsCleared()
    {
        var (_, things) = Three();
        await things.ReadAsync();
        var announced = 0;
        things.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(things.EditViewModel)) announced++; };

        things.EditViewModel = things.ViewModels["a"];
        Assert.Equal(1, announced);
        things.EditViewModel = things.ViewModels["a"];       // the same one again is no change
        Assert.Equal(1, announced);

        await things.ViewModels["a"].OpenEditAsync();
        await things.ViewModels["a"].CancelEditAsync();      // the collection is told, and clears it
        Assert.Null(things.EditViewModel);
        Assert.Equal(2, announced);

        things.NewItem(new Thing { Id = "d" });
        Assert.Equal(3, announced);
    }
}
