using Fuzn.TestFuzn.Contracts.Results.Load;

namespace Fuzn.TestFuzn.Contracts.Sinks;

internal interface ISinkPlugin
{
    Task InitGlobal();
    Task WriteStats(string testRunId, string featureName, ScenarioLoadResult scenarioResult);
    Task CleanupGlobal();
}
