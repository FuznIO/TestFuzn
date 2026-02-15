using Fuzn.FluentHttp;
using Fuzn.TestFuzn.Plugins.Http;

namespace SampleApp.Tests;

/// <summary>
/// Default HTTP client for SampleApp tests.
/// </summary>
public class SampleAppHttpClient : IHttpClient
{
    public HttpClient HttpClient { get; }

    public SampleAppHttpClient(HttpClient httpClient)
    {
        HttpClient = httpClient;
    }

    public FluentHttpRequest CreateHttpRequest()
    {
        return HttpClient.Request();
    }
}
