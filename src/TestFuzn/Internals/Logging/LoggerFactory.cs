namespace Fuzn.TestFuzn.Internals.Logging;

/// <summary>
/// Factory for creating logger instances with fallback mechanisms
/// </summary>
internal static class LoggerFactory
{
    /// <summary>
    /// Creates a logger instance for the given output directory
    /// </summary>
    public static ILogger CreateLogger(string testsOutputDirectory, string categoryName = "TestFuzn")
    {
        try
        {
            var logsPath = testsOutputDirectory;
            if (string.IsNullOrEmpty(logsPath))
            {
                // Fallback to temp directory if output directory is not set
                logsPath = Path.Combine(Path.GetTempPath(), "TestFuzn_Logs", DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"));
                Console.WriteLine($"WARNING: TestsOutputDirectory not set, using temporary path: {logsPath}");
            }
            string? directory = Path.GetDirectoryName(logsPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
            Directory.CreateDirectory(logsPath);
            string logFilePath = Path.Combine(logsPath, "TestFuzn_Log.log");

            // Create the logger factory with file provider
            var loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
            {
                builder
                    .AddProvider(new FileLoggerProvider(logFilePath))
                    .SetMinimumLevel(LogLevel.Debug);
            });
            
            var logger = loggerFactory.CreateLogger(categoryName);
            logger.LogInformation("Logging system initialized successfully. Log file: {LogFilePath}", logFilePath);

            return logger;
        }
        catch (Exception ex)
        {
            // If logger factory initialization fails, create a minimal console-only logger
            Console.Error.WriteLine($"Failed to initialize LoggerFactory: {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
            
            var fallbackFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
            {
                builder
                    .AddConsole()
                    .SetMinimumLevel(LogLevel.Debug);
            });

            return fallbackFactory.CreateLogger(categoryName);
        }
    }
}

