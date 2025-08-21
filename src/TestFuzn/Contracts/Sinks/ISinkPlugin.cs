using FuznLabs.TestFuzn.Contracts.Results.Load;

namespace FuznLabs.TestFuzn.Contracts.Sinks;

public interface ISinkPlugin
{
    Task InitGlobal();
    Task WriteMetrics(string testRunId, string featureName, ScenarioLoadResult scenarioResult);
    Task CleanupGlobal();
}
