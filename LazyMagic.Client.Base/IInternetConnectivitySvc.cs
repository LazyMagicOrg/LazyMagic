namespace LazyMagic.Client.Base;
public interface IInternetConnectivitySvc : IDisposable, INotifyPropertyChanged
{
    bool IsOnline { get; }
    Task<bool> CheckInternetConnectivityAsync();
    Task<bool> ShouldMakeNetworkRequestAsync();

}
