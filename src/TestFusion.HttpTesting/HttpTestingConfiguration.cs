namespace TestFusion.HttpTesting;

public class HttpTestingConfiguration
{
    public TimeSpan HttpClientTimeout { get; set; } = TimeSpan.FromSeconds(5);
    public bool LogFailedRequestsToTestConsole { get; set; }
    public IHttpClientFactory? CustomHttpClientFactory { get; set; }
}