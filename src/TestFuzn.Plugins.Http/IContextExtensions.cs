using Fuzn.FluentHttp;
using Fuzn.TestFuzn.Plugins.Http.Internals;

namespace Fuzn.TestFuzn.Plugins.Http;

/// <summary>
/// Extension methods for <see cref="Context"/> to create HTTP clients.
/// </summary>
public static class ContextExtensions
{
    /// <summary>
    /// Creates an HTTP client configured with TestFuzn logging and correlation ID injection.
    /// Use FluentHttp's fluent API to build and send requests.
    /// </summary>
    /// <param name="context">The step context.</param>
    /// <returns>An <see cref="HttpClient"/> configured for TestFuzn.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the HTTP plugin has not been initialized.</exception>
    /// <example>
    /// <code>
    /// var response = await context.CreateHttpClient()
    ///     .Url("https://api.example.com/users")
    ///     .WithContent(new { Name = "John" })
    ///     .Post&lt;User&gt;();
    /// </code>
    /// </example>
    public static HttpClient CreateHttpClient(this Context context)
    {
        if (!HttpGlobalState.HasBeenInitialized)
            throw new InvalidOperationException("TestFuzn.Plugins.HTTP has not been initialized. Please call configuration.UseHttp() in the Startup.");

        // Set context for the current async flow so the logging handler can access it
        TestFuznHttpContext.Current = context;

        var httpClientFactory = HttpGlobalState.Configuration.CustomHttpClientFactory 
            ?? context.Internals.Plugins.GetState<IHttpClientFactory>(typeof(HttpPlugin));
        
        return httpClientFactory.CreateClient(HttpClientNames.TestFuzn);
    }

    /// <summary>
    /// Creates an HTTP client and starts building a request for the specified URL.
    /// This is a convenience method combining CreateHttpClient() and Url().
    /// </summary>
    /// <param name="context">The step context.</param>
    /// <param name="url">The target URL for the HTTP request.</param>
    /// <returns>A FluentHttp request builder.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the HTTP plugin has not been initialized.</exception>
    /// <example>
    /// <code>
    /// var response = await context.CreateRequest("https://api.example.com/users/1")
    ///     .Get&lt;User&gt;();
    /// </code>
    /// </example>
    public static Fuzn.FluentHttp.HttpRequestBuilder CreateRequest(this Context context, string url)
    {
        return context.CreateHttpClient().Url(url);
    }
}
