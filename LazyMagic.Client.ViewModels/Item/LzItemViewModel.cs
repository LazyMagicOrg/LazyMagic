

namespace LazyMagic.Client.ViewModels;

/// <summary>
/// ItemViewModelBase<T,TEdit>
/// This abstract class provides CRUDL operations for a single item in storage. It 
/// also provides a set of properties and methods to manage the state of the item 
/// for editing. The State : New, Edit, Current, Deleted.
/// In general, a UI makes use of the OpenEditAsync(), SaveEditAsync(), DeleteAsync(), and CancelEditAsync() methods.
/// An LzItemViewModel often "belongs" to a LzItemsViewModel. If IParentViewModel exists, this class makes the 
/// following callbacks to it: 
///     ItemCreated(object itemViewModel, bool makeCurrentItem = true), 
///     ItemRead(string id).
///     ItemUpdated(string id), 
///     ItemDeleted(string id), 
///     ItemUpdateCanceled(string id), 
/// </summary>
/// <typeparam name="TDTO">DTO Type</typeparam>
/// <typeparam name="TModel">Model Type (extended model off of TDTO)</typeparam>
public abstract class LzItemViewModel<TDTO, TModel> 
    : LzViewModel, 
    ILzItemViewModel<TModel>
    where TDTO : class, new()
    where TModel : class, TDTO, IRegisterObservables, new()
{
    // Public Properties
    public LzItemViewModel(ILoggerFactory loggerFactory, ILzSessionViewModel sessionViewModel, TDTO? dto = null, TModel? model = null, bool? isLoaded = null)
        : base(loggerFactory)   
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
    /// <summary>
    /// Delegate to create a new item in storage. DTO must have an string Id property.
    /// </summary>
    protected Func<TDTO, Task<TDTO>>? _DTOCreateAsync { get; init; }
    /// <summary>
    /// Delegate to read an item from storage. string Id argument is required.
    /// </summary>
    protected Func<string, Task<TDTO>>? _DTOReadAsync { get; init; }
    /// <summary>
    /// Delegate to update an item in storage. DTO must have an string Id property.
    /// </summary>
    protected Func<TDTO, Task<TDTO>>? _DTOUpdateAsync { get; init; }
    /// <summary>
    /// Delegate to delete an item from storage. string Id argument is required.    
    /// </summary>
    protected Func<string, Task>? _DTODeleteAsync { get; init; }
    /// <summary>
    /// Check the Id property of the current item is not null. 
    /// If a string is passed, it is checked instead.
    /// Override this method if you need a more robust Id value 
    /// check.
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public virtual Task<(bool, string)> CheckIdAsync(string? id = null)
    {
        id ??= Id!; 

        if (string.IsNullOrEmpty(id))
            return Task.FromResult((true, "Id is empty"));

        return Task.FromResult((true, string.Empty));
    }
    // Public Edit Methods - these are methods generally called from the UI for CRUD operations
    /// <summary>
    /// Create a new item in storage. 
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public virtual async Task<(bool, string)> CreateAsync()
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

            var (success, msg) = await ValidateAsync();
            if (!success)
                return (success, msg);  

            (success, msg) = await CheckAuthAsync();
            if (!success) return (success, msg);

            if (_DTOCreateAsync == null)
                throw new Exception("CreateAsync not assigned.");

            if(Id is not null) // If Id is null we assume the Service will assign it
                (success, msg) = await CheckIdAsync(Id);
            if (!success) return (success, msg);

            item = await _DTOCreateAsync(item!);

            UpdateData(item);
            State = LzItemViewModelState.Current;
            IsLoaded = true;    
            if (ParentViewModel is not null)  await ParentViewModel.ItemCreated(this); 
            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            return (false, Log(MethodBase.GetCurrentMethod()!, ex.Message));
        }
    }
    /// <summary>
    /// Update the current item in storage.
    /// </summary>
    /// <returns></returns>
    public virtual async Task<(bool, string)> UpdateAsync()
    {
        try
        {
            if (!CanUpdate)
                throw new Exception("Update not authorized");

            if (Data is null)
                throw new Exception("Data not assigned");

            var (success, msg) = await ValidateAsync(); 
            if (!success)
                return (success, msg);  

            (success, msg) = await CheckAuthAsync();
            if (!success) return (success, msg);

            if (_DTOUpdateAsync == null)
                throw new Exception("SvcUpdateAsync is not assigned.");

            (success, msg) = await CheckIdAsync(Id);
            if (!success) return (success, msg);

            UpdateData(await _DTOUpdateAsync((TDTO)Data!));

            State = LzItemViewModelState.Current;
            IsLoaded = true;
            if (ParentViewModel is not null) await ParentViewModel.ItemUpdated(Id);
            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            return (false, Log(MethodBase.GetCurrentMethod()!, ex.Message));
        }
    }
    /// <summary>
    /// Creates or Updates the current item in storage.
    /// Creates if the current State is New.
    /// Updates if the current State is Edit.
    /// </summary>
    /// <returns></returns>
    public virtual async Task<(bool, string)> SaveEditAsync()
    {
        try
        {
            var (success, msg) =
                State == LzItemViewModelState.New
                ? await CreateAsync()
                : await UpdateAsync();
            return (success, msg);
        }
        catch (Exception ex)
        {
            return (false, Log(MethodBase.GetCurrentMethod()!, ex.Message));
        }
    }
    /// <summary>
    /// Delete the current item from storage.`
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public virtual async Task<(bool, string)> DeleteAsync(string? id = null)
    {
        try
        {
            if (!CanDelete)
                throw new Exception("Delete(id) not authorized.");

            if (State != LzItemViewModelState.Current)
                throw new Exception("State != Current");

            var (success, msg) = await CheckAuthAsync();
            if (!success) return (success, msg);

            if (id == null)
                id = Id;

            (success, msg) = await CheckIdAsync(id);
            if(!success) return (success, msg); 

            if (_DTODeleteAsync == null)
                throw new Exception("SvcDelete(id) is not assigned.");
            await _DTODeleteAsync(Id!);

            State = LzItemViewModelState.Deleted;
            Data = null;
            IsDirty = false;
            if(ParentViewModel is not null) await ParentViewModel.ItemDeleted(id!);  
            return (true, String.Empty);
        }
        catch (Exception ex)
        {
            return (false, Log(MethodBase.GetCurrentMethod()!, ex.Message));
        }
    }
    /// <summary>
    /// Open the current item for editing.
    /// </summary>
    /// <param name="forceCopy"></param>
    /// <returns></returns>
    public virtual Task<(bool, string)> OpenEditAsync(bool forceCopy = false)
    {
        if (!forceCopy && State == LzItemViewModelState.Edit)
            return Task.FromResult((true, string.Empty));

        var (success, msg) = MakeDataCopy();
        if (!success)
            return Task.FromResult((success, msg));

        if (State != LzItemViewModelState.New)
            State = LzItemViewModelState.Edit;
        return Task.FromResult((true, string.Empty));
    }
    /// <summary>
    /// Cancel the current edit.
    /// </summary>
    /// <returns></returns>
    public virtual async Task<(bool, string)> CancelEditAsync()
    {
        if (State != LzItemViewModelState.Edit && State != LzItemViewModelState.New)
            return (false, Log(MethodBase.GetCurrentMethod()!, "No Active Edit"));

        State = (IsLoaded) ? LzItemViewModelState.Current : LzItemViewModelState.New;

        RestoreFromDataCopy();

        if (ParentViewModel is not null) await ParentViewModel.ItemUpdateCanceled(Id);
        return (true, "");
    }
    // Public Methods
    public virtual Task<(bool, string)> CheckAuthAsync()
    {
        return Task.FromResult((true, string.Empty));
    }
    /// <summary>
    /// ReadAsync is used to read an item from storage.
    /// If an id is passed, the item is read using the id.
    /// if an id is not passed we use the Id of the current 
    /// item (essentially a refresh operation).
    /// If an id is not passed and there is no current item, 
    /// then we throw an exception.
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public virtual async Task<(bool, string)> ReadAsync(string? id = null)
    {
        var userMsg = "Can't load " + _EntityName;
        try
        {
            if (!CanRead) return (false, "Read not authorized");
            var (success, msg) = await CheckAuthAsync();
            if (!success) return (success, msg);

            id ??= Id;

            (success, msg) = await CheckIdAsync(id);    
            if(!success) return (success, msg);

            // Perform storage operation
            if (_DTOReadAsync == null)
                throw new Exception("SvcReadIdAsync not assigned.");
            UpdateData(await _DTOReadAsync(id!));
            State = LzItemViewModelState.Current;

            if (AutoLoadChildren)
                return await ReadChildrenAsync(forceload: true);

            if(ParentViewModel is not null) await ParentViewModel.ItemRead(Id);
            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            return (false, Log(userMsg + " " + MethodBase.GetCurrentMethod()!, ex.Message));
        }
    }
    /// <summary>
    /// Override this method to perform validation on the current item.
    /// </summary>
    /// <returns></returns>
    public virtual Task<(bool,string)> ValidateAsync()
    {
        return Task.FromResult((true, string.Empty));
    }
    /// <summary>
    /// Read the children of the current item.
    /// </summary>
    /// <param name="forceload"></param>
    /// <returns></returns>
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
    protected virtual (bool, string) MakeDataCopy()
    {
        try
        {
            Data ??= new();
            _DataCopyJson = JsonConvert.SerializeObject((TDTO)Data);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
        return (true, string.Empty);    
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

    //// Model Storage API Methods
    //// Since the Data property points to the single instance of the model, these 
    //// methods are essentially no-ops. They are here to provide a consistent processing
    //// pattern for all storage APIs and allow the introduction of side effects
    //// associated with each action. For instance, you might want to perform 
    //// referential integrity checks in the Create and Update methods if you are 
    //// not using Fluent Validation or some other validation library. 
    //protected virtual async Task<TModel> ModelCreateAsync(TModel body)
    //{
    //    // Perform any referential integrity checks here.
    //    await Task.Delay(0);
    //    return body;
    //}
    //protected async Task<TModel> ModelReadAsync(string id)
    //{
    //    await Task.Delay(0);
    //    return Data!;
    //}
    //protected async Task<TModel> ModelUpdateAsync(TModel body)
    //{
    //    if(_DTO is not null)
    //        ((TDTO)Data!).DeepCloneTo(_DTO);
    //    // Perform any referential integrity checks here
    //    await Task.Delay(0);
    //    return Data!;
    //}
    //protected async Task<TModel> ModelUpdateIdAsync(string id, TModel body)
    //{
    //    // Perform any referential integrity checks here
    //    await Task.Delay(0);
    //    return Data!;
    //}
    //protected async Task ModelDeleteAsync(string id)
    //{
    //    // Perform any referential integrity checks here
    //    await Task.Delay(0);
    //}
}
