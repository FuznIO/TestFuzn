using Fuzn.TestFuzn.Contracts.Plugins;
using Fuzn.TestFuzn.Contracts.Reports;
using Fuzn.TestFuzn.Contracts.Sinks;
using Microsoft.Extensions.DependencyInjection;

namespace Fuzn.TestFuzn;

/// <summary>
/// Configuration class for customizing TestFuzn behavior.
/// Use this class in your <see cref="IStartup"/> implementation to configure plugins, reports, and serialization.
/// </summary>
public class TestFuznConfiguration
{
    /// <summary>
    /// Gets or sets the test suite information including name, ID, and metadata.
    /// </summary>
    public SuiteInfo Suite { get; set; }
    /// <summary>
    /// Gets or sets the level of detail to include in log output. Defaults to <see cref="LoggingVerbosity.Full"/>.
    /// </summary>
    /// <remarks>Use this property to control how much information is written to the logs. Higher verbosity
    /// levels provide more detailed diagnostic information, which can be useful for troubleshooting but may produce
    /// larger log files.</remarks>
    public LoggingVerbosity LoggingVerbosity { get; set; } = LoggingVerbosity.Full;

    /// <summary>
    /// Gets or sets the target environment the tests are executing against (e.g., Dev, Test, Staging, Production).
    /// Set via TESTFUZN_TARGET_ENVIRONMENT environment variable or --target-environment argument.
    /// </summary>
    public string TargetEnvironment { get; internal set; }

    /// <summary>
    /// Gets or sets the execution environment where tests are running (e.g., Local, CI, CloudAgent).
    /// Set via TESTFUZN_EXECUTION_ENVIRONMENT environment variable or --execution-environment argument.
    /// Used for configuration loading, not for test filtering.
    /// </summary>
    public string ExecutionEnvironment { get; internal set; }

    /// <summary>
    /// Gets or sets the list of tags to include when filtering tests.
    /// Only tests with at least one of these tags will be executed.
    /// </summary>
    public List<string> TagsFilterInclude { get; internal set; } = [];

    /// <summary>
    /// Gets or sets the list of tags to exclude when filtering tests.
    /// Tests with any of these tags will be skipped.
    /// </summary>
    public List<string> TagsFilterExclude { get; internal set; } = [];

    /// <summary>
    /// Gets the service collection for registering dependencies.
    /// Plugins and user code can add services here during configuration.
    /// </summary>
    public IServiceCollection Services { get; }

    internal List<IContextPlugin> ContextPlugins { get; set; } = new();
    /// <summary>
    /// Gets the configuration manager that provides access to application configuration settings.
    /// </summary>
    public AppConfigurationManager AppConfiguration { get; internal set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="TestFuznConfiguration"/> class with default settings.
    /// </summary>
    internal TestFuznConfiguration(IServiceCollection services)
    {
        Services = services;
    }

    /// <summary>
    /// Adds a context plugin to the configuration.
    /// Context plugins can manage state and resources for tests.
    /// </summary>
    /// <param name="plugin">The context plugin to add.</param>
    /// <exception cref="ArgumentNullException">Thrown when the plugin is null.</exception>
    public void AddContextPlugin(IContextPlugin plugin)
    {
        ArgumentNullException.ThrowIfNull(plugin);
        ContextPlugins.Add(plugin);
    }

    /// <summary>
    /// Registers an additional standard report writer.
    /// </summary>
    internal void AddStandardReport(IStandardReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        Services.AddSingleton(report);
    }

    /// <summary>
    /// Registers an additional load report writer.
    /// </summary>
    internal void AddLoadReport(ILoadReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        Services.AddSingleton(report);
    }

    /// <summary>
    /// Registers an additional sink plugin.
    /// </summary>
    internal void AddSinkPlugin(ISinkPlugin plugin)
    {
        ArgumentNullException.ThrowIfNull(plugin);
        Services.AddSingleton(plugin);
    }

    /// <summary>
    /// Builds the service provider from the configured services.
    /// Called internally after all plugins have registered their services.
    /// </summary>
    internal IServiceProvider BuildServiceProvider()
    {
        return Services.BuildServiceProvider();
    }
}