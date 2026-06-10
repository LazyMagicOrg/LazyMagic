namespace LazyMagic.Client.ViewModels;

public abstract partial class LzSessionsViewModel<T> : LzViewModel, ILzSessionsViewModel<T>
    where T : ILzSessionViewModel
{
    [Reactive] public virtual partial T? SessionViewModel { get; set; }
    private Dictionary<string, T> _sessions = new();
    [Reactive] public partial bool IsInitialized { get; protected set; }
    [ObservableAsProperty(UseProtected = true)] public partial bool IsOnline { get; }
    protected readonly CompositeDisposable sessionDisposables = new();
    //public virtual async Task InitAsync(IOSAccess osAccess, ILzClientConfig clientConfig, IConnectivityService internetConnectivitySvc)

    public LzSessionsViewModel(ILoggerFactory loggerFactory) : base(loggerFactory)
    {
        // Default OAPH initialization — derived classes are expected to
        // overwrite _isOnlineHelper with a real connectivity stream.
        // The base initialization keeps the non-null contract intact.
        _isOnlineHelper = Observable.Return(false).ToProperty(this, nameof(IsOnline));
    }

    public virtual async Task<bool> CreateSessionAsync()
    {
        if (SessionViewModel != null) return false;
        T sessionViewModel = CreateSessionViewModel();
        await sessionViewModel.InitAsync();
        SessionViewModel = sessionViewModel;
        return true;
    }
    public virtual T CreateSessionViewModel() { throw new NotImplementedException(); }
    public virtual async Task DeleteAsync(string sessionId)
    {
        await Task.Delay(0);
        sessionDisposables.Dispose();
        if (!_sessions.ContainsKey(sessionId))
            throw new Exception("Bad session id");
        _sessions.Remove(sessionId);
    }
    public virtual async Task SetAsync(string sessionId)
    {
        await Task.Delay(0);
        if (_sessions.ContainsKey(sessionId))
            SessionViewModel = _sessions[sessionId];
        else throw new Exception("Bad session id");
    }
    public IDictionary<string, string> SessionLogins => _sessions.ToDictionary(x => x.Key, x => x.Value.SessionName);
}
