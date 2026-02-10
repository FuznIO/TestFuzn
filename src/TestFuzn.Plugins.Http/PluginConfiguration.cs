using Fuzn.TestFuzn.Plugins.Http.Internals;
using Microsoft.Extensions.DependencyInjection;

namespace Fuzn.TestFuzn.Plugins.Http;

/// <summary>
/// Configuration options for the HTTP plugin.
/// </summary>
public class PluginConfiguration
{
    internal Type DefaultHttpClient { get; set; } = typeof(TestFuznHttpClient);

    /// <summary>
    /// Gets the collection of service descriptors for dependency injection configuration.
    /// </summary>
    public IServiceCollection Services { get; internal set; }

    /// <summary>
    /// Gets or sets the default base address used for HTTP requests.
    /// </summary>
    public Uri? DefaultBaseAddress { get; set; } = null;

    /// <summary>
    /// Gets or sets the default timeout for HTTP requests. Defaults to 10 seconds.
    /// </summary>
    public TimeSpan DefaultRequestTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Gets or sets the DefaultAllowAutoRedirect property for the underlying HttpClientHandler. Defaults to false to prevent unintended redirects during testing.
    /// </summary>
    public bool DefaultAllowAutoRedirect { get; set; } = false;

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

    /// <summary>
    /// Sets the default HTTP client implementation to use.
    /// </summary>
    /// <typeparam name="THttpClient"></typeparam>
    public void UseDefaultHttpClient<THttpClient>()
        where THttpClient : IHttpClient
    {
        DefaultHttpClient = typeof(THttpClient);
    }
}
