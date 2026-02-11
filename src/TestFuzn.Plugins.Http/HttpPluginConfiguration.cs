using Fuzn.TestFuzn.Plugins.Http.Internals;
using Microsoft.Extensions.DependencyInjection;

namespace Fuzn.TestFuzn.Plugins.Http;

/// <summary>
/// Configuration options for the HTTP plugin.
/// </summary>
public class HttpPluginConfiguration
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
    /// Gets or sets a value indicating whether HTTP request/response details should be written to the test console when a step fails.
    /// This is useful for debugging failed tests, regardless of whether the HTTP response was successful.
    /// Only applies to standard tests, not load tests. Defaults to true.
    /// </summary>
    public bool WriteHttpDetailsToConsoleOnStepFailure { get; set; } = true;

    /// <summary>
    /// Gets or sets the header name used for correlation IDs. Defaults to "X-Correlation-ID".
    /// </summary>
    public string CorrelationIdHeaderName { get; set; } = "X-Correlation-ID";

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
