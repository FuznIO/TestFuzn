namespace FuznLabs.TestFuzn.Plugins.Http;

public class PluginConfiguration
{
    public TimeSpan DefaultRequestTimeout { get; set; } = TimeSpan.FromSeconds(10);
    public bool LogFailedRequestsToTestConsole { get; set; }
    public IHttpClientFactory? CustomHttpClientFactory { get; set; }
}
