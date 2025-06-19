using TestFusion.Internals.Reports;
using TestFusion.Plugins.Context;
using TestFusion.Plugins.Report;
using TestFusion.Plugins.Sink;

namespace TestFusion
{
    public class TestFusionConfiguration
    {
        public string TestSuiteName { get; set; }
        internal List<IContextPlugin> ContextPlugins { get; set; } = new();
        internal List<IFeatureReport> FeatureReports { get; set; } = new();
        internal List<ILoadReport> LoadReports { get; set; } = new();
        internal List<ISinkPlugin> SinkPlugins { get; set; } = new();
        
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

        public void UseDefaultFeatureHtmlReport()
        {
            AddFeatureReport(new FeatureHtmlReportWriter());
        }

        public void UseDefaultReports()
        {
            UseDefaultFeatureReports();
            UseDefaultLoadReports();
        }

        public void UseDefaultFeatureReports()
        {
            AddFeatureReport(new FeatureXmlReportWriter());
            AddFeatureReport(new FeatureHtmlReportWriter());
        }

        public void UseDefaultLoadReports()
        {
            AddSinkPlugin(new InMemorySnapshotCollectorSinkPlugin());
            AddLoadReport(new LoadHtmlReportWriter());
            AddLoadReport(new LoadXmlReportWriter());
        }
    }
}
