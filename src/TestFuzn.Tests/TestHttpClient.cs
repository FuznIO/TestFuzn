using Fuzn.FluentHttp;
using Fuzn.TestFuzn.Plugins.Http;

namespace Fuzn.TestFuzn.Tests;

/// <summary>
/// A typed HTTP client for TestFuzn tests with usage tracking.
/// Used to verify that typed HTTP clients work correctly with TestFuzn.
/// </summary>
public class TestHttpClient : IHttpClient
{
    private static int _usageCount;

    public static int UsageCount => _usageCount;

    public HttpClient HttpClient { get; }

    public TestHttpClient(HttpClient httpClient)
    {
        HttpClient = httpClient;
    }

    public FluentHttpRequest CreateHttpRequest()
    {
        Interlocked.Increment(ref _usageCount);
        return HttpClient.Request();
    }

    public static void ResetUsageCount() => Interlocked.Exchange(ref _usageCount, 0);
}
