using System.ComponentModel;

namespace LazyMagic.Client.ViewModels;
/// <inheritdoc/>
public interface ILzSessionViewModelAuthNotifications : ILzSessionViewModelAuth
{
    ILzNotificationSvc? NotificationsSvc { get; set; }
}