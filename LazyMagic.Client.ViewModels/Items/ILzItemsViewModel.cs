namespace LazyMagic.Client.ViewModels
{
    /// <summary>
    /// This class manages a list of ViewModels
    /// TVM is the ViewModel Class in the list
    /// TDTO is the data transfer object that the TVM model uses
    /// TModel is the data model derived from the TDTO that the TVM presents to views.
    /// Remember: 
    /// Assign SvcReadChildren or SvcReadChildrenId, and implement NewViewModel()
    /// Optionally Implement ItemCreated(), ItemDeleted(), ItemUpdated(), ItemUpdateCanceled(), ItemRead()
    /// </summary>
    /// <typeparam name="TVM">ItemViewModel type</typeparam>
    /// <typeparam name="TDTO">Item Data Transfer Object type</typeparam>
    /// <typeparam name="TModel">ItemModle type</typeparam>
    public interface ILzItemsViewModel<TVM, TDTO, TModel> : 
        ILzParentViewModel,
        INotifyCollectionChanged 
        where TVM : class, ILzItemViewModel<TModel>
        where TDTO : class, new()
        where TModel : class, IRegisterObservables, TDTO, new()
        
    {
        // Public Properties
        /// <summary>
        /// An optional Id for the list.
        /// </summary>
        public string? Id { get; set; }
        /// <summary>
        /// The Dictionary of ItemViewModels managed by this class.
        /// </summary>
        public Dictionary<string, TVM> ViewModels { get; set; }
        /// <summary>
        /// The current ItemViewModel.
        /// </summary>
        public TVM? CurrentViewModel { get; set; }
        /// <summary>
        /// EditViewModel is meant to be set prior to navigating to 
        /// a view that is used to edit an ItemViewModel. It indicates
        /// to that view whether the view is editing an existing item,
        /// or should create a new item.
        /// There are three common patterns:
        /// 1. When moving to a view that edits the CurrentViewModel, 
        /// set the EditViewModel to the CurrentViewModel.
        /// 2. When adding an item. If create the item item 
        /// and add it to the ViewModels, then set the EditViewModel
        /// to the new item.
        /// 3. If the UI view will call add item, then set the 
        /// EditViewModel to null. 
        /// Note that EditViewModel is set to null in these
        /// item edit callbacks:
        /// - ItemCreated
        /// - ItemDeleted
        /// - ItemUpdated
        /// - ItemUpdateCancelled
        /// - ItemRead
        /// But it is good practice to explicity set it to null 
        /// in your UI code, prior to navigating to the item edit view.
        /// </summary>
        public TVM? EditViewModel { get; set; }
        public TVM? LastViewModel { get; set; }
        /// <summary>
        /// Notify
        /// </summary>
        public event NotifyCollectionChangedEventHandler? CollectionChanged;
        /// <summary>
        /// When assigned to, this increments the changeCount
        /// </summary>
        public bool IsChanged { get; set; }
        /// <summary>
        /// Set to true to automatically read children of each item.
        /// </summary>
        public bool AutoReadChildren { get; set; }
        /// <summary>
        /// Reactive observable that is true when the list is loaded.
        /// </summary>
        public bool IsLoaded { get; set; }
        /// <summary>
        /// Reactive observable that is true when the list is loading.
        /// </summary>
        public bool IsLoading { get; set; }
        /// <summary>
        /// Reactive observable that contains the LastLoadTick
        /// </summary>
        public long LastLoadTick { get; set; }

        // Public Methods
        /// <summary>
        /// Read the storage for the list
        /// </summary>
        public void Clear();
        public Task<(bool, string)> ReadAsync(bool forceload = false);
        /// <summary>
        /// Read the storage for the list using the specified parent Id
        /// </summary>
        /// <param name="parentId"></param>
        /// <param name="forceload"></param>
        /// <returns></returns>
        public Task<(bool, string)> ReadAsync(string parentId, bool forceload = false);
        /// <summary>
        /// Check if the user is authorized
        /// </summary>
        public void CheckAuth();
        /// <summary>
        /// Implement this to create a new ViewModel for the list
        ///  using an item DTO.
        /// </summary>
        /// <param name="dto"></param>
        /// <returns></returns>
        public (TVM viewmodel, string id) NewViewModel(TDTO dto);
        /// <summary>
        /// Implement this to create a new ViewModel for the list
        /// using a ItemModel
        /// </summary>
        /// <param name="key"></param>
        /// <param name="model"></param>
        /// <returns></returns>
        public (TVM viewmodel, string id) NewViewModel(string key, TModel model);
        /// <summary>
        /// Implement this to create a new ViewModel for the list 
        /// using a key and item DTO.
        /// Depreciated. Use NewViewModel(TDTO dto) instead 
        /// of this method.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="dto"></param>
        /// <returns></returns>
        [Obsolete("Use NewViewModel(TDTO dto) instead of this method.")]
        public (TVM viewmodel, string id) NewViewModel(string key, TDTO dto);
    }
}