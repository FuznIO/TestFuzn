using Fuzn.FluentHttp;

namespace Fuzn.TestFuzn.Plugins.Http;

public interface IHttpClient
{
    public HttpClient HttpClient { get; }
    public FluentHttpRequest CreateHttpRequest();
}
