namespace LazyMagic.Client.ViewModels;

public interface ILzItemsViewModelAuthNotifications<TVM, TDTO, TModel> : ILzParentViewModel
        where TVM : class, ILzItemViewModel<TModel>
        where TDTO : class, new()
        where TModel : class, IRegisterObservables, TDTO, new()
{
    public ILzNotificationSvc? NotificationsSvc { get; init; }
    public long NotificationLastTick { get; set; }
    public Task UpdateFromNotificationAsync(LzNotification notificaiton);
}
