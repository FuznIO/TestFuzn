using TestFusion.HttpTesting.Internals;

namespace TestFusion.HttpTesting;

public static class IContextExtensions
{
    public static HttpRequestBuilder CreateHttpRequest(this Context context, string url)
    {
        if (!GlobalState.HasBeenInitialized)
            throw new InvalidOperationException("TestFusion has not been initialized. Please call configuration.UseHttpTesting() in the Startup.");

        return new HttpRequestBuilder(context, url);
    }
}
