using Fuzn.TestFuzn.Plugins.WebSocket.Internals;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;

namespace Fuzn.TestFuzn.Plugins.WebSocket;

/// <summary>
/// Represents a WebSocket connection for testing purposes.
/// Supports sending/receiving messages, JSON serialization, and lifecycle management.
/// </summary>
public class WebSocketConnection : IDisposable, IAsyncDisposable
{
    private readonly Context _context;
    private readonly string _url;
    private readonly int _receiveBufferSize;
    private readonly int _maxBufferedMessages;
    private ClientWebSocket? _webSocket;
    private LoggingVerbosity _verbosity = LoggingVerbosity.Full;
    private CancellationTokenSource? _receiveCts;
    private Task? _receiveTask;
    private readonly List<string> _receivedMessages = new();

    private readonly object _messagesLock = new();
    private bool _disposed;

    /// <summary>
    /// Gets the custom headers that will be sent with the WebSocket handshake request.
    /// </summary>
    public Dictionary<string, string> Headers { get; private set; } = new();

    /// <summary>
    /// Gets or sets the hook called immediately before connecting to the WebSocket server.
    /// </summary>
    public Action<WebSocketConnection>? OnPreConnect { get; set; }

    /// <summary>
    /// Gets or sets the hook called immediately after successfully connecting to the WebSocket server.
    /// </summary>
    public Action<WebSocketConnection>? OnPostConnect { get; set; }

    /// <summary>
    /// Gets or sets the hook called whenever a message is received from the WebSocket server.
    /// </summary>
    public Action<WebSocketConnection, string>? OnMessageReceived { get; set; }

    /// <summary>
    /// Gets or sets the hook called when the WebSocket connection is closed.
    /// </summary>
    public Action<WebSocketConnection>? OnDisconnect { get; set; }

    /// <summary>
    /// Gets or sets the connection timeout.
    /// </summary>
    public TimeSpan ConnectionTimeout { get; set; } = WebSocketGlobalState.Configuration.DefaultConnectionTimeout;

    /// <summary>
    /// Gets or sets the keep-alive interval for ping messages.
    /// </summary>
    public TimeSpan KeepAliveInterval { get; set; } = WebSocketGlobalState.Configuration.DefaultKeepAliveInterval;

    /// <summary>
    /// Gets or sets the WebSocket sub-protocol to request during handshake.
    /// </summary>
    public string? SubProtocol { get; set; }

    /// <summary>
    /// Gets the current state of the WebSocket connection.
    /// </summary>
    public WebSocketState State => _webSocket?.State ?? WebSocketState.None;

    /// <summary>
    /// Gets a value indicating whether the WebSocket is currently connected.
    /// </summary>
    public bool IsConnected => _webSocket?.State == WebSocketState.Open;

    /// <summary>
    /// Indicates whether this connection object has been disposed.
    /// </summary>
    public bool IsDisposed => _disposed;

    internal WebSocketConnection(Context context, string url, int? receiveBufferSizeOverride = null, int? maxBufferedMessagesOverride = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _url = url ?? throw new ArgumentNullException(nameof(url));
        _receiveBufferSize = receiveBufferSizeOverride ?? WebSocketGlobalState.Configuration.ReceiveBufferSize;
        _maxBufferedMessages = maxBufferedMessagesOverride ?? WebSocketGlobalState.Configuration.MaxMessageBufferSize;
        if (_receiveBufferSize < 128)
            throw new ArgumentOutOfRangeException(nameof(receiveBufferSizeOverride), "Receive buffer size must be at least 128 bytes.");
        if (_maxBufferedMessages < 0)
            throw new ArgumentOutOfRangeException(nameof(maxBufferedMessagesOverride), "Max buffered messages must be >= 0.");
    }

    /// <summary>
    /// Sets the logging verbosity for this connection.
    /// </summary>
    /// <param name="loggingVerbosity">The verbosity level.</param>
    /// <returns>This connection instance for method chaining.</returns>
    public WebSocketConnection SetLoggingVerbosity(LoggingVerbosity loggingVerbosity)
    {
        _verbosity = loggingVerbosity;
        return this;
    }

    /// <summary>
    /// Establishes a connection to the WebSocket server.
    /// </summary>
    /// <exception cref="WebSocketException">Thrown if the connection fails.</exception>
    /// <exception cref="TimeoutException">Thrown if the connection times out.</exception>
    /// <exception cref="ObjectDisposedException">Thrown if the connection has been disposed.</exception>
    public Task Connect() => Connect(CancellationToken.None);

