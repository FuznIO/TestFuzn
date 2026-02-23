using Fuzn.FluentHttp;

namespace Fuzn.TestFuzn.Plugins.Http;

/// <summary>
/// The built-in default HTTP client provided by the HTTP plugin.
/// This client is automatically registered and used as the default when no custom HTTP client is configured.
/// To override, call <c>httpConfig.DefaultHttpClient&lt;THttpClient&gt;()</c> in the <c>UseHttp()</c> configuration.
/// </summary>
public class TestFuznHttpClient : IHttpClient
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
