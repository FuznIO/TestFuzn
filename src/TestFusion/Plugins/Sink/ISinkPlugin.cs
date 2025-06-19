using TestFusion.Results.Load;

namespace TestFusion.Plugins.Sink;

public interface ISinkPlugin
{
    Task InitGlobal();
    Task WriteMetrics(string testRunId, string scenarioName, ScenarioLoadResult scenarioResult);
    Task CleanupGlobal();
}
