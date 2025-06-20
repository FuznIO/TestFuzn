using TestFusion.Plugins.Http.Internals;

namespace TestFusion.Plugins.Http;

public static class IContextExtensions
{
    public static HttpRequestBuilder CreateHttpRequest(this Context context, string url)
    {
        if (!GlobalState.HasBeenInitialized)
            throw new InvalidOperationException("TestFusion has not been initialized. Please call configuration.UseHttpTesting() in the Startup.");

        return new HttpRequestBuilder(context, url);
    }
}