    /// <summary>
    /// Establishes a connection to the WebSocket server.
    /// </summary>
    /// <param name="cancellationToken">A token to observe while waiting for the connection attempt to complete.</param>
    /// <exception cref="WebSocketException">Thrown if the connection fails.</exception>
    /// <exception cref="TimeoutException">Thrown if the connection times out.</exception>
    /// <exception cref="ObjectDisposedException">Thrown if the connection has been disposed.</exception>
    public async Task Connect(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Local function to perform actual connect (allows retry without duplicating logic)
        async Task DoConnectAsync(CancellationToken ct)
        {
            _webSocket = new ClientWebSocket();
            _webSocket.Options.KeepAliveInterval = KeepAliveInterval;

            foreach (var header in Headers)
            {
                try
                {
                    _webSocket.Options.SetRequestHeader(header.Key, header.Value);
                }
                catch (ArgumentException ex)
                {
                    throw new InvalidOperationException($"Invalid WebSocket header '{header.Key}': {ex.Message}", ex);
                }
            }

            if (!string.IsNullOrEmpty(SubProtocol))
                _webSocket.Options.AddSubProtocol(SubProtocol);

            OnPreConnect?.Invoke(this);

            if (_verbosity >= LoggingVerbosity.Minimal)
                _context.Logger.LogInformation($"Step {_context.StepInfo?.Name} - WebSocket Connecting: {_url} - CorrelationId: {_context.Info.CorrelationId}");

            using var timeoutCts = new CancellationTokenSource(ConnectionTimeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, ct);
            await _webSocket.ConnectAsync(new Uri(_url), linkedCts.Token);

            if (_verbosity >= LoggingVerbosity.Minimal)
                _context.Logger.LogInformation($"Step {_context.StepInfo?.Name} - WebSocket Connected: {_url} - State: {_webSocket.State} - CorrelationId: {_context.Info.CorrelationId}");

            OnPostConnect?.Invoke(this);

            _receiveCts = new CancellationTokenSource();
            _receiveTask = ReceiveLoop(_receiveCts.Token);
        }

        var start = Stopwatch.StartNew();
        var triedRetry = false;

        try
        {
            await DoConnectAsync(cancellationToken);
        }
        catch (ObjectDisposedException) when (!triedRetry && start.ElapsedMilliseconds < 200)
        {
            // Transient disposal during handshake (e.g., premature cleanup or server rejecting quickly).
            triedRetry = true;
            if (_verbosity >= LoggingVerbosity.Minimal)
                _context.Logger.LogWarning($"Step {_context.StepInfo?.Name} - Transient disposal during connect. Retrying once. {_url}");
            await DoConnectAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (_verbosity > LoggingVerbosity.None)
                _context.Logger.LogError(ex, $"WebSocket connection failed: {_url}");

            if (WebSocketGlobalState.Configuration.LogFailedConnectionsToTestConsole)
                _context.Logger.LogError($"WebSocket connection failed: {_url}\nError: {ex.Message}");

            throw;
        }
    }

