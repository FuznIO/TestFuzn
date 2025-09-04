using Fuzn.TestFuzn.Contracts.Results.Load;

namespace Fuzn.TestFuzn.Contracts.Sinks;

public interface ISinkPlugin
{
    Task InitGlobal();
    Task WriteMetrics(string testRunId, string featureName, ScenarioLoadResult scenarioResult);
    Task CleanupGlobal();
}
