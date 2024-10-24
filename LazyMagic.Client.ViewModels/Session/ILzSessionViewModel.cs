using System.ComponentModel;

namespace LazyMagic.Client.ViewModels;

public interface ILzSessionViewModel : INotifyPropertyChanged
{
    IInternetConnectivitySvc InternetConnectivity { get; set; }
    string SessionName { get; set; }
    string SessionId { get; set; }
    bool IsOnline { get; }
    bool IsLoaded { get; set; }
    bool IsLoading { get; set; }
    LzMessageSetSelector MessageSetSelector { get; set; }
    Task InitAsync();
    Task<bool> CheckInternetConnectivityAsync();
    Task LoadAsync();
    Task UnloadAsync();


}
