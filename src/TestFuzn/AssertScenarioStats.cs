using Fuzn.TestFuzn.Contracts.Results.Load;

namespace Fuzn.TestFuzn;

/// <summary>
/// Provides assertion capabilities for load test scenario statistics.
/// </summary>
public class AssertScenarioStats
{
    /// <summary>
    /// Gets the total number of requests executed in the scenario.
    /// </summary>
    public int RequestCount { get; }

    /// <summary>
    /// Gets the statistics for successful requests.
    /// </summary>
    public AssertStats Ok { get; }

    /// <summary>
    /// Gets the statistics for failed requests.
    /// </summary>
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

    /// <summary>
    /// Gets the statistics for a specific step by name.
    /// </summary>
    /// <param name="stepName">The name of the step to retrieve statistics for.</param>
    /// <returns>The step statistics.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when no step with the specified name exists.</exception>
    public AssertStepStats GetStep(string stepName)
    {
        if (_assertStepMetrics.TryGetValue(stepName, out var metrics))
            return metrics;
        throw new KeyNotFoundException($"No step metrics found for step: {stepName}");
    }
}
