using Fuzn.FluentHttp;

namespace Fuzn.TestFuzn.Plugins.Http.Internals;

internal class TestFuznHttpClient : IHttpClient 
{
    public HttpClient HttpClient { get; }

    public TestFuznHttpClient(HttpClient httpClient)
    {
        HttpClient = httpClient;
    }

    public FluentHttpRequest CreateHttpRequest()
    {
        return HttpClient.Request();
    }
}