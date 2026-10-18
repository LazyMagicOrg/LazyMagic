namespace LazyMagic.Client.ViewModels;

public abstract partial class LzItemsViewModel<TId, TVM, TDTO, TModel> : LzViewModel,
    ILzItemsViewModel<TId, TVM, TDTO, TModel> where TDTO : class, new()
    where TModel : class, TDTO, IRegisterObservables, new()
    where TVM : class, ILzItemViewModel<TId, TModel>
{
    // Public Properties
    public LzItemsViewModel(
        ILoggerFactory loggerFactory
        ) : base(loggerFactory)
    {
    }

    /// <inheritdoc/>
    public TId? Id { get; set; }
    /// <inheritdoc/>
    public virtual Dictionary<TId, TVM> ViewModels { get; set; } = new();
    /// <inheritdoc/>
    private TVM? currentViewModel;
    /// <inheritdoc/>
    public TVM? CurrentViewModel
    {
        get => currentViewModel;
        set
        {
            if (value != null && value != LastViewModel && value!.State != LzItemViewModelState.New)
                LastViewModel = value;
            this.RaiseAndSetIfChanged(ref currentViewModel, value);
        }
    }

    /// <inheritdoc/>
    /// <remarks>Reactive: a component drawn from this collection draws again when a form is opened or closed.</remarks>
    [Reactive] public partial TVM? EditViewModel { get; set; }
    /// <inheritdoc/>
    [Reactive] public partial TVM? LastViewModel { get; set; }
    protected int changeCount;
    /// <inheritdoc/>
    public event NotifyCollectionChangedEventHandler? CollectionChanged;
    /// <inheritdoc/>
    public bool IsChanged
    {
        get => changeCount > 0;
        set => this.RaiseAndSetIfChanged(ref changeCount, changeCount + 1);
    }
    /// <inheritdoc/>
    public bool AutoReadChildren { get; set; } = true;
    /// <inheritdoc/>
    [Reactive] public partial bool IsLoaded { get; set; }
    /// <inheritdoc/>
    [Reactive] public partial bool IsLoading { get; set; }
    /// <inheritdoc/>
    [Reactive] public partial long LastLoadTick { get; set; }
    /// <summary>
    /// When true, a read makes the collection WHAT WAS READ: a view model whose item the read did not return is
    /// removed. False by default, as it always was - a read adds and replaces and never removes, which suits a
    /// collection filled a page or a parent at a time. Set it for a collection whose read returns the whole set,
    /// so an item deleted elsewhere does not stay on screen.
    /// </summary>
    public bool ReplaceOnRead { get; set; }
    /// <summary>
    /// When true, the collection is READ AGAIN after an item is created, updated or deleted - the read it was last
    /// given (<see cref="ReadAgainAsync"/>). False by default, as it always was: an item's change touches that item
    /// and no other. Set it for a collection where a write to one item changes others on the service - a flag only
    /// one item may carry, an order, a total - so the collection is what the service holds, and every item has the
    /// version it now needs for its own next write. Usually together with <see cref="ReplaceOnRead"/>.
    /// </summary>
    public bool ReadAfterChange { get; set; }
    private bool _hasRead;
    private TId? _lastReadId;
    /// <summary>
    /// The exception that made the last ReadAsync return false, or null when it succeeded. ReadAsync returns a
    /// message built for a log; an application that has to tell a refusal from an outage, or show what the
    /// service said, reads it here. Cleared at the start of each read.
    /// </summary>
    public Exception? LastException { get; protected set; }
    /// <inheritdoc/>
    public IDictionary<TId, TDTO>? DTOs { get; set; }
    // Storage Access
    /// <inheritdoc/>
    protected Func<TId, Task<ICollection<TDTO>>>? _DTOReadListIdAsync { get; init; }
    /// <inheritdoc/>
    protected Func<Task<ICollection<TDTO>>>? _DTOReadListAsync { get; init; }
    /// <inheritdoc/>
    protected string _EntityName { get; set; } = string.Empty;
    // Public Methods
    /// <inheritdoc/>
    public virtual void Clear()
    {
        ViewModels.Clear();
        IsLoaded = false;
        CollectionHasChanged();
    }
    /// <inheritdoc/>
    public virtual async Task<(bool, string)> ReadAsync(bool forceload = false)
        => await ReadAsync(default(TId), forceload);
    /// <inheritdoc/>
    public virtual async Task<(bool, string)> ReadAsync(TId id, bool forceload = false)
    {
        var userMsg = "Can't read " + _EntityName + " id:" + id;
        LastException = null;
        _hasRead = true;
        _lastReadId = id;
        try
        {
            CheckAuth();
            if (id == null && _DTOReadListAsync == null)
                throw new Exception("SvcReadList function not assigned");
            // Was `id == null`: a read with no id threw here unless the by-id delegate was assigned too, so a
            // collection given only _DTOReadListAsync could never be read. LazyMagic.Client.ViewModels has it right.
            if (id != null && _DTOReadListIdAsync == null)
                throw new Exception("SvcReadListId function not assigned");
            IsLoading = true;
            var items = (id != null)
                ? await _DTOReadListIdAsync!(id)
                : await _DTOReadListAsync!();
            return await UpdateDataAsync(items, forceload);
        }
        catch (Exception ex)
        {
            LastException = ex;
            return (false, Log(userMsg, ex.Message));
        }
        finally { IsLoading = false; }
    }
    /// <summary>
    /// Read the collection again as it was last read: with the same parent id, or with none. Before any read, with
    /// <see cref="Id"/>. What <see cref="ReadAfterChange"/> calls.
    /// </summary>
    public virtual Task<(bool, string)> ReadAgainAsync()
        => ReadAsync(_hasRead ? _lastReadId! : Id!, forceload: true);

    /// <summary>
    /// A QUIET READ, FOR A POLL (2026-10-17): the collection read again as it was last read (<see cref="ReadAgainAsync"/>),
    /// but never setting <see cref="IsLoading"/>, and leaving the collection alone - no view model replaced, no
    /// notification - when what came back has the same <see cref="ReadSignature"/> as the last read applied: by default
    /// the whole list, serialized, field for field. Otherwise it is applied as a read applies it. A failure is kept in
    /// <see cref="LastException"/>; the collection keeps what it showed. <see cref="LastCheckedTick"/> moves on every
    /// success, announcing nothing.
    /// </summary>
    public virtual async Task<(bool, string)> RefreshAsync()
    {
        var id = _hasRead ? _lastReadId : Id;
        var userMsg = "Can't refresh " + _EntityName + " id:" + id;
        var failedBefore = LastException != null;
        try
        {
            CheckAuth();
            if (id == null && _DTOReadListAsync == null)
                throw new Exception("SvcReadList function not assigned");
            if (id != null && _DTOReadListIdAsync == null)
                throw new Exception("SvcReadListId function not assigned");
            var appliedAtStart = _applied;
            var items = id != null ? await _DTOReadListIdAsync!(id) : await _DTOReadListAsync!();
            LastException = null;
            if (failedBefore) this.RaisePropertyChanged(nameof(LastException));   // a page saying it was stale stops saying so
            if (_applied != appliedAtStart)
                return (true, string.Empty);              // overtaken: a newer list was applied while this one was out
            LastCheckedTick = DateTime.UtcNow.Ticks;
            if (_appliedSignature != null && string.Equals(_appliedSignature, SignatureOf(items), StringComparison.Ordinal))
                return (true, string.Empty);
            return await UpdateDataAsync(items, forceload: true);
        }
        catch (Exception ex)
        {
            LastException = ex;
            if (!failedBefore) this.RaisePropertyChanged(nameof(LastException));  // a failure is news: the page says it is stale
            return (false, Log(userMsg, ex.Message));
        }
    }

    /// <summary>When the collection was last read or refreshed successfully. Not reactive: a poll that changed nothing
    /// announces nothing.</summary>
    public long LastCheckedTick { get; protected set; }

    /// <summary>The signature of the last list a read applied; null before one.</summary>
    private string? _appliedSignature;

    /// <summary>
    /// How many times a list has been applied - by a read, a refresh, or anything else that applies one. A refresh notes
    /// it as it starts, and a refresh that lands after another list was applied is OVERTAKEN: what it carries is older
    /// than what is shown, and it is dropped (2026-10-18: a poll that started during a buyer's "clear my requests" landed
    /// after the read that followed it, and put the cleared rows back until the next tick).
    /// </summary>
    private long _applied;

    /// <summary>
    /// What a refresh compares to tell a changed read from an unchanged one: the list, serialized. Override to take in
    /// what else a read sets beside the list (a total, counts), so a change there is not missed.
    /// </summary>
    protected virtual string ReadSignature(ICollection<TDTO> list) => JsonConvert.SerializeObject(list);

    /// <summary>
    /// The signature, or null when it cannot be made: a generated DTO that marks a member DisallowNull refuses to be
    /// written with it null, and a read must never fail for want of a comparison. Null compares unequal, so the read is
    /// applied as a read always was.
    /// </summary>
    private string? SignatureOf(ICollection<TDTO> list)
    {
        try { return ReadSignature(list); }
        catch (JsonException) { return null; }
    }

    /// <summary>
    /// START A NEW ITEM: a view model for <paramref name="dto"/> made as every other is (<see cref="NewViewModel(TDTO)"/>),
    /// in the New state and not loaded, as the one being edited (<see cref="EditViewModel"/>). It is not in the
    /// collection until it has been created in storage, when it tells the collection so (ItemCreated). Call its
    /// OpenEditAsync before binding a form to it, as for an item being changed.
    /// </summary>
    public virtual TVM NewItem(TDTO dto)
    {
        var (item, _) = NewViewModel(dto);
        item.State = LzItemViewModelState.New;
        item.IsLoaded = false;
        EditViewModel = item;
        return item;
    }

    /// <inheritdoc/>
    public virtual string GetId(TDTO dto)
        => throw new NotImplementedException();

    /// <inheritdoc/>
    public virtual void CheckAuth()
    {
        return;
    }

    // Protected Methods
    /// <inheritdoc/>
    public virtual (TVM viewmodel, TId id) NewViewModel(TDTO dto)
    /// <inheritdoc/>
        => throw new NotImplementedException();
    /// <inheritdoc/>
    public virtual (TVM viewmodel, TId id) NewViewModel(string key, TModel model)
        => throw new NotImplementedException();
    /// <inheritdoc/>
    public virtual (TVM viewmodel, TId id) NewViewModel(string key, TDTO dto)
        => throw new NotImplementedException();
    /// <inheritdoc/>
    protected virtual async Task<(bool, string)> UpdateDataFromTextAsync(string jsonContent, bool forceload)
    {
        var items = JsonConvert.DeserializeObject<ICollection<TDTO>>(jsonContent);
        if (items == null) throw new Exception("UpdateDataFromJsonAsync returned null");
        return await UpdateDataAsync(items, forceload);
    }
    /// <inheritdoc/>
    protected virtual async Task<(bool, string)> UpdateDataAsync(ICollection<TDTO> list, bool forceload)
    {
        _applied++;
        _appliedSignature = SignatureOf(list);
        var tasks = new List<Task<(bool success, string msg)>>();
        var read = new List<TId>();
        foreach (var item in list)
        {
            try
            {
                var (vm, itemMsg) = NewViewModel(item);
                var id = vm.Id;
                if (id is null)
                    throw new Exception("NewViewModel return null id");
                read.Add(id);
                if (!ViewModels!.ContainsKey(id))
                    ViewModels!.Add(id, vm);
                else
                    ViewModels![id] = vm;
                vm.State = LzItemViewModelState.Current;
                if (AutoReadChildren)
                    tasks.Add(ViewModels![id].ReadChildrenAsync(forceload));
            }
            catch
            {
                Console.WriteLine($"Could not load item:");
            }
        }
        MakeWhatWasRead(read);
        CollectionHasChanged();
        await Task.WhenAll(tasks);
        var result = tasks
            .Where(x => x.Result.success == false)
            .Select(x => x.Result)
            .FirstOrDefault((success: true, msg: string.Empty));
        IsLoaded = result.success;
        if (result.success) LastCheckedTick = LastLoadTick = DateTime.UtcNow.Ticks;   // declared, and never set before: when the collection was last read
        return result;
    }
    /// <inheritdoc/>
    protected virtual async Task<(bool, string)> UpdateDataAsync(IDictionary<string, TModel> list, bool forceload)
    {
        _applied++;
        var tasks = new List<Task<(bool success, string msg)>>();
        var read = new List<TId>();
        foreach (var item in list)
        {
            try
            {
                var (vm, itemMsg) = NewViewModel(item.Key, item.Value);
                var id = vm.Id;
                if (id is null)
                    throw new Exception("NewViewModel return null id");
                read.Add(id);
                if (!ViewModels!.ContainsKey(id))
                    ViewModels!.Add(id, vm);
                else
                    ViewModels![id] = vm;
                vm.State = LzItemViewModelState.Current;
                if (AutoReadChildren)
                    tasks.Add(ViewModels![id].ReadChildrenAsync(forceload));
            }
            catch
            {
                Console.WriteLine($"Could not load item:");
            }
        }
        MakeWhatWasRead(read);
        CollectionHasChanged();
        await Task.WhenAll(tasks);
        var result = tasks.Where(x => x.Result.success == false).Select(x => x.Result).FirstOrDefault((success: true, msg: string.Empty));
        IsLoaded = result.success;
        if (result.success) LastLoadTick = DateTime.UtcNow.Ticks;   // declared, and never set before: when the collection was last read
        return result;
    }
    /// <inheritdoc/>
    protected virtual async Task<(bool, string)> UpdateDataAsync(IDictionary<string, TDTO> list, bool forceload)
    {
        _applied++;
        var tasks = new List<Task<(bool success, string msg)>>();
        var read = new List<TId>();
        foreach (var item in list)
        {
            try
            {
                var (vm, itemMsg) = NewViewModel(item.Key, item.Value);
                var id = vm.Id;
                if (id is null)
                    throw new Exception("NewViewModel return null id");
                read.Add(id);
                if (!ViewModels!.ContainsKey(id))
                    ViewModels!.Add(id, vm);
                else
                    ViewModels![id] = vm;
                vm.State = LzItemViewModelState.Current;
                if (AutoReadChildren)
                    tasks.Add(ViewModels![id].ReadChildrenAsync(forceload));
            }
            catch
            {
                Console.WriteLine($"Could not load item:");
            }
        }
        MakeWhatWasRead(read);
        CollectionHasChanged();
        await Task.WhenAll(tasks);
        var result = tasks.Where(x => x.Result.success == false).Select(x => x.Result).FirstOrDefault((success: true, msg: string.Empty));
        IsLoaded = result.success;
        if (result.success) LastLoadTick = DateTime.UtcNow.Ticks;   // declared, and never set before: when the collection was last read
        return result;
    }

    /// <summary>
    /// With <see cref="ReplaceOnRead"/>, make the collection what the read returned, IN THE ORDER IT RETURNED IT:
    /// a view model whose item was not returned is dropped - and stops being the current, the last or the one
    /// being edited - and the rest are put in the read's order, so <c>ViewModels.Values</c> is the order the
    /// service gave. (A Dictionary enumerates in insertion order while nothing is removed from it, which is what
    /// GetAdjacentKeys has always relied on; this clears it and adds again.) Without ReplaceOnRead nothing is
    /// dropped and nothing is moved, as before.
    /// </summary>
    protected virtual void MakeWhatWasRead(List<TId> read)
    {
        if (!ReplaceOnRead)
            return;

        var kept = new List<KeyValuePair<TId, TVM>>();
        var seen = new HashSet<TId>();
        foreach (var id in read)
            if (seen.Add(id) && ViewModels.TryGetValue(id, out var viewModel))
                kept.Add(new KeyValuePair<TId, TVM>(id, viewModel));

        foreach (var gone in ViewModels.Where(entry => !seen.Contains(entry.Key)).Select(entry => entry.Value).ToList())
        {
            if (ReferenceEquals(CurrentViewModel, gone)) CurrentViewModel = null;
            if (ReferenceEquals(LastViewModel, gone)) LastViewModel = null;
            if (ReferenceEquals(EditViewModel, gone)) EditViewModel = null;
        }

        ViewModels.Clear();
        foreach (var entry in kept)
            ViewModels.Add(entry.Key, entry.Value);
    }

    /// <summary>
    /// SAY THAT THE COLLECTION CHANGED: <see cref="IsChanged"/> for a component that redraws on a property change,
    /// and <see cref="CollectionChanged"/> for one that listens for it. Called after a read, after an item is
    /// created or deleted, and by <see cref="Clear"/> - the event was declared and never raised, so a collection
    /// that changed without a read redrew nothing.
    /// </summary>
    protected virtual void CollectionHasChanged()
    {
        IsChanged = true;
        CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    // Callbacks from ItemViewModel 
    // We use callbacks insted of events to avoid lots of subscription management.
    // It is reasonable ot use both callbacks and event subscriptions for some 
    // implementation patterns. 


    /// <inheritdoc/>
    public virtual async Task ItemCreated(object itemViewModelParam, bool makeCurrentItem = true)
    {
        await Task.Delay(0);    
        var itemViewModel = itemViewModelParam as TVM;   
        if (itemViewModel == null)
            throw new ArgumentNullException(nameof(itemViewModel));
        if (itemViewModel.Id == null)
            throw new Exception("ItemViewModel.Id is null");
        if (ViewModels.ContainsKey(itemViewModel.Id))
            throw new Exception("ViewModels already contains key");
        
        ViewModels.Add(itemViewModel.Id, itemViewModel);
        EditViewModel = null;
        if (makeCurrentItem == true)
        {
            CurrentViewModel = itemViewModel; 
        }
        CollectionHasChanged();
        if (ReadAfterChange) await ReadAgainAsync();
    }
    /// <inheritdoc/>
    public virtual async Task ItemDeleted(TId id)
    {
        await Task.Delay(0);
        // OUT OF THE COLLECTION FIRST, and only then anything that is announced. A deleted item has no Data, and
        // setting CurrentViewModel tells a component to draw: it used to be set while the deleted item was still
        // in the collection, so a component drawing each item's Data met one that had none.
        var (prevKey, nextKey) = GetAdjacentKeys(id!);
        var next = prevKey != null ? ViewModels[prevKey!] : nextKey != null ? ViewModels[nextKey!] : null;
        ViewModels.Remove(id!);
        CurrentViewModel = next;
        EditViewModel = null;
        CollectionHasChanged();
        if (ReadAfterChange) await ReadAgainAsync();
    }

    /// <inheritdoc/>
    public virtual async Task ItemUpdated(TId id)
    {
        EditViewModel = null;
        await Task.Delay(0);
        if (ReadAfterChange) await ReadAgainAsync();
    }

    /// <inheritdoc/>
    public virtual async Task ItemUpdateCanceled(TId id)
    {
        EditViewModel = null;
        await Task.Delay(0);
    }

    /// <inheritdoc/>
    public virtual async Task ItemRead(TId id)
    {
        EditViewModel = null;
        await Task.Delay(0);
    }
    /// <inheritdoc/>
    protected virtual (TId? prevKey, TId? nextKey) GetAdjacentKeys(TId key)
    {
        if (!ViewModels.ContainsKey(key))
            return (default, default);

        var orderedKeys = ViewModels.Keys.ToList();
        int currentIndex = orderedKeys.IndexOf(key);

        TId? prevKey = currentIndex > 0
            ? orderedKeys[currentIndex - 1]
            : default;

        TId? nextKey = currentIndex < orderedKeys.Count - 1
            ? orderedKeys[currentIndex + 1]
            : default;

        return (prevKey, nextKey);
    }

}
