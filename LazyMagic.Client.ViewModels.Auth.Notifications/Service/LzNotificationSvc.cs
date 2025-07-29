namespace LazyMagic.Client.ViewModels;
/// <summary>
/// Derive this class and 
/// - Implement ReadNotifications() // even if you are using WebSockets
/// 
/// Notifications using WebSocket. 
///     
/// ClientWebSocket limitations:
/// When used in a browser, .NET ClientWebSocket does not allow us to 
/// attach an authorization header when communicating to the WebSocket 
/// Service. For this reason, we do unauthenticated websocket connections.
/// Note that we subscribe using the REST API and subscription is usually subject to 
/// authentication.
/// On the server side, we only send messages to websocket connections, 
/// related to current subscriptions. 
/// So, in a nutshell, websocket connections are not authenticated
/// but subscription channels usually are. 
/// 
/// To mitigate DOS attacks, the service side should delete any websocket connections 
/// that are not subscribed to any topics within a reasonable time period, like
/// 5 seconds.
/// 
/// Also, we are using wss:// so the websocket connection transport is secure.
/// 
/// Client side Usage Notes
/// After signing in, use the ClientSDK.SubscribeAsync(Subscription subscription) to subscribe to 
/// one or more topics. Do at least one subscription with a short time to avoid having the 
/// service side delete the websocket connection. 
/// 
/// Flow:
/// - Singleton injection, should happen before authentication
/// 
/// </summary>
public abstract class LzNotificationSvc : LzViewModel, ILzNotificationSvc, IDisposable
{
    public LzNotificationSvc(
        ILoggerFactory loggerFactory,
        ILzClientConfig clientConfig,
        ILzHost lzHost,
        IAuthProcess authProces,
        IConnectivityService internetConnectivity,
        string? sessionId = null) : base(loggerFactory) 
    {
        this.clientConfig = clientConfig;
        this.lzHost = lzHost;
        AuthProcess = authProces;
        InternetConnectivitySvc = internetConnectivity;
        this.sessionId = sessionId ?? "";

        this.WhenAnyValue(x => x.InternetConnectivitySvc.IsOnline, x => x.AuthProcess.IsSignedIn, (x, y) => x && y )
            .Throttle(TimeSpan.FromMilliseconds(100))
            .DistinctUntilChanged()
            .Subscribe(async x =>
            {
                if(authProces.IsSignedIn && internetConnectivity.IsOnline)
                    await EnsureConnectedAsync();
            });
    }

    protected ILzClientConfig clientConfig;
    public IAuthProcess AuthProcess { get; init; }
    public IConnectivityService InternetConnectivitySvc { get; init; }
    protected string? wsBaseUri; 
    protected Timer? timer;
    protected ClientWebSocket? ws;
    protected string connectionId = string.Empty;
    protected ILzHost lzHost;
    protected string sessionId;    
    public bool Debug { get; set; } = false;

    protected long lastDateTimeTicks = 0;
    public ObservableCollection<string> Topics { get; set; } = new();
    [ObservableAsProperty] public bool IsActive { get; }
    // This class implements INotifyPropertyChanged so events are produced 
    // when Notification is assigned. 
    private LzNotification? _notification;
    public LzNotification? Notification 
    {
        get { return _notification; }
        set { this.RaiseAndSetIfChanged(ref _notification, value); }
    }
    protected bool isBusy = false;

    /// <summary>
    /// You must implement this method in the derived class.
    /// Typically, you will have extended your REST API to 
    /// have endpoints supporting Notifications using the 
    /// Service solution's NotificationsSvc.yaml and have
    /// appropriate Notification methods in the ClientSDK.
    /// Note: The method implementation should have the 
    /// 'async' modifier as it is expected to call the system 
    /// service using the system ClientSDK.
    /// </summary>
    /// <param name="lastDateTimeTick"></param>
    /// <returns></returns>
    /// Example Implementation:
    public abstract Task<List<LzNotification>> ReadNotificationsAsync(string connectionId, long lastDateTimeTick);
    public abstract Task<(bool success, string msg)> SubscribeAsync(List<string> topicIds);
    public abstract Task<(bool success, string msg)> UnsubscribeAsync(List<string> topicIds);
    public abstract Task<(bool success, string msg)> UnsubscribeAllAsync();
    
    protected string createdAtFieldName = "CreatedAt";

    public async Task ConnectAsync()
    {
        if(Debug)
            _logger.LogDebug("NotificationSvc.ConnectAsync()");
        await EnsureConnectedAsync();
    }

    public virtual async Task EnsureConnectedAsync()
    {

        //Todo: Clean up the handling of the websocket lifecycle. 
        ws ??= new ClientWebSocket();

        if(Debug)
            _logger.LogDebug($"EnsureConnectedAsync. WebSocketState={ws.State}");

        if (ws.State == WebSocketState.Open || ws.State == WebSocketState.Connecting)
            return;

        if (ws.State == WebSocketState.Closed || ws.State == WebSocketState.Aborted)
            ws = new ClientWebSocket();
           
        if(ws.State != WebSocketState.None)
            return;

        var uri = new Uri(lzHost.WsUrl);   
        try
        {
            if (Debug)
                _logger.LogDebug("Calling ws.ConnectAsync");
            await ws.ConnectAsync(uri, CancellationToken.None);
            await ListenForMessages();
        } catch (Exception ex)
        {
            _logger.LogDebug(ex.Message.ToString());   
        }
    }

    protected async Task ListenForMessages()
    {
        if (Debug)
            _logger.LogDebug("Listening for web socket messages");
        var buffer = new byte[1024];
        if (ws is null)
            return;
        while (ws.State == WebSocketState.Open)
        {
            var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            if (Debug)
                _logger.LogDebug($"Received message. type:{result.MessageType}");
            switch(result.MessageType)
            {
                case WebSocketMessageType.Close:
                    if (Debug)
                        _logger.LogDebug("WebSocket Close");
                    break;
                case WebSocketMessageType.Text:
                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    if (Debug)
                        _logger.LogDebug($"WebSocket Text message. {message}");
                    break;
                case WebSocketMessageType.Binary:
                    if (Debug)
                        _logger.LogDebug($"WebSocket Binary message.");
                    break;
            }
        }
    }

    public async Task SendAsync(string message)
    {
        if(ws is null)
        {
            if (Debug)
                _logger.LogDebug($"WebSocket SendAsync failed. WebSocketClient is null");
            return;
        }

        if (ws.State != WebSocketState.Open)
        {
            if (Debug)
                _logger.LogDebug($"WebSocket SendAsync failed. State={ws.State}");
            return;
        }    

        message = "{\"action\": \"message\", \"content\": \"{message}\"}";

        var bytes = Encoding.UTF8.GetBytes(message);

        await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);

        if (Debug)
            _logger.LogDebug($"WebSocket SendAsync done. State={ws.State}");

    }

    public async Task DisconnectAsync()
    {
        if (ws is null) return;

        if (ws.State == WebSocketState.Open)
        {
            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
            if (Debug)
                _logger.LogDebug($"DisconnectAsync called. ws.State={ws.State.ToString()}");
        }
        ws = null;  
    }

    public override void Dispose()
    {
        base.Dispose(); 
    }
}


