namespace LazyMagic.Client.ViewModels;

public interface ILzItemViewModel<TModel> 
    where TModel : class, new()
{
    // Public Properties
    public bool AutoLoadChildren { get; set; }
    public string? Id { get; }
    public long UpdatedAt { get; }
    public TModel? Data { get; set; }
    public LzItemViewModelState State { get; set; }
    public bool CanCreate { get; set; }
    public bool CanRead { get; set; }
    public bool CanUpdate { get; set; }
    public bool CanDelete { get; set; }
    public bool IsLoaded { get; set; }
    public long UpdateCount { get; set; }
    public bool IsNew { get; }
    public bool IsEdit { get; }
    public bool IsCurrent { get; }
    public bool IsDeleted { get; }
    public bool IsDirty { get; set; }
    public ILzParentViewModel? ParentViewModel { get; set; }
    public void CheckAuth();   

    // Public Methods
    public Task<(bool, string)> CreateAsync(string? id);
    public Task<(bool, string)> ReadAsync(string id);
    public Task<(bool, string)> ReadAsync();
    public Task<(bool, string)> UpdateAsync(string? id);
    public Task<(bool, string)> SaveEditAsync(string? id);
    public Task<(bool, string)> DeleteAsync(string id);
    public void OpenEdit(bool forceCopy = false);
    public Task OpenEditAsync(bool forceCopy = false);
    public Task<(bool, string)> CancelEditAsync();
    public bool Validate();
    public Task<(bool, string)> ReadChildrenAsync(bool forceload);


}
