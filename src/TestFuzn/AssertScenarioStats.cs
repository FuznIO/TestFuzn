using Fuzn.TestFuzn.Contracts.Results.Load;

namespace Fuzn.TestFuzn;

public class AssertScenarioStats
{
    private readonly ScenarioLoadResult _scenarioResult;
    public int RequestCount { get; }
    public AssertStats Ok { get; }
    public AssertStats Failed { get; }
    private Dictionary<string, AssertStepStats> _assertStepMetrics { get; set; }

    internal AssertScenarioStats(ScenarioLoadResult scenarioResult)
    {
        _scenarioResult = scenarioResult;
        RequestCount = scenarioResult.RequestCount;
        Ok = new AssertStats(scenarioResult.Ok);
        Failed = new AssertStats(scenarioResult.Failed);

        _assertStepMetrics = new();
        foreach (var step in scenarioResult.Steps)
            _assertStepMetrics.TryAdd(step.Key, new AssertStepStats(step.Value));
    }

    public AssertStepStats GetStepMetrics(string stepName)
    {
        if (_assertStepMetrics.TryGetValue(stepName, out var metrics))
            return metrics;
        throw new KeyNotFoundException($"No step metrics found for step: {stepName}");
    }
}
