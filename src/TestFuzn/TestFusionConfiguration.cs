using FuznLabs.TestFuzn.Contracts.Plugins;
using FuznLabs.TestFuzn.Contracts.Providers;
using FuznLabs.TestFuzn.Contracts.Reports;
using FuznLabs.TestFuzn.Contracts.Sinks;
using FuznLabs.TestFuzn.Internals.Comparers;
using FuznLabs.TestFuzn.Internals.Reports.Feature;
using FuznLabs.TestFuzn.Internals.Reports.Load;

namespace FuznLabs.TestFuzn
{
    public class TestFusionConfiguration
    {
        public string EnvironmentName { get; set; } = "";
        public string TestSuiteName { get; set; }
        internal List<IContextPlugin> ContextPlugins { get; set; } = new();
        internal List<IFeatureReport> FeatureReports { get; set; } = new();
        internal List<ILoadReport> LoadReports { get; set; } = new();
        internal List<ISinkPlugin> SinkPlugins { get; set; } = new();
        internal HashSet<ISerializerProvider> SerializerProviders { get; set; } = new(new SerializerProviderComparer());

        public TestFusionConfiguration()
        {
            AddFeatureReport(new FeatureXmlReportWriter());
            AddFeatureReport(new FeatureHtmlReportWriter());

            AddSinkPlugin(new InMemorySnapshotCollectorSinkPlugin());
            AddLoadReport(new LoadHtmlReportWriter());
            AddLoadReport(new LoadXmlReportWriter());

            AddSerializerProvider(new SystemTextJsonSerializerProvider());
        }

        public void AddContextPlugin(IContextPlugin plugin)
        {
            if (plugin == null)
                throw new ArgumentNullException(nameof(plugin), "Context plugin cannot be null");
            ContextPlugins.Add(plugin);
        }

        public void AddFeatureReport(IFeatureReport report)
        {
            if (report == null)
                throw new ArgumentNullException(nameof(report), "Feature report cannot be null");
            FeatureReports.Add(report);
        }

        public void AddLoadReport(ILoadReport report)
        {
            if (report == null)
                throw new ArgumentNullException(nameof(report), "Load report cannot be null");
            LoadReports.Add(report);
        }

        public void AddSinkPlugin(ISinkPlugin plugin)
        {
            if (plugin == null)
                throw new ArgumentNullException(nameof(plugin), "Sink plugin cannot be null");
            SinkPlugins.Add(plugin);
        }

        public void AddSerializerProvider(ISerializerProvider serializerProvider)
        {
            if (serializerProvider == null)
                throw new ArgumentNullException(nameof(serializerProvider), "SerializerProvider cannot be null");

            if (SerializerProviders.Contains(serializerProvider))
                SerializerProviders.Remove(serializerProvider);

            SerializerProviders.Add(serializerProvider);
        }

        public void ClearReports()
        {
            FeatureReports.Clear();
            LoadReports.Clear();
            var sinkPlugin = SinkPlugins.OfType<InMemorySnapshotCollectorSinkPlugin>().FirstOrDefault();
            if (sinkPlugin != null)
            {
                SinkPlugins.Remove(sinkPlugin);
            }
        }
    }
}
