namespace LazyMagic.Client.ViewModels;

/// <summary>
/// Orchestrates the connection to services.
/// </summary>
public abstract class LzSessionViewModelAuthNotifications : LzSessionViewModelAuth, ILzSessionViewModelAuthNotifications
{
    public LzSessionViewModelAuthNotifications(
        IAuthProcess authProcess,
        ILzClientConfig clientConfig, 
        IInternetConnectivitySvc internetConnectivity,
    	ILzMessages messages
		) : base(authProcess, clientConfig, internetConnectivity, messages)
	{
    }

    public ILzNotificationSvc? NotificationsSvc { get; set; }
}
