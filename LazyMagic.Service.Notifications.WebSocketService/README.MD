# LazyMagic.Notifications.WebSocketService 

This is a simple WebSocket service that can be used to send notifications to clients. This library is useful when using the local WebApi for debugging. This library is not intended for use in an AWS Stack or as a full-featured WebSocket service.

AWS Note: On AWS, we use the API Gateway and that service handles WebSocket connections. The AWS API Gateway is a robust solution but doesn't run locally so we use this library for local debugging.

If you are considering a production application that doesn't use AWS API Gateway, you should consider using SignalR instead of this library. 
- The client side LzNotificationSvc library currently uses the .NET ClientWebSocket class. 
- If you use SignalR server-side and don't use AWS API Gateway, you may want to consider creating a version of the LzNotificationSvc library that uses the more robust SignalR Client library.

## Limitations
This class is not intended for use in a production application. It is intended for use in a local debugging environment running a single server instance.

## Objective 
WebSocket connections are persistent and stateful. Therefore, you need a way to manage multiple connections simultaneously. 

This library provides a simple way to manage multiple WebSocket connections. It also provides a simple way to send notifications to all connected clients.

### WebSocketConnectionManager
A class that maintains a collection of all active WebSocket connections. Each connection is identified by a unique ID, which makes it easier to handle each connection individually.

public string AddSocket(WebSocket socket)); 
public async Task RemoveSocket(string id);

### WebSocketController : ControllerBase

