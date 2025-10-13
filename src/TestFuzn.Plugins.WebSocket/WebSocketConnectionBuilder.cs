using Fuzn.TestFuzn.Plugins.WebSocket.Internals;

namespace Fuzn.TestFuzn.Plugins.WebSocket;

/// <summary>
/// Fluent builder for configuring and establishing WebSocket connections.
/// </summary>
public class WebSocketConnectionBuilder
{
    private readonly Context _context;
    private readonly string _url;
    private readonly Dictionary<string, string> _headers = new();
    private Hooks _hooks = new();
    private LoggingVerbosity _loggingVerbosity = LoggingVerbosity.Full;
    private TimeSpan _connectionTimeout = WebSocketGlobalState.Configuration.DefaultConnectionTimeout;
    private TimeSpan _keepAliveInterval = WebSocketGlobalState.Configuration.DefaultKeepAliveInterval;
    private string? _subProtocol;
    private int? _receiveBufferSizeOverride;
    private int? _maxBufferedMessagesOverride;

    internal WebSocketConnectionBuilder(Context context, string url)
    {
        _context = context;
        _url = url;
    }

    /// <summary>
    /// Adds a custom header to the WebSocket connection request.
    /// </summary>
    /// <param name="key">The header name.</param>
    /// <param name="value">The header value.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public WebSocketConnectionBuilder Header(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Header key cannot be null or empty.", nameof(key));

        _headers[key] = value ?? string.Empty;
        return this;
    }

    /// <summary>
    /// Adds multiple custom headers to the WebSocket connection request.
    /// </summary>
    /// <param name="headers">A dictionary of headers to add.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public WebSocketConnectionBuilder Headers(IDictionary<string, string> headers)
    {
        if (headers == null)
            throw new ArgumentNullException(nameof(headers));

        foreach (var header in headers)
        {
            if (string.IsNullOrWhiteSpace(header.Key))
                throw new ArgumentException("Header key cannot be null or empty.");
            
            _headers[header.Key] = header.Value ?? string.Empty;
        }
        return this;
    }

    /// <summary>
    /// Configures lifecycle hooks for the WebSocket connection.
    /// </summary>
    /// <param name="hooks">The hooks to register.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public WebSocketConnectionBuilder Hooks(Hooks hooks)
    {
        _hooks = hooks ?? throw new ArgumentNullException(nameof(hooks));
        return this;
    }

    /// <summary>
    /// Sets the logging verbosity level for this connection.
    /// </summary>
    /// <param name="verbosity">The verbosity level.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public WebSocketConnectionBuilder Verbosity(LoggingVerbosity verbosity)
    {
        _loggingVerbosity = verbosity;
        return this;
    }

    /// <summary>
    /// Sets the connection timeout for establishing the WebSocket connection.
    /// </summary>
    /// <param name="timeout">The timeout duration.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <exception cref="ArgumentException">Thrown if timeout is zero or negative.</exception>
    public WebSocketConnectionBuilder ConnectionTimeout(TimeSpan timeout)
    {
        if (timeout <= TimeSpan.Zero)
            throw new ArgumentException("Connection timeout must be greater than zero.", nameof(timeout));

        _connectionTimeout = timeout;
        return this;
    }

    /// <summary>
    /// Sets the keep-alive interval for the WebSocket connection.
    /// </summary>
    /// <param name="interval">The keep-alive interval.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <exception cref="ArgumentException">Thrown if interval is negative.</exception>
    public WebSocketConnectionBuilder KeepAliveInterval(TimeSpan interval)
    {
        if (interval < TimeSpan.Zero)
            throw new ArgumentException("Keep-alive interval cannot be negative.", nameof(interval));

        _keepAliveInterval = interval;
        return this;
    }

    /// <summary>
    /// Sets the WebSocket sub-protocol to request during the handshake.
    /// </summary>
    /// <param name="subProtocol">The sub-protocol name.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public WebSocketConnectionBuilder SubProtocol(string subProtocol)
    {
        if (string.IsNullOrWhiteSpace(subProtocol))
            throw new ArgumentException("Sub-protocol cannot be null or empty.", nameof(subProtocol));
        _subProtocol = subProtocol;
        return this;
    }

    /// <summary>
    /// Overrides the default receive buffer size (bytes) for this connection only.
    /// </summary>
    /// <param name="size">Buffer size in bytes (minimum 128).</param>
    public WebSocketConnectionBuilder ReceiveBufferSize(int size)
    {
        if (size < 128)
            throw new ArgumentOutOfRangeException(nameof(size), "Receive buffer size must be at least 128 bytes.");
        _receiveBufferSizeOverride = size;
        return this;
    }

    /// <summary>
    /// Overrides the maximum number of buffered text messages for this connection.
    /// Use 0 for unlimited buffering (not recommended for large / load tests).
    /// </summary>
    /// <param name="maxMessages">Max messages (>=0).</param>
    public WebSocketConnectionBuilder MaxBufferedMessages(int maxMessages)
    {
        if (maxMessages < 0)
            throw new ArgumentOutOfRangeException(nameof(maxMessages), "Max buffered messages must be >= 0.");
        _maxBufferedMessagesOverride = maxMessages;
        return this;
    }

    /// <summary>
    /// Builds a WebSocket connection instance without establishing the connection.
    /// </summary>
    /// <returns>A configured WebSocket connection instance.</returns>
    public WebSocketConnection Build()
    {
        var connection = new WebSocketConnection(
            _context,
            _url,
            _receiveBufferSizeOverride,
            _maxBufferedMessagesOverride)
        {
            Hooks = _hooks,
            ConnectionTimeout = _connectionTimeout,
            KeepAliveInterval = _keepAliveInterval,
            SubProtocol = _subProtocol
        };
        
        foreach (var header in _headers)
            connection.Headers[header.Key] = header.Value;
            
        connection.SetLoggingVerbosity(_loggingVerbosity);
        
        return connection;
    }

    /// <summary>
    /// Builds and immediately connects to the WebSocket server.
    /// </summary>
    /// <returns>A connected WebSocket connection instance.</returns>
    public Task<WebSocketConnection> Connect() => Connect(CancellationToken.None);

    /// <summary>
    /// Builds and immediately connects to the WebSocket server with cancellation support.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the connect operation.</param>
    public async Task<WebSocketConnection> Connect(CancellationToken cancellationToken)
    {
        var connection = Build();
        await connection.Connect(cancellationToken);
        return connection;
    }
}
