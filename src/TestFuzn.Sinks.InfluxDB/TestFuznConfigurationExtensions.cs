namespace Fuzn.TestFuzn.Sinks.InfluxDB;

public static class TestFuznConfigurationExtensions
{
    public static void UseInfluxDb(this TestFuznConfiguration config)
    {
        var influxDbSinkConfig = ConfigurationManager.GetSection<InfluxDbSinkConfiguration>("InfluxDB");
        if (influxDbSinkConfig is null)
            throw new InvalidOperationException("InfluxDb configuration is not set in appsettings.json");

        config.AddSinkPlugin(new InfluxDbSink(influxDbSinkConfig));
    }

    public static void UseInfluxDB(this TestFuznConfiguration config, 
        Action<InfluxDbSinkConfiguration> influxDbSinkConfigAction)
    {
        if (influxDbSinkConfigAction is null)
            throw new ArgumentNullException(nameof(influxDbSinkConfigAction));

        var influxDbSinkConfig = new InfluxDbSinkConfiguration();
        influxDbSinkConfigAction(influxDbSinkConfig);

        config.AddSinkPlugin(new InfluxDbSink(influxDbSinkConfig));
    }
}
