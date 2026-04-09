namespace Fuzn.TestFuzn.Internals.Logging;

/// <summary>
/// File-based logger implementation with thread-safety and error handling
/// </summary>
internal class FileLogger : ILogger
{
    private readonly StreamWriter _writer;
    private readonly string _categoryName;
    private readonly object _lock;

    // Thread-safe scope storage using AsyncLocal
    private static readonly AsyncLocal<LoggerScope?> _currentScope = new();

    /// <summary>
    /// Initializes a new instance of the FileLogger
    /// </summary>
    /// <param name="writer">StreamWriter to use for logging</param>
    /// <param name="categoryName">Category name for the logger</param>
    /// <param name="lock">Synchronization object for thread safety</param>
    public FileLogger(StreamWriter writer, string categoryName, object @lock)
    {
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        _categoryName = categoryName ?? throw new ArgumentNullException(nameof(categoryName));
        _lock = @lock ?? throw new ArgumentNullException(nameof(@lock));
    }

    /// <summary>
    /// Begin a new logging scope with contextual information
    /// </summary>
    public IDisposable BeginScope<TState>(TState state)
    {
        if (state == null)
            return new NoOpDisposable();

        var scope = new LoggerScope(state, _currentScope.Value);
        _currentScope.Value = scope;
        return scope;
    }

    /// <summary>
    /// Checks if logging is enabled for the specified level
    /// </summary>
    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

    /// <summary>
    /// Logs a message with thread safety and retry mechanism
    /// </summary>
    public void Log<TState>(LogLevel logLevel, EventId eventId,
        TState state, Exception exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel) || formatter == null)
            return;

        try
        {
            string message = formatter(state, exception);
            var (scenario, step) = GetScopeContext();
            string logRecord = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{logLevel}] [{scenario}] [Step {step}] {_categoryName} {message}";

            lock (_lock)
            {
                _writer.WriteLine(logRecord);

                if (exception != null)
                {
                    _writer.WriteLine($"Exception: {exception.Message}");
                    _writer.WriteLine($"StackTrace: {exception.StackTrace}");

                    var innerException = exception.InnerException;
                    while (innerException != null)
                    {
                        _writer.WriteLine($"Inner Exception: {innerException.Message}");
                        innerException = innerException.InnerException;
                    }
                }
            }
        }
        catch (ObjectDisposedException)
        {
            Console.Error.WriteLine($"Cannot write to log - writer was disposed: [{logLevel}] {_categoryName}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Unexpected error during logging: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets scenario and step context from the current scope chain
    /// </summary>
    private (string scenario, string step) GetScopeContext()
    {
        var scope = _currentScope.Value;
        if (scope == null)
            return (string.Empty, string.Empty);

        // Fast path: check for LoggingScopeState to avoid dictionary allocations
        while (scope != null)
        {
            if (scope.State is LoggingScopeState scopeState)
                return (scopeState.Scenario, scopeState.Step.ToString());

            scope = scope.Parent;
        }

        return (string.Empty, string.Empty);
    }

    /// <summary>
    /// Represents a logging scope that can be nested
    /// </summary>
    private class LoggerScope : IDisposable
    {
        private readonly object _state;
        private readonly LoggerScope? _parent;
        private bool _disposed;

        public LoggerScope? Parent => _parent;
        public object State => _state;

        public LoggerScope(object state, LoggerScope? parent)
        {
            _state = state;
            _parent = parent;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                // Restore parent scope
                _currentScope.Value = _parent;
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// No-op disposable for when no scope is needed
    /// </summary>
    private class NoOpDisposable : IDisposable
    {
        public void Dispose() { }
    }
}
