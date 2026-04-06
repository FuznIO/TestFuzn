using Fuzn.FluentWebSocket;
using Fuzn.TestFuzn.Plugins.WebSocket.Internals;
using Microsoft.Extensions.DependencyInjection;
using System.Net.WebSockets;

namespace Fuzn.TestFuzn.Plugins.WebSocket;

/// <summary>
/// Extension methods for creating WebSocket connections from test contexts.
/// </summary>
public static class IContextExtensions
{
    /// <summary>
    /// Creates, configures, and connects a WebSocket connection to the specified URL.
    /// The connection is automatically tracked for cleanup when the test iteration ends.
    /// </summary>
    /// <param name="context">The test context.</param>
    /// <param name="url">The WebSocket URL to connect to (must start with ws:// or wss://).</param>
    /// <param name="configure">Optional action to configure the connection request before connecting.</param>
    /// <returns>A connected <see cref="FluentWebSocketConnection"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the WebSocket plugin has not been initialized.</exception>
    /// <example>
    /// <code>
    /// // Simple connection
    /// var connection = await context.CreateWebSocketConnection("wss://example.com/ws");
    ///
    /// // Connection with configuration
    /// var connection = await context.CreateWebSocketConnection("wss://example.com/ws", request => request
    ///     .WithHeader("Authorization", "Bearer token")
    ///     .WithTimeout(TimeSpan.FromSeconds(15)));
    /// </code>
    /// </example>
    public static async Task<FluentWebSocketConnection> CreateWebSocketConnection(this Context context, string url,
        Action<FluentWebSocketRequest>? configure = null)
    {
        var globalState = context.ServicesProvider.GetRequiredService<WebSocketGlobalState>();

        if (!globalState.HasBeenInitialized)
            throw new InvalidOperationException("TestFuzn WebSocket plugin has not been initialized. Please call configuration.UseWebSocket() in the Startup.");

        var config = globalState.Configuration;
        var webSocketManager = context.Internals.Plugins.GetState<WebSocketManager>(typeof(WebSocketPlugin));

        var request = new ClientWebSocket().Url(url)
            .WithCancellationToken(context.Info.CancellationToken)
            .WithTimeout(config.DefaultConnectionTimeout)
            .WithKeepAliveInterval(config.DefaultKeepAliveInterval)
            .WithReceiveBufferSize(config.ReceiveBufferSize)
            .WithMaxMessageSize(config.MaxMessageSize)
            .WithSerializer(config.Serializer);

        request.WithLogger(context.Logger);

        configure?.Invoke(request);

        var connection = await request.Connect();

        webSocketManager.TrackConnection(connection);

        return connection;
    }
}
