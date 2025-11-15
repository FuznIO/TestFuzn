using Fuzn.TestFuzn.Plugins.WebSocket.Internals;

namespace Fuzn.TestFuzn.Plugins.WebSocket;

public class WebSocketConnectionBuilder
{
    private readonly Context _context;
    private readonly string _url;
    private readonly WebSocketManager _manager;
    private readonly Dictionary<string, string> _headers = new();
    private Action<WebSocketConnection>? _onPreConnect;
    private Action<WebSocketConnection>? _onPostConnect;
    private Action<WebSocketConnection, string>? _onMessageReceived;
    private Action<WebSocketConnection>? _onDisconnect;
    private LoggingVerbosity _loggingVerbosity = LoggingVerbosity.Full;
    private TimeSpan _connectionTimeout = WebSocketGlobalState.Configuration.DefaultConnectionTimeout;
    private TimeSpan _keepAliveInterval = WebSocketGlobalState.Configuration.DefaultKeepAliveInterval;
    private string? _subProtocol;

    internal WebSocketConnectionBuilder(Context context, string url, WebSocketManager manager)
    {
        _context = context;
        _url = url;
        _manager = manager;
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
    /// Sets the hook called immediately before connecting to the WebSocket server.
    /// </summary>
    /// <param name="hook">The hook action.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public WebSocketConnectionBuilder OnPreConnect(Action<WebSocketConnection> hook)
    {
        _onPreConnect = hook ?? throw new ArgumentNullException(nameof(hook));
        return this;
    }

    /// <summary>
    /// Sets the hook called immediately after successfully connecting to the WebSocket server.
    /// </summary>
    /// <param name="hook">The hook action.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public WebSocketConnectionBuilder OnPostConnect(Action<WebSocketConnection> hook)
    {
        _onPostConnect = hook ?? throw new ArgumentNullException(nameof(hook));
        return this;
    }

    /// <summary>
    /// Sets the hook called whenever a message is received from the WebSocket server.
    /// </summary>
    /// <param name="hook">The hook action.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public WebSocketConnectionBuilder OnMessageReceived(Action<WebSocketConnection, string> hook)
    {
        _onMessageReceived = hook ?? throw new ArgumentNullException(nameof(hook));
        return this;
    }

    /// <summary>
    /// Sets the hook called when the WebSocket connection is closed.
    /// </summary>
    /// <param name="hook">The hook action.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public WebSocketConnectionBuilder OnDisconnect(Action<WebSocketConnection> hook)
    {
        _onDisconnect = hook ?? throw new ArgumentNullException(nameof(hook));
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
    /// Builds a WebSocket connection instance without establishing the connection.
    /// </summary>
    /// <returns>A configured WebSocket connection instance.</returns>
    public WebSocketConnection Build()
    {
        var connection = new WebSocketConnection(_context, _url)
        {
            OnPreConnect = _onPreConnect,
            OnPostConnect = _onPostConnect,
            OnMessageReceived = _onMessageReceived,
            OnDisconnect = _onDisconnect,
            ConnectionTimeout = _connectionTimeout,
            KeepAliveInterval = _keepAliveInterval,
            SubProtocol = _subProtocol
        };
        
        foreach (var header in _headers)
            connection.Headers[header.Key] = header.Value;
            
        connection.SetLoggingVerbosity(_loggingVerbosity);
        
        // Track the connection in the manager for automatic cleanup
        _manager.TrackConnection(connection);
        
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
