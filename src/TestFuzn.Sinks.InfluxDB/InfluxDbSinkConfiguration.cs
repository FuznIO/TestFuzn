namespace FuznLabs.TestFuzn.Sinks.InfluxDB;

public class InfluxDbSinkConfiguration
{
    public string Url { get; set; }
    public string Token { get; set; }
    public string Bucket { get; set; }
    public string Org { get; set; }
}
