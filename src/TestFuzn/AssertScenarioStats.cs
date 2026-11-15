using Fuzn.TestFuzn.Contracts.Results.Load;

namespace Fuzn.TestFuzn;

/// <summary>
/// Provides assertion capabilities for load test scenario statistics.
/// </summary>
public class AssertScenarioStats
{
    /// <summary>
    /// Gets the number of iterations of the scenario.
    /// </summary>
    public int Count { get; }

    /// <summary>
    /// Gets the statistics of the ok iterations.
    /// </summary>
    public AssertStats Ok { get; }

    /// <summary>
    /// Gets the statistics of the failed iterations.
    /// </summary>
    public AssertStats Failed { get; }

    /// <summary>
    /// Gets the share of ok iterations (0.0 to 1.0).
    /// </summary>
    public double OkRate { get; }

    /// <summary>
    /// Gets the share of failed iterations (0.0 to 1.0).
    /// </summary>
    public double FailedRate { get; }

    private Dictionary<string, AssertStepStats> _assertStepMetrics { get; set; }

    internal AssertScenarioStats(ScenarioLoadResult scenarioResult)
    {
        Count = scenarioResult.Count;
        Ok = new AssertStats(scenarioResult.Ok);
        Failed = new AssertStats(scenarioResult.Failed);
        OkRate = Count == 0 ? 0 : (double)Ok.Count / Count;
        FailedRate = Count == 0 ? 0 : (double)Failed.Count / Count;

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
