using TestFusion.Results.Load;

namespace TestFusion.Contracts.Sinks;

public interface ISinkPlugin
{
    Task InitGlobal();
    Task WriteMetrics(string testRunId, string scenarioName, ScenarioLoadResult scenarioResult);
    Task CleanupGlobal();
}
