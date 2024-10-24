namespace LazyMagic.Client.ViewModels;

public abstract class LzSessionsViewModelAuth<T> : LzSessionsViewModel<T>, ILzSessionsViewModelAuth<T>
    where T : ILzSessionViewModelAuth
{
    public LzSessionsViewModelAuth()   
    {
    }
    public override T? SessionViewModel { get; set; }
    // public IAuthProcess? AuthProcess { get; set; }
    //[ObservableAsProperty] public bool IsSignedIn { get; }
    //[ObservableAsProperty] public bool IsAdmin { get; }
    public override async Task<bool> CreateSessionAsync()
    {
        var sessionCreated = await base.CreateSessionAsync();
        if(sessionCreated)
        {
            //AuthProcess = SessionViewModel!.AuthProcess;
            //this.WhenAnyValue(x => x.AuthProcess!.IsSignedIn)
            //    .ToPropertyEx(this, x => x.IsSignedIn)
            //    .DisposeWith(sessionDisposables);

            //this.WhenAnyValue(x => x.SessionViewModel!.IsAdmin)
            //    .ToPropertyEx(this, x => x.IsAdmin)
            //    .DisposeWith(sessionDisposables);
        }
        return sessionCreated;
    }
}
