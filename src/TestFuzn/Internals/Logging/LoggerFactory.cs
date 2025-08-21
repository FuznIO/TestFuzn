using FuznLabs.TestFuzn.Internals.State;

namespace FuznLabs.TestFuzn.Internals.Logging;

/// <summary>
/// Factory for creating logger instances with fallback mechanisms
/// </summary>
internal static class LoggerFactory
{
    private static readonly ILoggerFactory _loggerFactory;
    private static readonly bool _isInitialized;

    static LoggerFactory()
    {
        try
        {
            var logsPath = GlobalState.TestsOutputDirectory;
            if (string.IsNullOrEmpty(logsPath))
            {
                // Fallback to temp directory if output directory is not set
                logsPath = Path.Combine(Path.GetTempPath(), "TestFusion_Logs", DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"));
                Console.WriteLine($"WARNING: TestsOutputDirectory not set, using temporary path: {logsPath}");
            }
            string? directory = Path.GetDirectoryName(logsPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
            Directory.CreateDirectory(logsPath);
            string logFilePath = Path.Combine(logsPath, "TestFusion_Log.log");

            // Create the logger factory with both console and file providers for redundancy
            _loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
            {
                builder
                    .AddProvider(new FileLoggerProvider(logFilePath))
                    .SetMinimumLevel(LogLevel.Debug);
            });

            _isInitialized = true;
            
            // Log successful initialization
            var logger = _loggerFactory.CreateLogger("LoggerFactory");
            logger.LogInformation("Logging system initialized successfully. Log file: {LogFilePath}", logFilePath);
        }
        catch (Exception ex)
        {
            // If logger factory initialization fails, create a minimal console-only logger
            Console.Error.WriteLine($"Failed to initialize LoggerFactory: {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
            
            _loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
            {
                builder
                    .AddConsole()
                    .SetMinimumLevel(LogLevel.Debug);
            });
        }
    }

    /// <summary>
    /// Creates a typed logger instance
    /// </summary>
    public static ILogger<T> CreateLogger<T>() => _loggerFactory.CreateLogger<T>();

    /// <summary>
    /// Creates a logger instance with the specified name
    /// </summary>
    public static ILogger CreateLogger(string categoryName = "TestFusion") => 
        _loggerFactory.CreateLogger(categoryName);

    /// <summary>
    /// Indicates whether the logger factory was properly initialized with file logging
    /// </summary>
    public static bool IsFileLoggingInitialized => _isInitialized;
}

