namespace LazyMagic.Client.ViewModels;
/// <summary>
/// This class manages a list of ViewModels
/// TVM is the ViewModel Class in the list
/// TDTO is the data transfer object that the TVM model uses
/// TModel is the data model derived from the TDTO that the TVM presents to views.
/// Remember: During construction, Assign SvcReadChildren or SvcReadChildrenId, and EntityName.
/// Also Implement NewViewModel()
/// </summary>
/// <typeparam name="TVM"></typeparam>
/// <typeparam name="TDTO"></typeparam>
/// <typeparam name="TModel"></typeparam>
public abstract class LzItemsViewModel<TVM, TDTO, TModel> : LzViewModel, INotifyCollectionChanged, ILzItemsViewModel<TVM, TDTO, TModel> where TDTO : class, new()
    where TModel : class, TDTO, IRegisterObservables, new()
    where TVM : class,  ILzItemViewModel<TModel>
{
    // Public Properties
    public LzItemsViewModel(
        ILzSessionViewModel sessionViewModel,
        IDictionary<string, TDTO>? dtos = null,
        IDictionary<string, TModel>? models = null
        )
    {
        _LzBaseSessionViewModel = sessionViewModel ?? throw new ArgumentNullException(nameof(sessionViewModel));
        Models = models;  
        DTOs = dtos;

        CanList = true;
        CanAdd = true;
    }
    public string? Id { get; set; }
    public virtual Dictionary<string, TVM> ViewModels { get; set; } = new();
  
    public bool SourceIsList { get; set; }  
    private TVM? currentViewModel;
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
    [Reactive] public TVM? LastViewModel { get; set; }
    protected int changeCount;
    public event NotifyCollectionChangedEventHandler? CollectionChanged;
    public bool IsChanged
    {
        get => changeCount > 0;
        set => this.RaiseAndSetIfChanged(ref changeCount, changeCount + 1);
    }
    public bool AutoReadChildren { get; set; } = true;
    [Reactive] public bool IsLoaded { get; set; }
    [Reactive] public bool IsLoading { get; set; }
    [Reactive] public long LastLoadTick { get; set; }
    [Reactive] public bool CanList { get; set; }
    [Reactive] public bool CanAdd { get; set; }
    [Reactive] public virtual long UpdateCount { get; set; }
    public IDictionary<string, TModel>? Models { get; set; }
    public IDictionary<string, TDTO>? DTOs { get; set; }    

    // Protected Properties 
    protected ILzSessionViewModel _LzBaseSessionViewModel { get; init; }

    // Storage Access
    protected Func<string, Task<ICollection<TDTO>>>? _DTOReadListIdAsync { get; init; }
    protected Func<Task<ICollection<TDTO>>>? _DTOReadListAsync { get; init; }
    protected string _EntityName { get; set; } = string.Empty;

    // Public Methods
    public virtual void Clear()
    {
        ViewModels.Clear();
        IsLoaded = false;
        IsChanged = true;
    }   
    public virtual async Task<(bool, string)> ReadAsync(bool forceload = false)
        => await ReadAsync(string.Empty, forceload);
    public virtual async Task<(bool, string)> ReadAsync(string id, bool forceload = false)
    {
        var userMsg = "Can't read " + _EntityName + " id:" + id;
        try
        {
            CheckAuth();  
            if(string.IsNullOrEmpty(id) && _DTOReadListAsync == null)
                throw new Exception("SvcReadList function not assigned");   
            if(!string.IsNullOrEmpty(id) && _DTOReadListIdAsync == null)
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
    public virtual string GetId(TDTO dto)
        => throw new NotImplementedException();
    public virtual async Task<(bool, string)> CancelCurrentViewModelEditAsync()
    {
        try
        {
            if (CurrentViewModel == null)
                return (false, "CurrentViewModel is null");
            if (CurrentViewModel.State != LzItemViewModelState.New && CurrentViewModel.State != LzItemViewModelState.Edit)
                throw new Exception("State != Edit && State != New");
            await CurrentViewModel.CancelEditAsync();
            if (CurrentViewModel.State == LzItemViewModelState.New)
            {
                if (LastViewModel?.Id != null && ViewModels.ContainsKey(LastViewModel.Id!))
                    CurrentViewModel = LastViewModel;
                else
                    CurrentViewModel = null;
                return (true, string.Empty);
            }
            return (true, String.Empty);
        }
        catch (Exception ex)
        {
            return (false, Log(string.Empty, ex.Message));
        }
    }
    public virtual async Task<(bool, string)> SaveCurrentViewModelAsync(string? id)
    {
        if (CurrentViewModel == null)
            return (false, "CurrentViewModel is null");
        var isAdd = CurrentViewModel.State == LzItemViewModelState.New;
        var (success, msg) = await CurrentViewModel.SaveEditAsync(id);
        if (success && isAdd)
        {
            if (CurrentViewModel.Id == null)
                throw new Exception("ItemViewModel.Id is null");
            ViewModels.TryAdd(CurrentViewModel.Id, CurrentViewModel);
        }
        return (success, msg);
    }
    public virtual void CheckAuth()
    {
        return;
    }

    // Protected Methods
    public virtual (TVM viewmodel, string id) NewViewModel(TDTO dto)
        => throw new NotImplementedException();
    public virtual(TVM viewmodel, string id) NewViewModel(string key, TModel model)
        => throw new NotImplementedException();
    public virtual(TVM viewmodel, string id) NewViewModel(string key, TDTO dto)
        => throw new NotImplementedException();
    protected virtual async Task<(bool, string)> UpdateDataFromTextAsync(string jsonContent, bool forceload)
    {
        var items = JsonConvert.DeserializeObject<ICollection<TDTO>>(jsonContent);
        if (items == null) throw new Exception("UpdateDataFromJsonAsync returned null");
        return await UpdateDataAsync(items, forceload);
    }
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
    protected virtual async Task<(bool, string)> UpdateDataAsync(IDictionary<string,TModel> list, bool forceload)
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

}
