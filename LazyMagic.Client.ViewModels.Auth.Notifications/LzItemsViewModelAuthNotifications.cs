namespace LazyMagic.Client.ViewModels;

public abstract class LzItemsViewModelAuthNotifications<TVM, TDTO, TModel> : 
    LzItemsViewModel<TVM, TDTO, TModel>, 
    INotifyCollectionChanged, 
    ILzItemsViewModelAuthNotifications<TVM, TDTO, TModel> where TDTO : class, new()
    where TModel : class, TDTO, IRegisterObservables, new()
    where TVM : class, ILzItemViewModel<TModel>
{
    public LzItemsViewModelAuthNotifications(
        ILzSessionViewModel sessionViewModel,
        IDictionary<string, TDTO>? dtos = null,
        IDictionary<string,TModel>? models = null
        ) 
        : base(sessionViewModel, dtos, models)
    {
        this.WhenAnyValue(x => x.NotificationsSvc!.Notification!)
            .WhereNotNull()
            .Where(x => x.PayloadParentId == ParentId)
            .Subscribe(async (x) => await UpdateFromNotificationAsync(x));
    }
    public ILzNotificationSvc? NotificationsSvc { get; init; }
    public string ParentId { get; set; } = string.Empty;
    [Reactive] public long NotificationLastTick { get; set; }
    public virtual async Task UpdateFromNotificationAsync(LzNotification notification)
    {
        await Task.Delay(0);
        return;
    }
}
