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
    public interface ILzItemsViewModel<TId, TVM, TDTO, TModel> : 
        ILzParentViewModel<TId>,
        IItemId<TId>,
        INotifyCollectionChanged 
        where TVM : class, ILzItemViewModel<TId, TModel>
        where TDTO : class, new()
        where TModel : class, IRegisterObservables, TDTO, new()
        
    {
        // Public Properties
        /// <summary>
        /// An optional Id for the list.
        /// </summary>
        public TId? Id { get; set; }
        /// <summary>
        /// The Dictionary of ItemViewModels managed by this class.
        /// </summary>
        public Dictionary<TId, TVM> ViewModels { get; set; }
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
        /// <summary>
        /// When true, a read makes the collection what was read, in the order it was returned: a view model whose
        /// item the read did not return is removed. False by default - a read then adds and replaces and never
        /// removes or moves.
        /// </summary>
        public bool ReplaceOnRead { get; set; }
        /// <summary>
        /// When true, the collection is read again (<see cref="ReadAgainAsync"/>) after an item is created, updated
        /// or deleted. False by default. For a collection where a write to one item changes others in storage.
        /// </summary>
        public bool ReadAfterChange { get; set; }
        /// <summary>
        /// The exception that made the last ReadAsync return false, or null when it succeeded. ReadAsync returns a
        /// message built for a log; this is the exception itself.
        /// </summary>
        public Exception? LastException { get; }

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
        public Task<(bool, string)> ReadAsync(TId parentId, bool forceload = false);
        /// <summary>
        /// Read the list again as it was last read: with the same parent Id, or with none. Before any read, with Id.
        /// </summary>
        public Task<(bool, string)> ReadAgainAsync();
        /// <summary>
        /// A quiet read, for a poll: no IsLoading, and the collection left alone when the read's signature (by default
        /// the whole list) is the last applied one, or when another list was applied while it was out (it is then older
        /// than what is shown).
        /// </summary>
        public Task<(bool, string)> RefreshAsync();
        /// <summary>When the collection was last read or refreshed successfully; not reactive.</summary>
        public long LastCheckedTick { get; }
        /// <summary>
        /// Start a new item: a view model for the dto made by NewViewModel, in the New state and not loaded, as the
        /// EditViewModel. It is not in the collection until it has been created in storage.
        /// </summary>
        public TVM NewItem(TDTO dto);
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
        public (TVM viewmodel, TId id) NewViewModel(TDTO dto);
        /// <summary>
        /// Implement this to create a new ViewModel for the list
        /// using a ItemModel
        /// </summary>
        /// <param name="key"></param>
        /// <param name="model"></param>
        /// <returns></returns>
        public (TVM viewmodel, TId id) NewViewModel(string key, TModel model);

    }
}