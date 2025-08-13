using System.Text;

namespace TestFusion.Internals.Logging;

/// <summary>
/// Provides file-based logging functionality with enhanced reliability
/// </summary>
internal class FileLoggerProvider : ILoggerProvider, IDisposable
{
    private readonly string _path;
    private readonly StreamWriter _writer;
    private readonly object _lock = new object();
    private bool _disposed;
    private const int MaxRetryAttempts = 3;
    private const int RetryDelayMs = 100;

    /// <summary>
    /// Initializes a new instance of the FileLoggerProvider
    /// </summary>
    /// <param name="path">Path to the log file</param>
    public FileLoggerProvider(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Log file path cannot be null or empty", nameof(path));

        _path = path;
        
        try
        {
            // Ensure directory exists
            string? directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Open file with more robust settings
            _writer = new StreamWriter(new FileStream(_path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite), Encoding.UTF8)
            {
                AutoFlush = true
            };

            // Log provider initialization
            WriteDirectly($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [Information] FileLoggerProvider initialized for: {_path}");
        }
        catch (Exception ex)
        {
            // If we can't create the log file, at least output to console
            Console.Error.WriteLine($"Failed to initialize FileLoggerProvider: {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);

            // Re-throw to ensure caller knows initialization failed
            throw;
        }
    }

    /// <summary>
    /// Creates a logger instance with the specified category name
    /// </summary>
    public ILogger CreateLogger(string categoryName)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(FileLoggerProvider));

        return new FileLogger(_writer, categoryName, _lock);
    }

    /// <summary>
    /// Writes directly to the log file with retries
    /// </summary>
    private void WriteDirectly(string message)
    {
        int attempt = 0;
        bool success = false;

        while (!success && attempt < MaxRetryAttempts)
        {
            try
            {
                lock (_lock)
                {
                    if (!_disposed)
                    {
                        _writer.WriteLine(message);
                        _writer.Flush();
                        success = true;
                    }
                }
            }
            catch (IOException)
            {
                // File might be temporarily locked, retry after delay
                attempt++;
                if (attempt < MaxRetryAttempts)
                {
                    Thread.Sleep(RetryDelayMs);
                }
                else
                {
                    Console.Error.WriteLine($"Failed to write to log file after {MaxRetryAttempts} attempts: {message}");
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Unexpected error writing to log file: {ex.Message}");
                break;
            }
        }
    }

    /// <summary>
    /// Disposes the StreamWriter
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes the StreamWriter with thread safety
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                try
                {
                    lock (_lock)
                    {
                        _writer.Flush();
                        _writer.Dispose();
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error disposing FileLoggerProvider: {ex.Message}");
                }
            }

            _disposed = true;
        }
    }
}
