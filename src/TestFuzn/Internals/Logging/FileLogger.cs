namespace Fuzn.TestFuzn.Internals.Logging;

/// <summary>
/// File-based logger implementation with thread-safety and error handling
/// </summary>
internal class FileLogger : ILogger
{
    private readonly StreamWriter _writer;
    private readonly string _categoryName;
    private readonly object _lock;
    private const int MaxRetryAttempts = 3;
    private const int RetryDelayMs = 100;

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

        int attempt = 0;
        bool success = false;

        while (!success && attempt < MaxRetryAttempts)
        {
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

                    _writer.Flush();
                    success = true;
                }
            }
            catch (IOException)
            {
                // File might be locked, retry after delay
                attempt++;
                if (attempt < MaxRetryAttempts)
                {
                    Thread.Sleep(RetryDelayMs);
                }
                else
                {
                    // Log to console if all retries fail
                    Console.Error.WriteLine($"Failed to write to log after {MaxRetryAttempts} attempts: [{logLevel}] {_categoryName}");
                }
            }
            catch (ObjectDisposedException)
            {
                // Writer was disposed, log to console
                Console.Error.WriteLine($"Cannot write to log - writer was disposed: [{logLevel}] {_categoryName}");
                break;
            }
            catch (Exception ex)
            {
                // Unexpected error, log to console
                Console.Error.WriteLine($"Unexpected error during logging: {ex.Message}");
                Console.Error.WriteLine(ex.StackTrace);
                break;
            }
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

        string? scenario = null;
        string? step = null;

        // Walk the scope chain to find scenario and step values
        while (scope != null)
        {
            var scopeValues = scope.GetScopeValues();
            
            if (scenario == null && scopeValues.TryGetValue("scenario", out var scenarioValue))
                scenario = scenarioValue?.ToString();
            
            if (step == null && scopeValues.TryGetValue("step", out var stepValue))
                step = stepValue?.ToString();
            
            // If we found both, no need to continue
            if (scenario != null && step != null)
                break;
            
            scope = scope.Parent;
        }

        return (scenario ?? string.Empty, step ?? string.Empty);
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

        public LoggerScope(object state, LoggerScope? parent)
        {
            _state = state;
            _parent = parent;
        }

        /// <summary>
        /// Gets scope values as a dictionary
        /// </summary>
        public Dictionary<string, object?> GetScopeValues()
        {
            var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

            if (_state == null)
                return values;

            // Handle Dictionary<string, object?> which is commonly used for structured scopes
            if (_state is IEnumerable<KeyValuePair<string, object?>> kvps)
            {
                foreach (var kvp in kvps)
                {
                    values[kvp.Key] = kvp.Value;
                }
            }

            return values;
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
