
namespace LazyMagic.Client.ViewModels;

public interface ILzNotificationSvc
{
    /// <summary>
    /// ViewModels use ReactiveUI 's WhenAnyValue to subscribe to changes in this property.
    /// </summary>
    LzNotification? Notification { get; set; }
    ObservableCollection<string> Topics { get; }
    bool IsActive { get; } 
    Task ConnectAsync();
    Task DisconnectAsync();
    Task SendAsync(string message);
    Task<List<LzNotification>> ReadNotificationsAsync(string sessionId, long lastDateTimeTick);
    Task<(bool success, string msg)> SubscribeAsync(List<String> topicIds);
    Task<(bool success, string msg)> UnsubscribeAsync(List<String> topicIds);
    Task<(bool success, string msg)> UnsubscribeAllAsync();
}