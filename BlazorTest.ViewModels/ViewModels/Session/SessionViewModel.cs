namespace ViewModels;

[Factory]
public class SessionViewModel : LzSessionViewModelAuth, ISessionViewModel, ILzTransient
{
    public SessionViewModel(
        [FactoryInject] ILoggerFactory loggerFactory, // singleton  
        [FactoryInject] IAuthProcess authProcess, // transient
        [FactoryInject] ILzClientConfig clientConfig, // singleton
        [FactoryInject] IConnectivityService connectivityService, // singleton
        [FactoryInject] ILzMessages messages // singleton
        )
        : base(loggerFactory, authProcess, clientConfig, connectivityService, messages)
    {
        authProcess.SetAuthenticator(clientConfig.AuthConfigs["TenantAuth"]);

    }
}
