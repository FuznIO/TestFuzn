using Fuzn.TestFuzn.Plugins.Http.Internals;

namespace Fuzn.TestFuzn.Plugins.Http;

public static class IContextExtensions
{
    public static HttpRequestBuilder CreateHttpRequest(this Context context, string url)
    {
        if (!HttpGlobalState.HasBeenInitialized)
            throw new InvalidOperationException("TestFuzn has not been initialized. Please call configuration.UseHttpTesting() in the Startup.");

        return new HttpRequestBuilder(context, url);
    }
}
