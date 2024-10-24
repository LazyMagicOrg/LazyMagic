using System.ComponentModel;

namespace LazyMagic.Client.ViewModels;

public interface ILzSessionViewModelAuthNotifications : ILzSessionViewModelAuth, INotifyPropertyChanged
{
    ILzNotificationSvc? NotificationsSvc { get; set; }
}