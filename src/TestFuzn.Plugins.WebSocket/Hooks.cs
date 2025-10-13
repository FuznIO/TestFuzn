namespace Fuzn.TestFuzn.Plugins.WebSocket;

/// <summary>
/// Lifecycle hooks for WebSocket connections.
/// </summary>
public class Hooks
{
    /// <summary>
    /// Called immediately before connecting to the WebSocket server.
    /// </summary>
    public Action<WebSocketConnection>? PreConnect { get; set; }

    /// <summary>
    /// Called immediately after successfully connecting to the WebSocket server.
    /// </summary>
    public Action<WebSocketConnection>? PostConnect { get; set; }

    /// <summary>
    /// Called whenever a message is received from the WebSocket server.
    /// </summary>
    public Action<WebSocketConnection, string>? OnMessageReceived { get; set; }

    /// <summary>
    /// Called when the WebSocket connection is closed.
    /// </summary>
    public Action<WebSocketConnection>? OnDisconnect { get; set; }
}
