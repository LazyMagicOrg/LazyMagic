namespace LazyMagic.Client.ViewModels;

/// <summary>
/// Orchestrates the connection to services.
/// </summary>
public abstract partial class LzSessionViewModel : LzViewModel, ILzSessionViewModel
{
    public LzSessionViewModel(
        ILoggerFactory loggerFactory,
        IConnectivityService connectivityService,
    	ILzMessages messages
		) : base(loggerFactory) 
    {
        ConnectivityService = connectivityService ?? throw new ArgumentNullException(nameof(connectivityService));
        Messages = messages ?? throw new ArgumentNullException(nameof(messages));
        // Maintain a local instance of the MessageSetSelector so we can react to changes in that value 
        // to update the current MessageSetSelector in Messages.
        MessageSetSelector = new LzMessageSetSelector(Messages.MessageSet.Culture, Messages.MessageSet.Units);

        // Wire the IsOnline OAPH. Was ToPropertyEx (Fody); SourceGenerators
        // requires assigning the generated _isOnlineHelper field directly.
        _isOnlineHelper = this.WhenAnyValue(x => x.ConnectivityService.IsOnline)
            .ToProperty(this, nameof(IsOnline));

        this.WhenAnyValue(x => x.MessageSetSelector)
            .Skip(1) // Skip the initial value assignment to avoid load timing issues.
            .DistinctUntilChanged()
            .Subscribe(async (messageSetSelector) =>
            { 
                // Note: The LzMessage instance MessageSetSelector is not the same instance as the one in 
                // LzMessages. When it changes, we make a call to the LzMessages instance to update
                // the current message set.
    		    await Messages.SetMessageSetAsync(messageSetSelector.Culture, messageSetSelector.Units);
			});
    }
    public IConnectivityService ConnectivityService { get; set; }  
    public string SessionId { get; set; } = Guid.NewGuid().ToString();
    public ILzMessages Messages { get; set; }
    public string SessionName { get; set; } = "Session";

    [ObservableAsProperty] public partial bool IsOnline { get; }
    [Reactive] public partial bool IsLoading { get; set; }
    [Reactive] public partial bool IsLoaded { get; set; }
    [Reactive] public partial LzMessageSetSelector MessageSetSelector { get; set; }
    public Task<bool> CheckInternetConnectivityAsync()
        => ConnectivityService.CheckInternetConnectivityAsync();

    public virtual async Task LoadAsync()
        => await Task.Delay(0);
    public virtual async Task UnloadAsync()
        => await Task.Delay(0);

    public virtual async Task InitAsync()
    {
        await Task.Delay(0);
    }
}
