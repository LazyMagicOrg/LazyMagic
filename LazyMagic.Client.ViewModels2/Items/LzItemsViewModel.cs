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
    public TVM? EditViewModel { get; set; }
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
        IsChanged = true;
    }
    /// <inheritdoc/>
    public virtual async Task<(bool, string)> ReadAsync(bool forceload = false)
        => await ReadAsync(default(TId), forceload);
    /// <inheritdoc/>
    public virtual async Task<(bool, string)> ReadAsync(TId id, bool forceload = false)
    {
        var userMsg = "Can't read " + _EntityName + " id:" + id;
        LastException = null;
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
        var tasks = new List<Task<(bool success, string msg)>>();
        var read = new HashSet<TId>();
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
        RemoveWhatWasNotRead(read);
        await Task.WhenAll(tasks);
        var result = tasks
            .Where(x => x.Result.success == false)
            .Select(x => x.Result)
            .FirstOrDefault((success: true, msg: string.Empty));
        IsLoaded = result.success;
        return result;
    }
    /// <inheritdoc/>
    protected virtual async Task<(bool, string)> UpdateDataAsync(IDictionary<string, TModel> list, bool forceload)
    {
        var tasks = new List<Task<(bool success, string msg)>>();
        var read = new HashSet<TId>();
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
        RemoveWhatWasNotRead(read);
        await Task.WhenAll(tasks);
        var result = tasks.Where(x => x.Result.success == false).Select(x => x.Result).FirstOrDefault((success: true, msg: string.Empty));
        IsLoaded = result.success;
        return result;
    }
    /// <inheritdoc/>
    protected virtual async Task<(bool, string)> UpdateDataAsync(IDictionary<string, TDTO> list, bool forceload)
    {
        var tasks = new List<Task<(bool success, string msg)>>();
        var read = new HashSet<TId>();
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
        RemoveWhatWasNotRead(read);
        await Task.WhenAll(tasks);
        var result = tasks.Where(x => x.Result.success == false).Select(x => x.Result).FirstOrDefault((success: true, msg: string.Empty));
        IsLoaded = result.success;
        return result;
    }

    /// <summary>
    /// With <see cref="ReplaceOnRead"/>, drop every view model the read just made did not return. One that is the
    /// current, the last or the one being edited stops being it: it is no longer in the collection.
    /// </summary>
    protected virtual void RemoveWhatWasNotRead(HashSet<TId> read)
    {
        if (!ReplaceOnRead)
            return;
        foreach (var gone in ViewModels.Keys.Where(key => !read.Contains(key)).ToList())
        {
            var removed = ViewModels[gone];
            ViewModels.Remove(gone);
            if (ReferenceEquals(CurrentViewModel, removed)) CurrentViewModel = null;
            if (ReferenceEquals(LastViewModel, removed)) LastViewModel = null;
            if (ReferenceEquals(EditViewModel, removed)) EditViewModel = null;
        }
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
    }
    /// <inheritdoc/>
    public virtual async Task ItemDeleted(TId id)
    {
        await Task.Delay(0);
        var (prevKey, nextKey) = GetAdjacentKeys(id!);
        if(prevKey != null)
            CurrentViewModel = ViewModels[prevKey!];
        else if (nextKey != null)
            CurrentViewModel = ViewModels[nextKey!];
        else
            CurrentViewModel = null;

        ViewModels.Remove(id!);
        EditViewModel = null;   
    }

    /// <inheritdoc/>
    public virtual async Task ItemUpdated(TId id)
    {
        EditViewModel = null;
        await Task.Delay(0);
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
