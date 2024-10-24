namespace BlazorizeTest.ViewModels;
public class SessionsViewModel : LzSessionsViewModelAuth<ISessionViewModel>, ISessionsViewModel
{
    public SessionsViewModel(
        ISessionViewModelFactory sessionViewModelFactory
        )
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
