using Fuzn.TestFuzn.Contracts.Results.Load;

namespace Fuzn.TestFuzn.Contracts.Sinks;

internal interface ISinkPlugin
{
    Task InitSuite();
    Task WriteStats(string testRunId, string groupName, string testName, ScenarioLoadResult scenarioResult);
    Task CleanupSuite();
}
