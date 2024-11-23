namespace LazyMagic.Client.ViewModels;
/// <inheritdoc/>
public interface ILzItemsViewModelAuthNotifications<TVM, TDTO, TModel> :
        ILzItemsViewModelAuth<TVM, TDTO, TModel>
        where TVM : class, ILzItemViewModelAuthNotifications<TModel>
        where TDTO : class, new()
        where TModel : class, IRegisterObservables, TDTO, new()
{
    public ILzNotificationSvc? NotificationsSvc { get; init; }
    public long NotificationLastTick { get; set; }
    public Task UpdateFromNotificationAsync(LzNotification notificaiton);
}
