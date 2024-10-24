namespace LazyMagic.Client.ViewModels
{
    public interface ILzItemsViewModel<TVM, TDTO, TModel> : ILzParentViewModel
        where TVM : class, ILzItemViewModel<TModel>
        where TDTO : class, new()
        where TModel : class, IRegisterObservables, TDTO, new()
        
    {
        // Public Properties
        public string? Id { get; set; }
        public Dictionary<string, TVM> ViewModels { get; set; }
        public bool SourceIsList { get; set; }
        public TVM? CurrentViewModel { get; set; }
        public TVM? LastViewModel { get; set; }
        public event NotifyCollectionChangedEventHandler? CollectionChanged;
        public bool IsChanged { get; set; }
        public bool AutoReadChildren { get; set; }
        public bool IsLoaded { get; set; }
        public bool IsLoading { get; set; }
        public long LastLoadTick { get; set; }
        public bool CanList { get; set; }
        public bool CanAdd { get; set; }
        public long UpdateCount { get; set; }
        public IDictionary<string, TModel>? Models { get; set; }
        public IDictionary<string, TDTO>? DTOs { get; set; }

        // Public Methods
        public void Clear();
        public Task<(bool, string)> ReadAsync(bool forceload = false);
        public Task<(bool, string)> ReadAsync(string parentId, bool forceload = false);
        public Task<(bool, string)> CancelCurrentViewModelEditAsync();
        public Task<(bool, string)> SaveCurrentViewModelAsync(string? id);
        public void CheckAuth();
        public (TVM viewmodel, string id) NewViewModel(TDTO dto);
        public (TVM viewmodel, string id) NewViewModel(string key, TModel model);
        public (TVM viewmodel, string id) NewViewModel(string key, TDTO dto);
    }
}