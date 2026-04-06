using Fuzn.FluentWebSocket;

namespace Fuzn.TestFuzn.Plugins.WebSocket;

/// <summary>
/// Configuration options for the WebSocket plugin.
/// </summary>
public class PluginConfiguration
{
    /// <summary>
    /// Gets or sets the default timeout for establishing WebSocket connections.
    /// Default value is 10 seconds.
    /// </summary>
    public TimeSpan DefaultConnectionTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Gets or sets the default interval for keep-alive ping messages.
    /// Default value is 30 seconds.
    /// </summary>
    public TimeSpan DefaultKeepAliveInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets whether failed connections should be logged to the test console.
    /// Default value is false.
    /// </summary>
    public bool LogFailedConnectionsToTestConsole { get; set; }

    /// <summary>
    /// Gets or sets the receive buffer size in bytes.
    /// Default value is 4096 bytes (4 KB).
    /// </summary>
    public int ReceiveBufferSize { get; set; } = 4096;

    /// <summary>
    /// Gets or sets the maximum allowed message size in bytes.
    /// Messages exceeding this limit will cause an exception during receive.
    /// Set to 0 for no limit. Default value is 0.
    /// </summary>
    public long MaxMessageSize { get; set; } = 0;

    /// <summary>
    /// Gets or sets the serializer provider for JSON serialization and deserialization.
    /// Default value is <see cref="Fuzn.FluentWebSocket.SystemTextJsonSerializerProvider"/>.
    /// </summary>
    public ISerializerProvider Serializer { get; set; } = new SystemTextJsonSerializerProvider();
}
