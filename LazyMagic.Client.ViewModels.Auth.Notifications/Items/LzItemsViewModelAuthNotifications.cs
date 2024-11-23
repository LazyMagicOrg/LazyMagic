namespace LazyMagic.Client.ViewModels;
/// <inheritdoc/>
public abstract class LzItemsViewModelAuthNotifications<TVM, TDTO, TModel> : 
    LzItemsViewModelAuth<TVM, TDTO, TModel>, 
    ILzItemsViewModelAuthNotifications<TVM, TDTO, TModel> where TDTO : class, new()
    where TModel : class, TDTO, IRegisterObservables, new()
    where TVM : class, ILzItemViewModelAuthNotifications<TModel>
{
    public LzItemsViewModelAuthNotifications(
        ILoggerFactory loggerFactory,
        ILzSessionViewModel sessionViewModel
        ) 
        : base(loggerFactory, sessionViewModel)
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
