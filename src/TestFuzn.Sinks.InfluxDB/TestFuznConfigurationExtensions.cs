using Fuzn.TestFuzn.Sinks.InfluxDB.Internals;

namespace Fuzn.TestFuzn.Sinks.InfluxDB;

/// <summary>
/// Extension methods for configuring the InfluxDB sink on <see cref="TestFuznConfiguration"/>.
/// </summary>
public static class TestFuznConfigurationExtensions
{
    /// <summary>
    /// Registers the InfluxDB sink using the "InfluxDB" section from the application configuration.
    /// </summary>
    /// <param name="config">The TestFuzn configuration to extend.</param>
    /// <exception cref="InvalidOperationException">Thrown when the "InfluxDB" configuration section is missing from appsettings.json.</exception>
    public static void UseInfluxDB(this TestFuznConfiguration config)
    {
        var influxDbSinkConfig = config.AppConfiguration.GetRequiredSection<InfluxDbSinkConfiguration>("InfluxDB");
        if (influxDbSinkConfig is null)
            throw new InvalidOperationException("InfluxDB configuration is not set in appsettings.json");

        config.AddSinkPlugin(new InfluxDbSink(influxDbSinkConfig));
    }

    /// <summary>
    /// Registers the InfluxDB sink using an inline configuration action.
    /// </summary>
    /// <param name="config">The TestFuzn configuration to extend.</param>
    /// <param name="influxDbSinkConfigAction">A delegate that configures the <see cref="InfluxDbSinkConfiguration"/> instance.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="influxDbSinkConfigAction"/> is <see langword="null"/>.</exception>
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
