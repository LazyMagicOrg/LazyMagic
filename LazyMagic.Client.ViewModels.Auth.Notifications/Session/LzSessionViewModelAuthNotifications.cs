namespace LazyMagic.Client.ViewModels;

/// <summary>
/// Orchestrates the connection to services.
/// </summary>
public abstract class LzSessionViewModelAuthNotifications 
    : LzSessionViewModelAuth, 
    ILzSessionViewModelAuthNotifications
{
    /// <summary>
    /// Note that we don't pass in INotificationsSvc 
    /// in the constructor. This is because we need 
    /// to construct the NotificationsSvc 
    /// in the ILzSessionViewModelAuthNotifications
    /// constructor.
    /// </summary>
    /// <param name="authProcess"></param>
    /// <param name="clientConfig"></param>
    /// <param name="internetConnectivity"></param>
    /// <param name="messages"></param>
    public LzSessionViewModelAuthNotifications(
        ILoggerFactory loggerFactory,
        IAuthProcess authProcess,
        ILzClientConfig clientConfig, 
        IInternetConnectivitySvc internetConnectivity,
    	ILzMessages messages
        ) : base(loggerFactory, authProcess, clientConfig, internetConnectivity, messages)
	{
    }

    public ILzNotificationSvc? NotificationsSvc { get; set; }
}
