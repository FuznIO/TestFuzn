namespace Fuzn.TestFuzn.Plugins.Http.Internals;

internal static class HttpRequestOptionsExtensions
{
    public static Context GetTestFuznContext(this HttpRequestOptions options)
    {
        if (!options.TryGetValue(HttpPluginConstants.ContextOptionName, out var context))
            throw new InvalidOperationException("TestFuzn context is not available in the HttpRequestOptions. Ensure that the HttpRequestMessage was created using the TestFuzn HttpPlugin.");

        return context;
    }

    public static HttpPluginState? GetTestFuznState(this HttpRequestOptions options)
    {
        if (options.TryGetValue(HttpPluginConstants.StateOptionName, out var state))
            return state;

        return null;
    }
}
