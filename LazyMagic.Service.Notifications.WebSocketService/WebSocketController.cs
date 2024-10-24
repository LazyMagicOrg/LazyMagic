namespace LazyMagic.Service.Notifications.WebSocketService;

[ApiController]
[Route("/ws")]
public class WebSocketController : ControllerBase, IWebSocketController
{
    private readonly WebSocketConnectionManager _connectionManager;

    public WebSocketController()
    {
        _connectionManager = new WebSocketConnectionManager();  
    }

    [HttpGet]
    public async Task Get()
    {
        if (HttpContext.WebSockets.IsWebSocketRequest)
        {
            WebSocket webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
            string connectionId = _connectionManager.AddSocket(webSocket);

            // Now handle the WebSocket communication
            await HandleWebSocketCommunication(webSocket, connectionId);
        }
        else
        {
            HttpContext.Response.StatusCode = 400;
        }
    }

    private async Task HandleWebSocketCommunication(WebSocket webSocket, string connectionId)
    {
        var buffer = new byte[1024 * 4];
        try
        {
            WebSocketReceiveResult result;
            do
            {
                result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                switch (result.MessageType)
                {
                    case WebSocketMessageType.Text:
                        string receivedMessage = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        if (!receivedMessage.StartsWith("getConnectionId"))
                            throw new Exception("Text message not supported.");
                        await _connectionManager.SendMessageAsync(connectionId, $"{{ \"connectionId\":\"{connectionId}\"}}");
                        break;
                    case WebSocketMessageType.Binary:
                        throw new Exception("Binary message not supported.");
                    case WebSocketMessageType.Close:
                        await HandleClose(webSocket, result);
                        break;
                }
            }
            while (!result.CloseStatus.HasValue);
        }
        catch (WebSocketException ex)
        {
            Console.WriteLine($"HandleWebSocketCommunication error: {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"HandleWebSocketCommunication error: {ex.Message}");
        }
        finally
        {
            await _connectionManager.RemoveSocketAsync(connectionId);
        }
    }
    private async Task HandleClose(WebSocket webSocket, WebSocketReceiveResult result)
    {
        if (result.CloseStatus.HasValue)
            await webSocket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
    }

}
