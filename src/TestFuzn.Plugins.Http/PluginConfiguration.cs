namespace Fuzn.TestFuzn.Plugins.Http;

/// <summary>
/// Configuration options for the HTTP plugin.
/// </summary>
public class PluginConfiguration
{
    /// <summary>
    /// Gets or sets the default timeout for HTTP requests. Defaults to 10 seconds.
    /// </summary>
    public TimeSpan DefaultRequestTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Gets or sets a value indicating whether failed requests should be logged to the test console.
    /// </summary>
    public bool LogFailedRequestsToTestConsole { get; set; }

    /// <summary>
    /// Gets or sets the header name used for correlation IDs. Defaults to "X-Correlation-ID".
    /// </summary>
    public string CorrelationIdHeaderName { get; set; } = "X-Correlation-ID";

    /// <summary>
    /// Gets or sets the logging verbosity level. Defaults to <see cref="LoggingVerbosity.Normal"/>.
    /// </summary>
    public LoggingVerbosity LoggingVerbosity { get; set; } = LoggingVerbosity.Normal;
}
