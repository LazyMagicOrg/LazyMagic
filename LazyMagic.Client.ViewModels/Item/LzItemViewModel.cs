namespace LazyMagic.Client.ViewModels;

/// <summary>
/// ItemViewModelBase<T,TEdit>
/// </summary>
/// <typeparam name="TDTO">DTO Type</typeparam>
/// <typeparam name="TModel">Model Type (extended model off of TDTO)</typeparam>
public abstract class LzItemViewModel<TDTO, TModel> : LzViewModel, ILzItemViewModel<TModel>
    where TDTO : class, new()
    where TModel : class, TDTO, IRegisterObservables, new()
{
    // Public Properties
    public LzItemViewModel(ILzSessionViewModel sessionViewModel, TDTO? dto = null, TModel? model = null, bool? isLoaded = null)
    {
        LzBaseSessionViewModel = sessionViewModel;    
        CanCreate = true;
        CanRead = true;
        CanUpdate = true;
        CanDelete = true;
        IsLoaded = false;
        IsDirty = false;
       

        this.WhenAnyValue(x => x.State, (x) => x == LzItemViewModelState.New)
            .ToPropertyEx(this, x => x.IsNew);

        this.WhenAnyValue(x => x.State, (x) => x == LzItemViewModelState.Edit)
            .ToPropertyEx(this, x => x.IsEdit);

        this.WhenAnyValue(x => x.State, (x) => x == LzItemViewModelState.Current)
            .ToPropertyEx(this, x => x.IsCurrent);

        this.WhenAnyValue(x => x.State, (x) => x == LzItemViewModelState.Deleted)
            .ToPropertyEx(this, x => x.IsDeleted);

        if (model is not null && dto is not null)
            throw new Exception("itemModel and itemDTO cannot both be assigned.");

        if(dto is not null)
            _DTO = dto;

        // Init Model Data 
        if (model != null)
        {
            Data = model;
            State = LzItemViewModelState.Current;
            IsLoaded = true;
        }
        else
        {
            if (dto != null)
                dto.DeepCloneTo(Data = new());
            State = (Data == null) ? LzItemViewModelState.New : LzItemViewModelState.Current;
            IsLoaded = isLoaded ??= Data != null;
            Data ??= new();
        }
        Data.RegisterObservables();

    }
    public bool AutoLoadChildren { get; set; } = true;
    public abstract string? Id { get; }
    public abstract long UpdatedAt { get; }
    
    [Reactive] public TModel? Data { get; set; }
    [Reactive] public LzItemViewModelState State { get; set; }
    [Reactive] public bool CanCreate { get; set; }
    [Reactive] public bool CanRead { get; set; }
    [Reactive] public bool CanUpdate { get; set; }
    [Reactive] public bool CanDelete { get; set; }
    [Reactive] public bool IsLoaded { get; set; }
    [Reactive] public virtual long UpdateCount { get; set; }
    [ObservableAsProperty] public bool IsNew { get; }
    [ObservableAsProperty] public bool IsEdit { get; }
    [ObservableAsProperty] public bool IsCurrent { get; }
    [ObservableAsProperty] public bool IsDeleted { get; }
    [Reactive] public bool IsDirty { get; set; }
    public ILzParentViewModel? ParentViewModel { get; set; }

    // protected Properties
    protected TDTO? _DTO { get; init; } 
    protected ILzSessionViewModel LzBaseSessionViewModel { get; init; }
    protected string _EntityName { get; init; } = string.Empty;
    protected string _DataCopyJson = string.Empty;
    // Storage 
    protected Func<TDTO, Task<TDTO>>? _DTOCreateAsync { get; init; } // Assumes storage Id is in TDTO
    protected Func<string, TDTO, Task<TDTO>>? _DTOCreateIdAsync { get; init; } // Assumes storage Id is passed separate from TDTO
    protected Func<string, Task<TDTO>>? _DTOReadIdAsync { get; init; }
    protected Func<Task<TDTO>>? _DTOReadAsync { get; init; } // Read using this.Id
    protected Func<TDTO, Task<TDTO>>? _DTOUpdateAsync { get; init; } // Assumes storage Id is in TDTO
    protected Func<string, TDTO, Task<TDTO>>? _DTOUpdateIdAsync { get; init; } // Assumes storage Id is passed separate from TDTO
    protected Func<string, Task>? _DTODeleteIdAsync { get; init; }
    protected void CheckId(string? id)
    {
        if (id is null)
            throw new Exception("Id is null");
    }

    // Public Methods
    public virtual void CheckAuth()
    {
    }
    public virtual async Task<(bool, string)> CreateAsync(string? id)
    {

        try
        {
            if (!CanCreate)
                throw new Exception("Create not authorized");

            if (State != LzItemViewModelState.New)
                throw new Exception("State != New");

            if (Data == null)
                throw new Exception("Data not assigned");

            var item = (TDTO)Data;

            if (!Validate())
                throw new Exception("Validation failed.");

            CheckAuth();

            if (id is null)
            {
                if (_DTOCreateAsync == null)
                    throw new Exception("CreateAsync not assigned.");
                item = await _DTOCreateAsync(item!);
            }
            else
            {
                if (_DTOCreateIdAsync == null)
                    throw new Exception("CreateIdAsync not assigned.");
                CheckId(id);
                item = await _DTOCreateIdAsync(id!, item!);
            }
            UpdateData(item);

            State = LzItemViewModelState.Current;
            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            return (false, Log(MethodBase.GetCurrentMethod()!, ex.Message));
        }
    }
    public virtual async Task<(bool, string)> ReadAsync(string id)
    {
        var userMsg = "Can't load " + _EntityName;
        try
        {
            if (!CanRead) 
                throw new Exception("Read not authorized");

            CheckAuth();
            CheckId(id);

            // Perform storage operation
            if (_DTOReadIdAsync == null)
                throw new Exception("SvcReadIdAsync not assigned.");
            UpdateData(await _DTOReadIdAsync(id));
            State = LzItemViewModelState.Current;

            if (AutoLoadChildren)
                return await ReadChildrenAsync(forceload: true);

            return (true, string.Empty);
        }
        catch (Exception ex)
        {

            return (false, Log(userMsg + " " + MethodBase.GetCurrentMethod()!, ex.Message));
        }
    }
    // ReadAsync without an Id is used to read from an API, often where  the API uses the 
    // logged in identity of the caller as an id for data retrieval. The identity of the 
    // caller is contained in the JWT or Authentication Signature so as to make it 
    // impossible for a sniffer to see the id of the data requested. 
    public virtual async Task<(bool, string)> ReadAsync()
    {
        try
        {
            if (!CanRead)
                throw new Exception("Read not authorized");

            CheckAuth();

            // Perform storage operation
            if (_DTOReadAsync == null)
                throw new Exception("SvcReadAsync not assigned.");
            UpdateData(await _DTOReadAsync());

            // Id = Data!.Id;
            State = LzItemViewModelState.Current;
            IsLoaded = true;
            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            return (false, Log(MethodBase.GetCurrentMethod()!, ex.Message));
        }
    }
    public virtual async Task<(bool, string)> UpdateAsync(string? id)
    {
        try
        {
            if (!CanUpdate)
                throw new Exception("Update not authorized");

            // Todo: Review usecases to see if we need this.
            //if (State != LzItemViewModelState.Edit)
            //    throw new Exception("State != Edit.");

            if (Data is null)
                throw new Exception("Data not assigned");

            if (!Validate())
                throw new Exception("Validation failed.");

            CheckAuth();

            if (id is null)
            {
                if (_DTOUpdateAsync == null)
                    throw new Exception("SvcUpdateAsync is not assigned.");
                UpdateData(await _DTOUpdateAsync((TDTO)Data!));
            }
            else
            {
                if (_DTOUpdateIdAsync == null)
                    throw new Exception("SvcUpdateIdAsync is not assigned.");
                CheckId(id);
                UpdateData(await _DTOUpdateIdAsync(id, (TDTO)Data!));
            }
            State = LzItemViewModelState.Current;
            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            return (false, Log(MethodBase.GetCurrentMethod()!, ex.Message));
        }
    }
    public virtual async Task<(bool, string)> SaveEditAsync(string? id)
    {
        try
        {
            var (success, msg) =
                State == LzItemViewModelState.New
                ? await CreateAsync(id)
                : await UpdateAsync(id);

            State = LzItemViewModelState.Current;
            IsLoaded = true;
            return (success, msg);
        }
        catch (Exception ex)
        {
            return (false, Log(MethodBase.GetCurrentMethod()!, ex.Message));
        }
    }
    public virtual async Task<(bool,string)> DeleteAsync(string id)
    {
        try
        {
            if (!CanDelete)
                throw new Exception("Delete(id) not authorized.");

            if (State != LzItemViewModelState.Current)
                throw new Exception("State != Current");

            CheckAuth();
            CheckId(id); 
            if (_DTODeleteIdAsync == null)
                throw new Exception("SvcDelete(id) is not assigned.");
            await _DTODeleteIdAsync(Id!);

            State = LzItemViewModelState.Deleted;
            Data = null;
            IsDirty = false;
            return(true,String.Empty);

        }
        catch (Exception ex)
        {
            return (false, Log(MethodBase.GetCurrentMethod()!, ex.Message));
        }
    }
    public virtual void OpenEdit(bool forceCopy = false)
    {
        if (!forceCopy && State == LzItemViewModelState.Edit)
            return;

        if (State != LzItemViewModelState.New)
            State = LzItemViewModelState.Edit;
        MakeDataCopy();
    }
    public virtual Task OpenEditAsync(bool forceCopy = false)
    {
        if (!forceCopy && State == LzItemViewModelState.Edit)
            return Task.CompletedTask;

        if(State != LzItemViewModelState.New)
            State = LzItemViewModelState.Edit;
        MakeDataCopy();
        return Task.CompletedTask;
    }
    public virtual (bool, string) CancelEdit()
    {
        if (State != LzItemViewModelState.Edit && State != LzItemViewModelState.New)
            return (false, Log(MethodBase.GetCurrentMethod()!, "No Active Edit"));

        State = (IsLoaded) ? LzItemViewModelState.Current : LzItemViewModelState.New;

        RestoreFromDataCopy();
        return (true, String.Empty);
    }
    public virtual async Task<(bool,string)> CancelEditAsync()
    {
        await Task.Delay(0);
        return CancelEdit();
    }
    public virtual bool Validate()
    {
        return true;
    }

    public virtual async Task<(bool, string)> ReadChildrenAsync(bool forceload)
    {
        await Task.Delay(0);
        return (true, string.Empty);
    }
    // Protected Methods

    /// <summary>
    /// We use PopulateObject to update the Data object to 
    /// preserve any event subscriptions.
    /// </summary>
    /// <param name="item"></param>
    protected virtual void UpdateData(TDTO item)
    {

        Data ??= new();
        var json = JsonConvert.SerializeObject(item);
        JsonConvert.PopulateObject(json, Data);
        IsDirty = false;
        this.RaisePropertyChanged(nameof(Data));
    }
    /// <summary>
    /// This method uses a json copy of the data. 
    /// Saving data using JSON is not fast. Using Force.DeepCloner
    /// for DataCopy is not possible because the clone process 
    /// fails if the source data has event subscriptions.
    /// It is unlikely that MakeDataCopy is ever used in a use case 
    /// where performance is critical. If your use case requires 
    /// optimization, override this method (and the RestoreFromDataCopy method)
    /// and use individual property assignments. 
    /// </summary>
    protected virtual void MakeDataCopy()
    {
        Data ??= new();
        _DataCopyJson = JsonConvert.SerializeObject((TDTO)Data);
    }
    /// <summary>
    /// This method uses a json copy of the data. 
    /// Saving data using JSON is not fast. Using Force.DeepCloner
    /// for DataCopy is not possible because the clone process 
    /// fails if the source data has event subscriptions.
    /// It is unlikely that RestoreFromDataCopy is ever used in a use case 
    /// where performance is critical. If your use case requires 
    /// optimization, override this method (and the MakeDataCopy method)
    /// and use individual property assignments. 
    /// </summary>
    protected virtual void RestoreFromDataCopy()
    {
        // Restoring data from JSON is not fast. Using Force.DeepCloner 
        // DeepCloneTo(Data) is not possible because it overwrites any
        // event subscriptions.
        Data ??= new();
        JsonConvert.PopulateObject(_DataCopyJson, Data);
    }

    /// <summary>
    /// We use PopulateObject to update the Data object to 
    /// preserve any event subscriptions.
    /// </summary>
    /// <param name="item"></param>
    protected virtual void UpdateData(TModel item)
    {
        Data = item;
        IsDirty = false;
        this.RaisePropertyChanged(nameof(Data));
    }

    // Model Storage API Methods
    // Since the Data property points to the single instance of the model, these 
    // methods are essentially no-ops. They are here to provide a consistent processing
    // pattern for all storage APIs and allow the introduction of side effects
    // associated with each action. For instance, you might want to perform 
    // referential integrity checks in the Create and Update methods if you are 
    // not using Fluent Validation or some other validation library. 
    protected virtual async Task<TModel> ModelCreateAsync(TModel body)
    {
        // Perform any referential integrity checks here.
        await Task.Delay(0);
        return body;
    }
    protected async Task<TModel> ModelReadAsync(string id)
    {
        await Task.Delay(0);
        return Data!;
    }
    protected async Task<TModel> ModelUpdateAsync(TModel body)
    {
        if(_DTO is not null)
            ((TDTO)Data!).DeepCloneTo(_DTO);
        // Perform any referential integrity checks here
        await Task.Delay(0);
        return Data!;
    }
    protected async Task<TModel> ModelUpdateIdAsync(string id, TModel body)
    {
        // Perform any referential integrity checks here
        await Task.Delay(0);
        return Data!;
    }
    protected async Task ModelDeleteAsync(string id)
    {
        // Perform any referential integrity checks here
        await Task.Delay(0);
    }
}
