using LazyMagic.Client.FactoryGenerator; // do not put in global using. Causes runtime error.
namespace ViewModels;

[Factory]
public class SessionViewModel : LzSessionViewModel, ISessionViewModel
{
    public SessionViewModel(
        [FactoryInject] ILoggerFactory loggerFactory, // singleton  
        [FactoryInject] ILzClientConfig clientConfig, // singleton
        [FactoryInject] IConnectivityService connectivityService, // singleton
        [FactoryInject] ILzMessages messages // singleton
        )
        : base(loggerFactory, connectivityService, messages)
    {

    }
}
