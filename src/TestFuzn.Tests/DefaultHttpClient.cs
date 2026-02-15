using Fuzn.FluentHttp;
using Fuzn.TestFuzn.Plugins.Http;

namespace Fuzn.TestFuzn.Tests;

/// <summary>
/// Default HTTP client for TestFuzn tests.
/// </summary>
public class DefaultHttpClient : IHttpClient
{
    public HttpClient HttpClient { get; }

    public DefaultHttpClient(HttpClient httpClient)
    {
        HttpClient = httpClient;
    }

    public FluentHttpRequest CreateHttpRequest()
    {
        return HttpClient.Request();
    }
}