    /// <summary>
    /// Sends a text message through the WebSocket connection.
    /// </summary>
    /// <param name="message">The message to send.</param>
    /// <exception cref="InvalidOperationException">Thrown if the connection is not open.</exception>
    /// <exception cref="ObjectDisposedException">Thrown if the connection has been disposed.</exception>
    public async Task SendText(string message)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!IsConnected)
            throw new InvalidOperationException("WebSocket is not connected.");

        if (message == null)
            throw new ArgumentNullException(nameof(message));

        var bytes = Encoding.UTF8.GetBytes(message);
        var buffer = new ArraySegment<byte>(bytes);

        if (_verbosity == LoggingVerbosity.Full)
            _context.Logger.LogInformation($"Step {_context.StepInfo?.Name} - WebSocket Sending: {message} - CorrelationId: {_context.Info.CorrelationId}");

        await _webSocket!.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
    }

    /// <summary>
    /// Sends binary data through the WebSocket connection.
    /// </summary>
    /// <param name="data">The binary data to send.</param>
    /// <exception cref="InvalidOperationException">Thrown if the connection is not open.</exception>
    /// <exception cref="ObjectDisposedException">Thrown if the connection has been disposed.</exception>
    public async Task SendBinary(byte[] data)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!IsConnected)
            throw new InvalidOperationException("WebSocket is not connected.");

        if (data == null)
            throw new ArgumentNullException(nameof(data));

        var buffer = new ArraySegment<byte>(data);

        if (_verbosity >= LoggingVerbosity.Minimal)
            _context.Logger.LogInformation($"Step {_context.StepInfo?.Name} - WebSocket Sending binary data ({data.Length} bytes) - CorrelationId: {_context.Info.CorrelationId}");

        await _webSocket!.SendAsync(buffer, WebSocketMessageType.Binary, true, CancellationToken.None);
    }

    /// <summary>
    /// Serializes an object to JSON and sends it as a text message.
    /// </summary>
    /// <typeparam name="T">The type of the object to send.</typeparam>
    /// <param name="data">The object to serialize and send.</param>
    /// <exception cref="InvalidOperationException">Thrown if the connection is not open.</exception>
    /// <exception cref="ObjectDisposedException">Thrown if the connection has been disposed.</exception>
    public async Task SendJson<T>(T data) where T : class
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        var json = GlobalState.SerializerProvider.Serialize(data);
        await SendText(json);
    }

    /// <summary>
    /// Gets all received messages from the buffer.
    /// </summary>
    /// <returns>A copy of the current message buffer.</returns>
    public List<string> GetReceivedMessages()
    {
        lock (_messagesLock)
        {
            return new List<string>(_receivedMessages);
        }
    }

    /// <summary>
    /// Clears all messages from the receive buffer.
    /// </summary>
    public void ClearReceivedMessages()
    {
        lock (_messagesLock)
        {
            _receivedMessages.Clear();
        }
    }

    /// <summary>
    /// Waits for the next message to be received, with an optional timeout.
    /// </summary>
    /// <param name="timeout">The maximum time to wait. Defaults to 30 seconds if not specified.</param>
    /// <returns>The received message.</returns>
    /// <exception cref="TimeoutException">Thrown if no message is received within the timeout period.</exception>
    public async Task<string> WaitForMessage(TimeSpan? timeout = null)
    {
        var maxWait = timeout ?? TimeSpan.FromSeconds(30);
        var startTime = DateTime.UtcNow;

        while (DateTime.UtcNow - startTime < maxWait)
        {
            lock (_messagesLock)
            {
                if (_receivedMessages.Count > 0)
                {
                    var message = _receivedMessages[0];
                    _receivedMessages.RemoveAt(0);
                    return message;
                }
            }

            await Task.Delay(100);
        }

        throw new TimeoutException($"No message received within {maxWait.TotalSeconds} seconds");
    }

    /// <summary>
    /// Waits for the next message and deserializes it from JSON.
    /// </summary>
    /// <typeparam name="T">The type to deserialize to.</typeparam>
    /// <param name="timeout">The maximum time to wait. Defaults to 30 seconds if not specified.</param>
    /// <returns>The deserialized object.</returns>
    /// <exception cref="TimeoutException">Thrown if no message is received within the timeout period.</exception>
    public async Task<T> WaitForMessageAs<T>(TimeSpan? timeout = null) where T : class
    {
        var message = await WaitForMessage(timeout);
        return GlobalState.SerializerProvider.Deserialize<T>(message);
    }

    /// <summary>
    /// Closes the WebSocket connection gracefully.
    /// </summary>
    /// <param name="closeStatus">The close status to send.</param>
    /// <param name="statusDescription">A description of why the connection is closing.</param>
    public async Task Close(WebSocketCloseStatus closeStatus = WebSocketCloseStatus.NormalClosure, string statusDescription = "Closing")
    {
        // If already disposed we cannot operate on _webSocket
        if (_disposed)
            return;

        if (_webSocket != null && (_webSocket.State == WebSocketState.Open || _webSocket.State == WebSocketState.CloseReceived))
        {
            if (_verbosity >= LoggingVerbosity.Minimal)
                _context.Logger.LogInformation($"Step {_context.StepInfo?.Name} - WebSocket Closing: {_url} - CorrelationId: {_context.Info.CorrelationId}");

            try
            {
                if (_webSocket.State == WebSocketState.Open)
                    await _webSocket.CloseAsync(closeStatus, statusDescription, CancellationToken.None);
                else if (_webSocket.State == WebSocketState.CloseReceived)
                    await _webSocket.CloseOutputAsync(closeStatus, statusDescription, CancellationToken.None);
            }
            catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.InvalidState)
            {
                if (_verbosity >= LoggingVerbosity.Minimal)
                    _context.Logger.LogInformation($"Step {_context.StepInfo?.Name} - WebSocket already closed or invalid state - CorrelationId: {_context.Info.CorrelationId}");
            }
            catch (Exception ex)
            {
                if (_verbosity > LoggingVerbosity.None)
                    _context.Logger.LogWarning(ex, $"Step {_context.StepInfo?.Name} - Error during WebSocket close - CorrelationId: {_context.Info.CorrelationId}");
            }
            finally
            {
                _receiveCts?.Cancel();
                if (_receiveTask != null)
                {
                    try { await _receiveTask.WaitAsync(TimeSpan.FromSeconds(2)); }
                    catch (TimeoutException) { }
                }
            }

            OnDisconnect?.Invoke(this);

            if (_verbosity >= LoggingVerbosity.Minimal)
                _context.Logger.LogInformation($"Step {_context.StepInfo?.Name} - WebSocket Closed: {_url} - CorrelationId: {_context.Info.CorrelationId}");
        }
    }

    private async Task ReceiveLoop(CancellationToken cancellationToken)
    {
        var buffer = new byte[_receiveBufferSize];
        var messageBuffer = new List<byte>();

        try
        {
            while (!cancellationToken.IsCancellationRequested && _webSocket!.State == WebSocketState.Open)
            {
                WebSocketReceiveResult result;
                try
                {
                    result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    if (_verbosity >= LoggingVerbosity.Minimal)
                        _context.Logger.LogInformation($"Step {_context.StepInfo?.Name} - WebSocket received close message - CorrelationId: {_context.Info.CorrelationId}");
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    messageBuffer.AddRange(buffer.AsSpan(0, result.Count).ToArray());
                    if (result.EndOfMessage)
                    {
                        var message = Encoding.UTF8.GetString(messageBuffer.ToArray());
                        messageBuffer.Clear();

                        if (_verbosity == LoggingVerbosity.Full)
                            _context.Logger.LogInformation($"Step {_context.StepInfo?.Name} - WebSocket Received: {message} - CorrelationId: {_context.Info.CorrelationId}");

                        lock (_messagesLock)
                        {
                            if (_maxBufferedMessages > 0 && _receivedMessages.Count >= _maxBufferedMessages)
                                _receivedMessages.RemoveAt(0);
                            _receivedMessages.Add(message);
                        }

                        OnMessageReceived?.Invoke(this, message);
                    }
                }
                else if (result.MessageType == WebSocketMessageType.Binary)
                {
                    if (_verbosity >= LoggingVerbosity.Minimal)
                        _context.Logger.LogInformation($"Step {_context.StepInfo?.Name} - WebSocket Received binary message ({result.Count} bytes) - CorrelationId: {_context.Info.CorrelationId}");
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (WebSocketException ex) when (
            ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely ||
            ex.WebSocketErrorCode == WebSocketError.InvalidState)
        {
            if (_verbosity > LoggingVerbosity.None)
                _context.Logger.LogInformation($"WebSocket connection closed: {ex.Message}");
        }
        catch (Exception ex)
        {
            if (_verbosity > LoggingVerbosity.None)
                _context.Logger.LogError(ex, "Error in WebSocket receive loop");
        }
    }

    /// <summary>
    /// Disposes the WebSocket connection and releases all resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        try
        {
            // Only attempt graceful close if socket still usable
            if (_webSocket != null &&
                (_webSocket.State == WebSocketState.Open || _webSocket.State == WebSocketState.CloseReceived))
                Close().GetAwaiter().GetResult();
        }
        catch { }
        finally
        {
            _receiveCts?.Dispose();
            _webSocket?.Dispose();
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Asynchronously disposes the WebSocket connection and releases all resources.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;
        _disposed = true;

        try
        {
            if (_webSocket != null &&
                (_webSocket.State == WebSocketState.Open || _webSocket.State == WebSocketState.CloseReceived))
                await Close();
        }
        catch { }
        finally
        {
            _receiveCts?.Dispose();
            _webSocket?.Dispose();
        }

        GC.SuppressFinalize(this);
    }
}
