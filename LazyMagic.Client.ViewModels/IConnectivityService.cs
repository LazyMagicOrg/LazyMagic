namespace LazyMagic.Client.ViewModels;

public interface IConnectivityService : IAsyncDisposable, INotifyPropertyChanged
{
    bool IsOnline { get; } 
    Task<bool> CheckInternetConnectivityAsync();
    Task<bool> ShouldMakeNetworkRequestAsync();
    Task OnConnectivityChanged(bool isOnline);
    void Dispose();
}