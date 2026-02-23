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
    /// Creates an HTTP request builder for the specified URL using the default HTTP client.
    /// </summary>
    /// <param name="context">The step context.</param>
    /// <param name="url">The target URL for the HTTP request.</param>
    /// <returns>A <see cref="FluentHttpRequest"/> for building and executing the HTTP request.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the HTTP plugin has not been initialized (call <c>configuration.UseHttp()</c> in the startup),
    /// or when no default HTTP client has been configured (call <c>httpConfig.UseDefaultHttpClient&lt;THttpClient&gt;()</c>).
    /// </exception>
    /// <example>
    /// <code>
    /// var response = await context.CreateHttpRequest("https://api.example.com/users/1")
    ///     .Get&lt;User&gt;();
    /// </code>
    /// </example>
    public static FluentHttpRequest CreateHttpRequest(this Context context, string url)
    {
        if (!HttpGlobalState.HasBeenInitialized)
            throw new InvalidOperationException("TestFuzn.Plugins.HTTP has not been initialized. Please call configuration.UseHttp() in the Startup.");

        if (HttpGlobalState.Configuration.DefaultHttpClientInternal == null)
            throw new InvalidOperationException(
                "No default HTTP client has been configured. " +
                "Either call httpConfig.DefaultHttpClient<THttpClient>() in UseHttp() configuration, " +
                "or use context.CreateHttpRequest<THttpClient>(url) to specify the HTTP client type explicitly.");

        return CreateHttpRequest(HttpGlobalState.Configuration.DefaultHttpClientInternal, context, url);
    }

    /// <summary>
    /// Creates a new fluent HTTP request for the specified URL using the given HTTP client type.
    /// </summary>
    /// <typeparam name="THttpClient">The type of HTTP client to use for the request. Must implement the IHttpClient interface.</typeparam>
    /// <param name="context">The context in which to create the HTTP request. Cannot be null.</param>
    /// <param name="url">The URL to which the HTTP request will be sent. Cannot be null or empty.</param>
    /// <returns>A FluentHttpRequest instance configured for the specified URL and HTTP client type.</returns>
    public static FluentHttpRequest CreateHttpRequest<THttpClient>(this Context context, string url)
        where THttpClient : IHttpClient
    {
        return CreateHttpRequest(typeof(THttpClient), context, url);
    }

    private static FluentHttpRequest CreateHttpRequest(Type httpClientType, Context context, string url)
    {
        if (!HttpGlobalState.HasBeenInitialized)
            throw new InvalidOperationException("TestFuzn.Plugins.HTTP has not been initialized. Please call configuration.UseHttp() in the Startup.");

        var httpClientInstance = context.Services.GetRequiredService(httpClientType);
        if (httpClientInstance is not IHttpClient httpClient)
            throw new InvalidOperationException($"The specified HTTP client type '{httpClientType.FullName}' does not implement IHttpClient.");

        // Get the HTTP plugin state for this context
        var httpPluginState = context.Internals.Plugins.GetState(typeof(HttpPlugin)) as HttpPluginState;

        var fluentHttpRequest = httpClient
                                    .CreateHttpRequest()
                                    .WithUrl(url)
                                    .WithOption(HttpPluginConstants.ContextOptionName.Key, context);

        // Pass the state to the request if available
        if (httpPluginState != null)
        {
            fluentHttpRequest.WithOption(HttpPluginConstants.StateOptionName.Key, httpPluginState);
        }

        return fluentHttpRequest;
    }
}
