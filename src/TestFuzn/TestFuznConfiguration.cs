using Fuzn.TestFuzn.Contracts.Plugins;
using Fuzn.TestFuzn.Contracts.Providers;
using Fuzn.TestFuzn.Contracts.Reports;
using Fuzn.TestFuzn.Contracts.Sinks;
using Fuzn.TestFuzn.Internals.Reports.Standard;
using Fuzn.TestFuzn.Internals.Reports.Load;

namespace Fuzn.TestFuzn;

public class TestFuznConfiguration
{
    public SuiteInfo Suite { get; set; }
    internal List<IContextPlugin> ContextPlugins { get; set; } = new();
    internal List<IStandardReport> StandardReports { get; set; } = new();
    internal List<ILoadReport> LoadReports { get; set; } = new();
    internal List<ISinkPlugin> SinkPlugins { get; set; } = new();
    internal ISerializerProvider SerializerProvider { get; set; }

    public TestFuznConfiguration()
    {
        AddStandardReport(new StandardXmlReportWriter());
        AddStandardReport(new StandardHtmlReportWriter());

        AddSinkPlugin(new InMemorySnapshotCollectorSinkPlugin());
        AddLoadReport(new LoadHtmlReportWriter());
        AddLoadReport(new LoadXmlReportWriter());

        SetSerializerProvider(new SystemTextJsonSerializerProvider());
    }

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

    public void SetSerializerProvider(ISerializerProvider serializerProvider)
    {
        if (serializerProvider == null)
            throw new ArgumentNullException(nameof(serializerProvider), "SerializerProvider cannot be null");

        SerializerProvider = serializerProvider;
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