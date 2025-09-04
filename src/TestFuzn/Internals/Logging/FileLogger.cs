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
    /// Begin a new logging scope (not implemented)
    /// </summary>
    public IDisposable BeginScope<TState>(TState state) => default!;

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
                string logRecord = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{logLevel}] {_categoryName} {message}";

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
}
