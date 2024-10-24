namespace LazyMagic.Client.ViewModels;

public interface ILzItemViewModelData<TModel>
{
    public TModel? Data { get; set; }
    public TModel? NotificationData { get; set; }

    public void CheckAuth();
}
