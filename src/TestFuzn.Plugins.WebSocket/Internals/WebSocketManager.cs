using Fuzn.FluentWebSocket;

namespace Fuzn.TestFuzn.Plugins.WebSocket.Internals;

internal class WebSocketManager
{
    private readonly List<FluentWebSocketConnection> _connections = new();
    private readonly object _connectionsLock = new();

    public void TrackConnection(FluentWebSocketConnection connection)
    {
        lock (_connectionsLock)
        {
            _connections.Add(connection);
        }
    }

    public async ValueTask CleanupIteration()
    {
        List<FluentWebSocketConnection> connectionsToCleanup;
        lock (_connectionsLock)
            connectionsToCleanup = new List<FluentWebSocketConnection>(_connections);

        foreach (var connection in connectionsToCleanup)
        {
            try
            {
                await connection.DisposeAsync();
            }
            catch
            {
                // Swallow during cleanup
            }
        }

        lock (_connectionsLock)
            _connections.Clear();
    }
}
