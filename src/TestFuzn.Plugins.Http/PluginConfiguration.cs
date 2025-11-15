namespace Fuzn.TestFuzn.Plugins.Http;

public class PluginConfiguration
{
    public TimeSpan DefaultRequestTimeout { get; set; } = TimeSpan.FromSeconds(10);
    public bool LogFailedRequestsToTestConsole { get; set; }
    public IHttpClientFactory? CustomHttpClientFactory { get; set; }
    public string CorrelationIdHeaderName { get; set; } = "X-Correlation-ID";
}
