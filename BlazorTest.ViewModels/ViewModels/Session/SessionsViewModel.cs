namespace ViewModels;
public class SessionsViewModel : LzSessionsViewModelAuth<ISessionViewModel>, ISessionsViewModel
{
    public SessionsViewModel(
        ILoggerFactory loggerFactory,  
        ILzHttpClient lzHttpClient, // singleton
        ISessionViewModelFactory sessionViewModelFactory
        ) : base(loggerFactory)
    {
        _sessionViewModelFactory = sessionViewModelFactory;
        IsInitialized = true;
        lzHttpClient.Initialize(this);

    }
    private ISessionViewModelFactory _sessionViewModelFactory;

    public override ISessionViewModel CreateSessionViewModel()
    {
        return _sessionViewModelFactory.Create();
    }

}
