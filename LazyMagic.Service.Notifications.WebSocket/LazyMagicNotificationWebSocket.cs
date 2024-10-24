
[assembly: LambdaSerializer(typeof(DefaultLambdaJsonSerializer))]

namespace LazyMagic.Service.Notifications.WebSocket;

/// <summary>
/// This lambda handles connection requests from users. 
/// /// </summary>
public class LazyMagicNotificationWebSocket
{
    public LazyMagicNotificationWebSocket()
    {
        //var logConfig = new NLog.Config.LoggingConfiguration();
        //var logConsole = new NLog.Targets.ConsoleTarget("logconsole");
        //logConfig.AddRuleForAllLevels(logConsole);
        //NLog.LogManager.Configuration = logConfig;

        //logger.Info("Logging configured");
    }

    //private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
    private IAmazonApiGatewayManagementApi? gatewayManagementApi;

    public async Task<APIGatewayProxyResponse> FunctionHandlerAsync(APIGatewayProxyRequest input, ILambdaContext context)
    {
        var apiGatewayUrl = $"https://{input.RequestContext.DomainName}/{input.RequestContext.Stage}";
        
        try
        {
            gatewayManagementApi ??= new AmazonApiGatewayManagementApiClient(
                new AmazonApiGatewayManagementApiConfig 
                {
                    ServiceURL = $"https://{input.RequestContext.DomainName}/{input.RequestContext.Stage}"
                });

            //logger.Info($"EventType: {input.RequestContext.EventType}  ConnectionId: {input.RequestContext.ConnectionId}");
            //logger.Info($"apiGatewayUrl:{apiGatewayUrl}");
            switch(input.RequestContext.EventType)
            {
                case "CONNECT":
                    //logger.Info($"CONNECT");
                    return new APIGatewayProxyResponse
                    {
                        StatusCode = (int)HttpStatusCode.OK,
                        Body = input.RequestContext.ConnectionId
                    };
                case "MESSAGE":
                    var message = input.Body;
                    var messageObj = JObject.Parse(message);
                    var eventType = messageObj["messageType"];
                    if (eventType == null || eventType.ToString() != "getConnectionId")
                        return new APIGatewayProxyResponse
                        {
                            StatusCode = (int)HttpStatusCode.BadRequest
                        };
                    //logger.Info($"Message received: {message}");
                    await SendWebSocketMessageAsync(input.RequestContext.ConnectionId, $"{{ \"connectionId\":\"{input.RequestContext.ConnectionId}\"}}");
                    return new APIGatewayProxyResponse
                    {
                        StatusCode = (int)HttpStatusCode.OK,
                        Body = input.RequestContext.ConnectionId
                    };
                case "DISCONNECT":
                    //logger.Info($"Disconnect: {input.RequestContext.ConnectionId}");
                    return new APIGatewayProxyResponse
                    {
                        StatusCode = (int)HttpStatusCode.OK
                    };
                default:
                    //logger.Warn($"Route: {input.RequestContext.EventType} not supported.");
                    return new APIGatewayProxyResponse
                    {
                        StatusCode = (int)HttpStatusCode.BadRequest,
                        Body = "Bad Request"
                    };
            }
        }
        catch (UnauthorizedAccessException)
        {
            //logger.Warn($"Route: {input.RequestContext.EventType} unauthorized request.");
            return new APIGatewayProxyResponse
            {
                StatusCode = (int)HttpStatusCode.Unauthorized
            };
        }
        catch (Exception ex)
        {
            //logger.Warn($"Route: {input.RequestContext.EventType} exception {ex.Message}.");
            return new APIGatewayProxyResponse
            {
                StatusCode = (int)HttpStatusCode.BadRequest
            };
        }
    }
    private async Task SendWebSocketMessageAsync(string connectionId, string message)
    {
        if (gatewayManagementApi == null)
        {
            //logger.Info($"SendWebSocketMessageAsync called before gatewayManagementApi initialized.");
            return;
        }
        try
        {
            //logger.Info($"Post to connectin: {connectionId}");
            await gatewayManagementApi.PostToConnectionAsync(
                new PostToConnectionRequest
                {
                    ConnectionId = connectionId,
                    Data = new MemoryStream(Encoding.UTF8.GetBytes(message))
                });
        } catch (Exception ex) {
            //logger.Warn($"PostToConnectionAsync() failed on connection {connectionId}. {ex.Message}");

        }
    }
}
