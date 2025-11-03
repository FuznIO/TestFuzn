using Fuzn.TestFuzn.Plugins.WebSocket.Internals;

namespace Fuzn.TestFuzn.Plugins.WebSocket;

/// <summary>
/// Extension methods for creating WebSocket connections from test contexts.
/// </summary>
public static class IContextExtensions
{
    /// <summary>
    /// Creates a new WebSocket connection builder for the specified URL.
    /// </summary>
    /// <param name="context">The test context.</param>
    /// <param name="url">The WebSocket URL to connect to (must start with ws:// or wss://).</param>
    /// <returns>A builder for configuring and establishing the WebSocket connection.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the WebSocket plugin has not been initialized.</exception>
    /// <example>
    /// <code>
    /// var connection = await context.CreateWebSocketConnection("wss://example.com/ws")
    ///     .Header("Authorization", "Bearer token")
    ///     .ConnectionTimeout(TimeSpan.FromSeconds(15))
    ///     .Connect();
    /// </code>
    /// </example>
    public static WebSocketConnectionBuilder CreateWebSocketConnection(this Context context, string url)
    {
        if (!WebSocketGlobalState.HasBeenInitialized)
            throw new InvalidOperationException("TestFuzn WebSocket plugin has not been initialized. Please call configuration.UseWebSocket() in the Startup.");

        var webSocketManager = context.Internals.Plugins.GetState<WebSocketManager>(typeof(WebSocketPlugin));

        return new WebSocketConnectionBuilder(context, url, webSocketManager);
    }
}
