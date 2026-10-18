using System.ComponentModel;
using LazyMagic.Client.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LazyMagic.Client.ViewModels.Test;

/// <summary>
/// A QUIET READ, FOR A POLL (2026-10-17): RefreshAsync reads the item as ReadAsync does, but never sets IsBusy and
/// announces nothing when what came back is, field for field, what it already holds - so a page polling it redraws only
/// when something changed. ViewModels2 only.
/// </summary>
public class ItemRefreshTests : IClassFixture<ReactiveUi>
{
    private static async Task<(ThingStore Store, ThingViewModel Item, List<string> Changed)> ReadOneAsync()
    {
        var store = new ThingStore();
        store.Things["a"] = new Thing { Id = "a", Name = "Alpha", UpdateUtcTick = 1 };
        var item = new ThingViewModel(NullLoggerFactory.Instance, store, null, new Thing { Id = "a" }) { IsLoaded = false };
        Assert.True((await item.ReadAsync()).Item1);

        var changed = new List<string>();
        ((INotifyPropertyChanged)item).PropertyChanged += (_, e) => changed.Add(e.PropertyName ?? "");
        return (store, item, changed);
    }

    [Fact]
    public async Task ARefreshThatBringsBackTheSame_AnnouncesNothing_AndIsNeverBusy()
    {
        var (store, item, changed) = await ReadOneAsync();
        store.Things["a"] = new Thing { Id = "a", Name = "Alpha", UpdateUtcTick = 1 };   // a new object, the same value

        Assert.True((await item.RefreshAsync()).Item1);

        Assert.Empty(changed);                               // not Data, not IsBusy, not IsLoaded, not State
        Assert.Equal("Alpha", item.Data!.Name);
        Assert.Contains("read", store.Calls);
    }

    [Fact]
    public async Task ARefreshThatBringsBackAChange_AppliesIt_AndSaysSo_StillNeverBusy()
    {
        var (store, item, changed) = await ReadOneAsync();
        // The name changed and the stamp did not: compared field for field, not by the stamp.
        store.Things["a"] = new Thing { Id = "a", Name = "Alpha, renamed", UpdateUtcTick = 1 };

        Assert.True((await item.RefreshAsync()).Item1);

        Assert.Equal("Alpha, renamed", item.Data!.Name);
        Assert.Contains(nameof(item.Data), changed);
        Assert.DoesNotContain(nameof(item.IsBusy), changed);

        changed.Clear();
        Assert.True((await item.RefreshAsync()).Item1);       // and then the same again: nothing
        Assert.Empty(changed);
    }

    /// <summary>A refresh that started before an update, and lands after it, carries the item as it was: it is dropped.</summary>
    [Fact]
    public async Task ARefreshOvertakenByAWrite_IsDropped_AndTheWriteStands()
    {
        var (store, item, _) = await ReadOneAsync();
        var hold = new TaskCompletionSource();
        store.HoldNext = hold;
        var refresh = item.RefreshAsync();                   // asks now: "Alpha" - and is held

        store.Things["a"] = new Thing { Id = "a", Name = "Alpha, booked", UpdateUtcTick = 2 };
        Assert.True((await item.ReadAsync()).Item1);         // the act's read
        Assert.Equal("Alpha, booked", item.Data!.Name);

        hold.SetResult();
        Assert.True((await refresh).Item1);
        Assert.Equal("Alpha, booked", item.Data!.Name);      // not put back to what the poll asked for
    }

    [Fact]
    public async Task ARefreshThatFails_KeepsWhatWasShown_AndSaysWhy()
    {
        var (store, item, changed) = await ReadOneAsync();
        store.FailOn = _ => new Refused(503, "throttled");

        Assert.False((await item.RefreshAsync()).Item1);

        Assert.IsType<Refused>(item.LastException);
        Assert.Equal("Alpha", item.Data!.Name);
        Assert.Equal(new[] { nameof(item.LastException) }, changed);   // a failure is news, and the only news

        changed.Clear();
        store.FailOn = _ => null;
        Assert.True((await item.RefreshAsync()).Item1);
        Assert.Null(item.LastException);
        Assert.Equal(new[] { nameof(item.LastException) }, changed);   // and so is its end
    }

    [Fact]
    public async Task ARefresh_DoesNotOverwriteAnEditInProgress()
    {
        var (store, item, _) = await ReadOneAsync();
        Assert.True((await item.OpenEditAsync()).Item1);
        item.Data!.Name = "Typed by the user";
        store.Things["a"] = new Thing { Id = "a", Name = "Changed elsewhere", UpdateUtcTick = 2 };

        Assert.True((await item.RefreshAsync()).Item1);

        Assert.Equal("Typed by the user", item.Data!.Name);
    }

    [Fact]
    public async Task RefreshAsync_IsOnTheInterface()
    {
        var (store, concrete, _) = await ReadOneAsync();
        ILzItemViewModel<string, ThingModel> item = concrete;
        store.Things["a"] = new Thing { Id = "a", Name = "Beta", UpdateUtcTick = 1 };

        Assert.True((await item.RefreshAsync()).Item1);
        Assert.Equal("Beta", item.Data!.Name);
    }
}
