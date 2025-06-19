using TestFusion.InfluxDB;

namespace TestFusion.BrowserTesting;

public static class TestFusionConfigurationExtensions
{
    public static void UseInfluxDbSink(this TestFusionConfiguration config)
    {
        var influxDbSinkConfig = new InfluxDbSinkConfiguration();
        ConfigurationManager.LoadPluginConfiguration("InfluxDBSink", influxDbSinkConfig);
        if (influxDbSinkConfig is null)
            throw new InvalidOperationException("InfluxDbSink configuration is not set in appsettings.json");

        config.AddSinkPlugin(new InfluxDbSink(influxDbSinkConfig));
    }

    public static void UseInfluxDbSink(this TestFusionConfiguration config, 
        Action<InfluxDbSinkConfiguration> influxDbSinkConfigAction)
    {
        if (influxDbSinkConfigAction is null)
            throw new ArgumentNullException(nameof(influxDbSinkConfigAction));

        var influxDbSinkConfig = new InfluxDbSinkConfiguration();
        influxDbSinkConfigAction(influxDbSinkConfig);

        config.AddSinkPlugin(new InfluxDbSink(influxDbSinkConfig));
    }
}
