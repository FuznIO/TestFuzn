namespace TestFusion.Sinks.InfluxDB;

public static class TestFusionConfigurationExtensions
{
    public static void UseInfluxDb(this TestFusionConfiguration config)
    {
        var influxDbSinkConfig = new InfluxDbSinkConfiguration();
        ConfigurationManager.LoadPluginConfiguration("InfluxDB", influxDbSinkConfig);
        if (influxDbSinkConfig is null)
            throw new InvalidOperationException("InfluxDb configuration is not set in appsettings.json");

        config.AddSinkPlugin(new InfluxDbSink(influxDbSinkConfig));
    }

    public static void UseInfluxDB(this TestFusionConfiguration config, 
        Action<InfluxDbSinkConfiguration> influxDbSinkConfigAction)
    {
        if (influxDbSinkConfigAction is null)
            throw new ArgumentNullException(nameof(influxDbSinkConfigAction));

        var influxDbSinkConfig = new InfluxDbSinkConfiguration();
        influxDbSinkConfigAction(influxDbSinkConfig);

        config.AddSinkPlugin(new InfluxDbSink(influxDbSinkConfig));
    }
}
