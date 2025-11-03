using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace SampleApp.WebApp.WebSockets;

public class WebSocketHandler
{
    private readonly ILogger<WebSocketHandler> _logger;

    public WebSocketHandler(ILogger<WebSocketHandler> logger)
    {
        _logger = logger;
    }

    public async Task HandleWebSocketConnection(HttpContext context, WebSocket webSocket)
    {
        _logger.LogInformation("WebSocket connection established");

        var buffer = new byte[1024 * 4];

        try
        {
            while (webSocket.State == WebSocketState.Open)
            {
                var result = await webSocket.ReceiveAsync(
                    new ArraySegment<byte>(buffer),
                    CancellationToken.None);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await webSocket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "Closing",
                        CancellationToken.None);
                    _logger.LogInformation("WebSocket connection closed by client");
                }
                else if (result.MessageType == WebSocketMessageType.Text)
                {
                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    _logger.LogInformation($"Received message: {message}");

                    // Echo the message back
                    var responseBytes = Encoding.UTF8.GetBytes(message);
                    await webSocket.SendAsync(
                        new ArraySegment<byte>(responseBytes),
                        WebSocketMessageType.Text,
                        true,
                        CancellationToken.None);

                    _logger.LogInformation($"Echoed message back: {message}");
                }
                else if (result.MessageType == WebSocketMessageType.Binary)
                {
                    _logger.LogInformation($"Received binary message ({result.Count} bytes)");

                    // Echo binary data back
                    await webSocket.SendAsync(
                        new ArraySegment<byte>(buffer, 0, result.Count),
                        WebSocketMessageType.Binary,
                        true,
                        CancellationToken.None);
                }
            }
        }
        catch (WebSocketException ex)
        {
            _logger.LogError(ex, "WebSocket error occurred");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in WebSocket handler");
        }
        finally
        {
            if (webSocket.State != WebSocketState.Closed && webSocket.State != WebSocketState.Aborted)
            {
                await webSocket.CloseAsync(
                    WebSocketCloseStatus.InternalServerError,
                    "Server error",
                    CancellationToken.None);
            }
        }
    }
}
