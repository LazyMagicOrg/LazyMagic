namespace BlazorTest.ViewModels;
public class SessionsViewModel : LzSessionsViewModelAuth<ISessionViewModel>, ISessionsViewModel
{
    public SessionsViewModel(
        ILoggerFactory loggerFactory,   
        ISessionViewModelFactory sessionViewModelFactory
        ) : base(loggerFactory)
    {
        _sessionViewModelFactory = sessionViewModelFactory;
        IsInitialized = true;

    }
    private ISessionViewModelFactory _sessionViewModelFactory;

    public override ISessionViewModel CreateSessionViewModel()
    {
        return _sessionViewModelFactory.Create();
    }

}
