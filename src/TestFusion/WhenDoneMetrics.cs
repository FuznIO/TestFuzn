using TestFusion.Results.Load;

namespace TestFusion;

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

public class AssertStepStats
{
    public string StepName { get; }
    public AssertStats Ok { get; }
    public AssertStats Failed { get; }

    internal AssertStepStats(StepLoadResult stepResult)
    {
        StepName = stepResult.Name;
        Ok = new AssertStats(stepResult.Ok);
        Failed = new AssertStats(stepResult.Failed);
    }
}

public class AssertStats
{
    public int RequestCount { get; internal set; }
    public int RequestsPerSecond { get; internal set; }
    public TimeSpan ResponseTimeMin { get; internal set; }
    public TimeSpan ResponseTimeMax { get; internal set; }
    public TimeSpan ResponseTimeMean { get; internal set; }
    public TimeSpan ResponseTimeStandardDeviation { get; internal set; }
    public TimeSpan ResponseTimeMedian { get; internal set; }
    public TimeSpan ResponseTimePercentile75 { get; internal set; }
    public TimeSpan ResponseTimePercentile95 { get; internal set; }
    public TimeSpan ResponseTimePercentile99 { get; internal set; }

    internal AssertStats(Stats stats)
    {
        RequestCount = stats.RequestCount;
        RequestsPerSecond = stats.RequestsPerSecond;
        ResponseTimeMin = stats.ResponseTimeMin;
        ResponseTimeMax = stats.ResponseTimeMax;
        ResponseTimeMean = stats.ResponseTimeMean;
        ResponseTimeStandardDeviation = stats.ResponseTimeStandardDeviation;
        ResponseTimeMedian = stats.ResponseTimeMedian;
        ResponseTimePercentile75 = stats.ResponseTimePercentile75;
        ResponseTimePercentile95 = stats.ResponseTimePercentile95;
        ResponseTimePercentile99 = stats.ResponseTimePercentile99;
    }
}
