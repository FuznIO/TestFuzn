using Fuzn.TestFuzn.Plugins.Http.Internals;

namespace Fuzn.TestFuzn.Plugins.Http;

/// <summary>
/// Extension methods for <see cref="Context"/> to create HTTP requests.
/// </summary>
public static class IContextExtensions
{
    /// <summary>
    /// Creates a new HTTP request builder for the specified URL.
    /// </summary>
    /// <param name="context">The step context.</param>
    /// <param name="url">The target URL for the HTTP request.</param>
    /// <returns>An <see cref="HttpRequestBuilder"/> instance for building the request.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the HTTP plugin has not been initialized.</exception>
    public static HttpRequestBuilder CreateHttpRequest(this Context context, string url)
    {
        if (!HttpGlobalState.HasBeenInitialized)
            throw new InvalidOperationException("TestFuzn.Plugins.HTTP has not been initialized. Please call configuration.UseHttp() in the Startup.");

        return new HttpRequestBuilder(context, url);
    }
}
