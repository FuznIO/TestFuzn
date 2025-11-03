using System.Net.WebSockets;
using Microsoft.Extensions.Logging;

namespace Fuzn.TestFuzn.Plugins.WebSocket.Internals;

internal class WebSocketManager
{
    private readonly IList<WebSocketConnection> _connections = new List<WebSocketConnection>();
    private readonly object _connectionsLock = new object();

    public void TrackConnection(WebSocketConnection connection)
    {
        lock (_connectionsLock)
        {
            _connections.Add(connection);
        }
    }

    public async ValueTask CleanupContext()
    {
        List<WebSocketConnection> connectionsToCleanup;
        lock (_connectionsLock)
            connectionsToCleanup = new List<WebSocketConnection>(_connections);

        foreach (var connection in connectionsToCleanup)
        {
            try
            {
                // Skip already disposed connections
                if (connection.IsDisposed)
                    continue;

                // Close only if fully open or in close-received state
                if (connection.State == WebSocketState.Open || connection.State == WebSocketState.CloseReceived)
                    await connection.Close(WebSocketCloseStatus.NormalClosure, "Test scenario completed - auto cleanup");

                // Dispose (will skip Close again if not open)
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
