using TestFusion.Internals.State;

internal static class LoggerFactory
{
    private static readonly ILoggerFactory _loggerFactory;

    static LoggerFactory()
    {
        var logsPath = GlobalState.TestsOutputDirectory;
        Directory.CreateDirectory(logsPath);

        _loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
        {
            builder
                //.AddConsole() // Uncomment if you also want console output
                .AddProvider(new FileLoggerProvider(Path.Combine(logsPath, "TestFusion_Log.log")))
                .SetMinimumLevel(LogLevel.Debug);
        });
    }

    public static ILogger<T> CreateLogger<T>() => _loggerFactory.CreateLogger<T>();

    public static ILogger CreateLogger() => _loggerFactory.CreateLogger("TestFusion");
}

