namespace LazyMagic.Service.Notifications.WebSocketService;
public class WebSocketConnectionManager : IWebSocketConnectionManager
{
    private ConcurrentDictionary<string, WebSocket> _sockets = new ConcurrentDictionary<string, WebSocket>();

    public string AddSocket(WebSocket socket)
    {
        string connectionId = Guid.NewGuid().ToString();
        _sockets.TryAdd(connectionId, socket);
        return connectionId;
    }

    public async Task RemoveSocketAsync(string id)
    {
        if (_sockets.TryRemove(id, out var socket))
            await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
    }

    public async Task SendMessageAsync(string connectionId, string message)
    {
        if (_sockets.TryGetValue(connectionId, out var socket))
        {
            if (socket.State == WebSocketState.Open)
            {
                var buffer = Encoding.UTF8.GetBytes(message);
                var segment = new ArraySegment<byte>(buffer);

                await socket.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None);
            }
        }
    }
}