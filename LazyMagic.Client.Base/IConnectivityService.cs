namespace LazyMagic.Client.Base;

public interface IConnectivityService : IAsyncDisposable, INotifyPropertyChanged
{
    bool IsOnline { get; }
    bool IsPollingEnabled { get; }
    Task<bool> CheckInternetConnectivityAsync();
    Task<bool> ShouldMakeNetworkRequestAsync();
    Task OnConnectivityChanged(bool isOnline);
    Task SetPollingEnabledAsync(bool enabled);
    void Dispose();
}