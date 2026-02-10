using Fuzn.TestFuzn.Contracts.Plugins;
using Fuzn.TestFuzn.Contracts.Reports;
using Fuzn.TestFuzn.Contracts.Sinks;
using Fuzn.TestFuzn.Internals.Reports.Standard;
using Fuzn.TestFuzn.Internals.Reports.Load;
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
    /// Gets the service collection for registering dependencies.
    /// Plugins and user code can add services here during configuration.
    /// </summary>
    public IServiceCollection Services { get; } = new ServiceCollection();

    internal List<IContextPlugin> ContextPlugins { get; set; } = new();
    internal List<IStandardReport> StandardReports { get; set; } = new();
    internal List<ILoadReport> LoadReports { get; set; } = new();
    internal List<ISinkPlugin> SinkPlugins { get; set; } = new();
    internal IServiceProvider ServiceProvider { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="TestFuznConfiguration"/> class with default settings.
    /// </summary>
    public TestFuznConfiguration()
    {
        AddStandardReport(new StandardXmlReportWriter());
        AddStandardReport(new StandardHtmlReportWriter());

        AddSinkPlugin(new InMemorySnapshotCollectorSinkPlugin());
        AddLoadReport(new LoadHtmlReportWriter());
        AddLoadReport(new LoadXmlReportWriter());
    }

    /// <summary>
    /// Adds a context plugin to the configuration.
    /// Context plugins can manage state and resources for tests.
    /// </summary>
    /// <param name="plugin">The context plugin to add.</param>
    /// <exception cref="ArgumentNullException">Thrown when the plugin is null.</exception>
    public void AddContextPlugin(IContextPlugin plugin)
    {
        if (plugin == null)
            throw new ArgumentNullException(nameof(plugin), "Context plugin cannot be null");
        
        ContextPlugins.Add(plugin);
    }

    internal void AddStandardReport(IStandardReport report)
    {
        if (report == null)
            throw new ArgumentNullException(nameof(report), "Standard report cannot be null");
        StandardReports.Add(report);
    }

    internal void AddLoadReport(ILoadReport report)
    {
        if (report == null)
            throw new ArgumentNullException(nameof(report), "Load report cannot be null");
        LoadReports.Add(report);
    }

    internal void AddSinkPlugin(ISinkPlugin plugin)
    {
        if (plugin == null)
            throw new ArgumentNullException(nameof(plugin), "Sink plugin cannot be null");
        SinkPlugins.Add(plugin);
    }

    /// <summary>
    /// Builds the service provider from the configured services.
    /// Called internally after all plugins have registered their services.
    /// </summary>
    internal void BuildServiceProvider()
    {
        ServiceProvider = Services.BuildServiceProvider();
    }

    internal void ClearReports()
    {
        StandardReports.Clear();
        LoadReports.Clear();
        var sinkPlugin = SinkPlugins.OfType<InMemorySnapshotCollectorSinkPlugin>().FirstOrDefault();
        if (sinkPlugin != null)
        {
            SinkPlugins.Remove(sinkPlugin);
        }
    }
}