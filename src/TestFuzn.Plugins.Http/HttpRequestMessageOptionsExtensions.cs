using Fuzn.TestFuzn.Plugins.Http.Internals;

namespace Fuzn.TestFuzn.Plugins.Http;

/// <summary>
/// Extension methods for <see cref="HttpRequestOptions"/> to retrieve TestFuzn-specific options.
/// </summary>
public static class HttpRequestOptionsExtensions
{
    /// <summary>
    /// Gets the <see cref="Context"/> associated with the HTTP request.
    /// </summary>
    /// <param name="options">The HTTP request options.</param>
    /// <returns>The <see cref="Context"/> associated with the request.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the TestFuzn context is not available in the request options.
    /// This occurs when the <see cref="HttpRequestMessage"/> was not created using the TestFuzn HTTP plugin.
    /// </exception>
    public static Context GetTestFuznContext(this HttpRequestOptions options)
    {
        if (!options.TryGetValue(HttpPluginConstants.ContextOptionName, out var context))
            throw new InvalidOperationException("TestFuzn context is not available in the HttpRequestOptions. Ensure that the HttpRequestMessage was created using the TestFuzn HttpPlugin.");

        return context;
    }

    internal static HttpPluginState? GetTestFuznState(this HttpRequestOptions options)
    {
        if (options.TryGetValue(HttpPluginConstants.StateOptionName, out var state))
            return state;

        return null;
    }
}
