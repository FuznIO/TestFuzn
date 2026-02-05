using Fuzn.FluentHttp;
using Fuzn.TestFuzn.Plugins.Http.Internals;
using Microsoft.Extensions.DependencyInjection;

namespace Fuzn.TestFuzn.Plugins.Http;

/// <summary>
/// Extension methods for <see cref="Context"/> to create HTTP clients.
/// </summary>
public static class ContextExtensions
{
    /// <summary>
    /// Creates an HTTP request builder for the specified URL using a named HTTP client.
    /// </summary>
    /// <param name="context">The step context.</param>
    /// <param name="url">The target URL for the HTTP request.</param>
    /// <param name="httpClientName">The name of the HTTP client to use. Defaults to "TestFuzn".</param>
    /// <returns>A <see cref="HttpRequestBuilder"/> for building and executing the HTTP request.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the HTTP plugin has not been initialized. Call <c>configuration.UseHttp()</c> in the startup.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="httpClientName"/> is null or whitespace.</exception>
    /// <example>
    /// <code>
    /// var response = await context.CreateHttpRequest("https://api.example.com/users/1")
    ///     .Get&lt;User&gt;();
    /// </code>
    /// </example>
    public static FluentHttpRequest CreateHttpRequest(this Context context, string url, string httpClientName = "TestFuzn")
    {
        if (!HttpGlobalState.HasBeenInitialized)
            throw new InvalidOperationException("TestFuzn.Plugins.HTTP has not been initialized. Please call configuration.UseHttp() in the Startup.");

        if (string.IsNullOrWhiteSpace(httpClientName))
            throw new ArgumentException("HTTP client name must be provided. Default is 'TestFuzn'.", nameof(httpClientName));

        var httpClientFactory = context.Services.GetRequiredService<IHttpClientFactory>();
        var httpClient = httpClientFactory.CreateClient(httpClientName);

        var fluentHttpRequest = httpClient
            .Url(url)
            .WithOption("TestFuznContext", context);

        return fluentHttpRequest;
    }
}
