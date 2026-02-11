using Fuzn.FluentHttp;
using Fuzn.TestFuzn.Plugins.Http;

namespace Fuzn.TestFuzn.Tests.CustomHttpClient;


public class CustomTestHttpClient : IHttpClient
{
    private static int _usageCount;

    public static int UsageCount => _usageCount;

    public HttpClient HttpClient { get; }

    public CustomTestHttpClient(HttpClient httpClient)
    {
        HttpClient = httpClient;
    }

    public FluentHttpRequest CreateHttpRequest()
    {
        Interlocked.Increment(ref _usageCount);
        return HttpClient.Request();
    }
}
