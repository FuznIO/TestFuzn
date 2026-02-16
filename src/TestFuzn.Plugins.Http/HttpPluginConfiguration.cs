using Microsoft.Extensions.DependencyInjection;

namespace Fuzn.TestFuzn.Plugins.Http;

/// <summary>
/// Configuration options for the HTTP plugin.
/// </summary>
public class HttpPluginConfiguration
{
    internal Type? DefaultHttpClientInternal { get; set; } = null;

    /// <summary>
    /// Gets the collection of service descriptors for dependency injection configuration.
    /// </summary>
    public IServiceCollection Services { get; internal set; }

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
    /// Configures the default HTTP client implementation to use for <c>context.CreateHttpRequest()</c> calls.
    /// The HTTP client must be registered with <c>services.AddHttpClient&lt;T&gt;().AddTestFuznHandlers()</c>.
    /// </summary>
    /// <typeparam name="THttpClient">The type of HTTP client to use as the default.</typeparam>
    public void DefaultHttpClient<THttpClient>()
        where THttpClient : IHttpClient
    {
        DefaultHttpClientInternal = typeof(THttpClient);
    }
}
