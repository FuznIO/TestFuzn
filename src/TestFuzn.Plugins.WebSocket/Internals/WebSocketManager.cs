using System.Net.WebSockets;

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
        {
            connectionsToCleanup = new List<WebSocketConnection>(_connections);
        }

        foreach (var connection in connectionsToCleanup)
        {
            try
            {
                // Only close if the connection is still open
                if (connection.State == WebSocketState.Open)
                    await connection.Close(WebSocketCloseStatus.NormalClosure, "Test scenario completed - auto cleanup");
                
                // Dispose the connection to release resources
                await connection.DisposeAsync();
            }
            catch (Exception)
            {
                // Swallow exceptions during cleanup - connection may already be closed/disposed
                // The test framework logger might not be available here
            }
        }

        lock (_connectionsLock)
        {
            _connections.Clear();
        }
    }
}
