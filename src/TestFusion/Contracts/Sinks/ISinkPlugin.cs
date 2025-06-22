using TestFusion.Contracts.Results.Load;

namespace TestFusion.Contracts.Sinks;

public interface ISinkPlugin
{
    Task InitGlobal();
    Task WriteMetrics(string testRunId, string featureName, ScenarioLoadResult scenarioResult);
    Task CleanupGlobal();
}
