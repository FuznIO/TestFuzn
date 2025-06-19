using Microsoft.Extensions.Logging;

namespace TestFusion.Internals.Logging;

internal class FileLogger : ILogger
{
    private readonly StreamWriter _writer;
    private readonly string _categoryName;

    public FileLogger(StreamWriter writer, string categoryName)
    {
        _writer = writer;
        _categoryName = categoryName;
    }

    public IDisposable BeginScope<TState>(TState state) => default!;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId,
        TState state, Exception exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
            return;

        var logRecord = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{logLevel}] {_categoryName} {formatter(state, exception)}";
        _writer.WriteLine(logRecord);

        if (exception != null)
        {
            _writer.WriteLine(exception);
        }
    }
}
