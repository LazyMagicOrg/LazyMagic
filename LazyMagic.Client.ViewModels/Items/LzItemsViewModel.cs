using Microsoft.Extensions.Logging;

namespace LazyMagic.Client.ViewModels;

public abstract class LzItemsViewModel<TVM, TDTO, TModel> : LzViewModel,
    ILzItemsViewModel<TVM, TDTO, TModel> where TDTO : class, new()
    where TModel : class, TDTO, IRegisterObservables, new()
    where TVM : class, ILzItemViewModel<TModel>
{
    // Public Properties
    public LzItemsViewModel(
        ILoggerFactory loggerFactory,
        ILzSessionViewModel sessionViewModel
        ) : base(loggerFactory)
    {
        _LzBaseSessionViewModel = sessionViewModel ?? throw new ArgumentNullException(nameof(sessionViewModel));
    }

    /// <inheritdoc/>
    public string? Id { get; set; }
    /// <inheritdoc/>
    public virtual Dictionary<string, TVM> ViewModels { get; set; } = new();
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
    [Reactive] public TVM? LastViewModel { get; set; }
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
    [Reactive] public bool IsLoaded { get; set; }
    /// <inheritdoc/>
    [Reactive] public bool IsLoading { get; set; }
    /// <inheritdoc/>
    [Reactive] public long LastLoadTick { get; set; }
    /// <inheritdoc/>
    public IDictionary<string, TDTO>? DTOs { get; set; }
    // Protected Properties 
    /// <inheritdoc/>
    protected ILzSessionViewModel _LzBaseSessionViewModel { get; init; }
    // Storage Access
    /// <inheritdoc/>
    protected Func<string, Task<ICollection<TDTO>>>? _DTOReadListIdAsync { get; init; }
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
        => await ReadAsync(string.Empty, forceload);
    /// <inheritdoc/>
    public virtual async Task<(bool, string)> ReadAsync(string id, bool forceload = false)
    {
        var userMsg = "Can't read " + _EntityName + " id:" + id;
        try
        {
            CheckAuth();
            if (string.IsNullOrEmpty(id) && _DTOReadListAsync == null)
                throw new Exception("SvcReadList function not assigned");
            if (!string.IsNullOrEmpty(id) && _DTOReadListIdAsync == null)
                throw new Exception("SvcReadListId function not assigned");
            IsLoading = true;
            var items = (!string.IsNullOrEmpty(id))
                ? await _DTOReadListIdAsync!(id)
                : await _DTOReadListAsync!();
            return await UpdateDataAsync(items, forceload);
        }
        catch (Exception ex)
        {
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
    public virtual (TVM viewmodel, string id) NewViewModel(TDTO dto)
    /// <inheritdoc/>
        => throw new NotImplementedException();
    /// <inheritdoc/>
    public virtual (TVM viewmodel, string id) NewViewModel(string key, TModel model)
        => throw new NotImplementedException();
    /// <inheritdoc/>
    public virtual (TVM viewmodel, string id) NewViewModel(string key, TDTO dto)
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
        foreach (var item in list)
        {
            try
            {
                var (vm, itemMsg) = NewViewModel(item);
                var id = vm.Id;
                if (id is null)
                    throw new Exception("NewViewModel return null id");
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
        foreach (var item in list)
        {
            try
            {
                var (vm, itemMsg) = NewViewModel(item.Key, item.Value);
                var id = vm.Id;
                if (id is null)
                    throw new Exception("NewViewModel return null id");
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
        await Task.WhenAll(tasks);
        var result = tasks.Where(x => x.Result.success == false).Select(x => x.Result).FirstOrDefault((success: true, msg: string.Empty));
        IsLoaded = result.success;
        return result;
    }
    /// <inheritdoc/>
    protected virtual async Task<(bool, string)> UpdateDataAsync(IDictionary<string, TDTO> list, bool forceload)
    {
        var tasks = new List<Task<(bool success, string msg)>>();
        foreach (var item in list)
        {
            try
            {
                var (vm, itemMsg) = NewViewModel(item.Key, item.Value);
                var id = vm.Id;
                if (id is null)
                    throw new Exception("NewViewModel return null id");
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
        await Task.WhenAll(tasks);
        var result = tasks.Where(x => x.Result.success == false).Select(x => x.Result).FirstOrDefault((success: true, msg: string.Empty));
        IsLoaded = result.success;
        return result;
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
    public virtual async Task ItemDeleted(string id)
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
    public virtual async Task ItemUpdated(string id)
    {
        EditViewModel = null;
        await Task.Delay(0);
    }

    /// <inheritdoc/>
    public virtual async Task ItemUpdateCanceled(string id)
    {
        EditViewModel = null;
        await Task.Delay(0);
    }

    /// <inheritdoc/>
    public virtual async Task ItemRead(string id)
    {
        EditViewModel = null;
        await Task.Delay(0);
    }
    /// <inheritdoc/>
    protected virtual (string? prevKey, string? nextKey) GetAdjacentKeys(string key)
    {
        if (!ViewModels.ContainsKey(key))
            return (null, null);

        var orderedKeys = ViewModels.Keys.ToList();
        int currentIndex = orderedKeys.IndexOf(key);

        string? prevKey = currentIndex > 0
            ? orderedKeys[currentIndex - 1]
            : null;

        string? nextKey = currentIndex < orderedKeys.Count - 1
            ? orderedKeys[currentIndex + 1]
            : null;

        return (prevKey, nextKey);
    }

}
