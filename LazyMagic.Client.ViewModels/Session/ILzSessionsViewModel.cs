using System.ComponentModel;

namespace LazyMagic.Client.ViewModels;
public interface ILzSessionsViewModel<T>: INotifyPropertyChanged
    where T : ILzSessionViewModel
{
    IDictionary<string, string> SessionLogins { get; }
    T? SessionViewModel { get; set; }
    bool IsInitialized { get; }
    Task<bool> CreateSessionAsync();
    Task DeleteAsync(string sessionId);
    Task SetAsync(string sessionId);

}