using Fuzn.TestFuzn.Contracts.Results.Load;

namespace Fuzn.TestFuzn;

public class AssertScenarioStats
{
    public int RequestCount { get; }
    public AssertStats Ok { get; }
    public AssertStats Failed { get; }
    private Dictionary<string, AssertStepStats> _assertStepMetrics { get; set; }

    internal AssertScenarioStats(ScenarioLoadResult scenarioResult)
    {
        RequestCount = scenarioResult.RequestCount;
        Ok = new AssertStats(scenarioResult.Ok);
        Failed = new AssertStats(scenarioResult.Failed);

        _assertStepMetrics = new();
        foreach (var step in scenarioResult.Steps)
        {
            AddStepStats(step.Key, step.Value);
        }
    }

    private void AddStepStats(string name, StepLoadResult loadResult)
    {
        _assertStepMetrics.TryAdd(name, new AssertStepStats(loadResult));

        if (loadResult.Steps == null || loadResult.Steps.Count == 0)
            return;

        foreach (var step in loadResult.Steps)
        {
            AddStepStats(step.Name, step);
        }
    }

    public AssertStepStats GetStep(string stepName)
    {
        if (_assertStepMetrics.TryGetValue(stepName, out var metrics))
            return metrics;
        throw new KeyNotFoundException($"No step metrics found for step: {stepName}");
    }
}
