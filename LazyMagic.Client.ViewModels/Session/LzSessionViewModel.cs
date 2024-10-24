namespace LazyMagic.Client.ViewModels;

/// <summary>
/// Orchestrates the connection to services.
/// </summary>
public abstract class LzSessionViewModel : LzViewModel, ILzSessionViewModel, INotifyPropertyChanged
{
    public LzSessionViewModel(
        IInternetConnectivitySvc internetConnectivity,
    	ILzMessages messages
		)
	{
        InternetConnectivity = internetConnectivity ?? throw new ArgumentNullException(nameof(internetConnectivity));
        Messages = messages ?? throw new ArgumentNullException(nameof(messages));
        // Maintain a local instance of the MessageSetSelector so we can react to changes in that value 
        // to update the current MessageSetSelector in Messages.
        MessageSetSelector = new LzMessageSetSelector(Messages.MessageSet.Culture, Messages.MessageSet.Units);

        this.WhenAnyValue(x => x.InternetConnectivity.IsOnline)
            .ToPropertyEx(this, x => x.IsOnline);

        this.WhenAnyValue(x => x.MessageSetSelector)
            .DistinctUntilChanged()
            .Subscribe(async (messageSetSelector) =>
            { 
                // Note: The LzMessage instance MessageSetSelector is not the same instance as the one in 
                // LzMessages. When it changes, we make a call to the LzMessages instance to update
                // the current message set.
    		    await Messages.SetMessageSetAsync(messageSetSelector.Culture, messageSetSelector.Units);
			});
    }
    public IInternetConnectivitySvc InternetConnectivity { get; set; }  
    public string SessionId { get; set; } = Guid.NewGuid().ToString();
    public ILzMessages Messages { get; set; }
    public string SessionName { get; set; } = "Session";

    // The ObservableAsProperty annotation is defined in ReactiveUI.Fody
    [ObservableAsProperty] public bool IsOnline { get; }
    [Reactive] public bool IsLoading { get; set; }
    [Reactive] public bool IsLoaded { get; set; }
    [Reactive] public LzMessageSetSelector MessageSetSelector { get; set; }
    public Task<bool> CheckInternetConnectivityAsync()
        => InternetConnectivity.CheckInternetConnectivityAsync();
    public virtual async Task LoadAsync()
        => await Task.Delay(0); 
    public virtual async Task UnloadAsync()
        => await Task.Delay(0);

    public virtual async Task InitAsync()
    {
        await Task.Delay(0);
    }
}
