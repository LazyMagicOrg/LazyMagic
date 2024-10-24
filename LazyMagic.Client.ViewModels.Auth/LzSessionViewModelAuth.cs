namespace LazyMagic.Client.ViewModels;

/// <summary>
/// Orchastrates the connection to services.
/// Decouples IAuthProcess from ILzClientConfig.
/// </summary>
public abstract class LzSessionViewModelAuth : LzSessionViewModel, ILzSessionViewModelAuth
{
    public LzSessionViewModelAuth(
        IAuthProcess authProcess, 
        ILzClientConfig clientConfig, 
        IInternetConnectivitySvc internetConnectivity,
        ILzMessages messages
        ) : base(internetConnectivity, messages)    
    {
        AuthProcess = authProcess ?? throw new ArgumentNullException(nameof(authProcess));    

        // ReactiveUI statements. I've added some comments for folks who have 
        // never used ReactiveUI
        // When AuthProcess.IsSignedIn changes, update this classes observable IsSignedIn property
        this.WhenAnyValue(x => x.AuthProcess.IsSignedIn)
            .Select(x => { return x; })
            .ToPropertyEx(this, x => x.IsSignedIn);

        // When AuthProcess.IsBusy changes, update this classes observable IsBusy property
        this.WhenAnyValue(x => x.AuthProcess.IsBusy)
            .ToProperty(this, x => x.IsBusy);

        // When AuthProcess.HasChallenge changes, update this classes observable HasChallenge property
        this.WhenAnyValue(x => x.AuthProcess.HasChallenge)
            .ToPropertyEx(this, x => x.HasChallenge);

        // When the login changes, update the IsAdmin property. Note the virtual method IsAdminCheck().
        // Implement your application specific logic in the IsAdminCheck() method.  
        this.WhenAnyValue(x => x.AuthProcess.Login)
            .SelectMany(async x => await IsAdminCheck()) // SelectMany allows the use of async in the reactive chain
            .ToPropertyEx(this, x => x.IsAdmin);

        this.WhenAnyValue(x => x.IsSignedIn)
            .DistinctUntilChanged()
            .Throttle(TimeSpan.FromMilliseconds(200))
            .Subscribe(async (isSignedIn) =>
            {
                if (isSignedIn)
                    await OnSignedInAsync();
                else
                    await OnSignedOutAsync();
            });
    }
    public IAuthProcess AuthProcess { get; set; }
    [Reactive] public bool IsBusy { get; set; }
    [ObservableAsProperty] public bool IsSignedIn { get; }
    [ObservableAsProperty] public bool HasChallenge { get; }
    [ObservableAsProperty] public bool IsAdmin { get; }
    public virtual async Task OnSignedOutAsync()
    {
        await UnloadAsync();
    }
    public virtual async Task OnSignedInAsync()
    {
        try
        {
            IsBusy = true;
            IsLoading = true;
            await LoadAsync();
            IsLoaded = true;
        }
        catch
        {
            return;
        }
        finally
        {
            IsBusy = false;
            IsLoading = false;
        }
    }
    // The ObservableAsProperty annotation is defined in ReactiveUI.Fody
    public async virtual Task<bool> IsAdminCheck()
    {
        await Task.Delay(0);
        // This is not recommended. Override this method in your app 
        // to do an appropriate check.
        return AuthProcess.Login == "Administrator";
    }
}
