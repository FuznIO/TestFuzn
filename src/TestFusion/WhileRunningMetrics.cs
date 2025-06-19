using TestFusion.Results.Load;

namespace TestFusion;

public class WhileRunningMetrics
{
    private readonly ScenarioLoadResult _scenarioResult;

    public TimeSpan Duration { get; set; }
    public int RequestCount { get; private set; }
    public int RequestCountOk { get; set; }
    public int RequestCountNotOk { get; set; }
    public TimeSpan MinResponseTime { get; private set; }
    public TimeSpan MaxResponseTime { get; private set; }

    internal WhileRunningMetrics(ScenarioLoadResult scenarioResult)
    {
        _scenarioResult = scenarioResult;

        RequestCount = _scenarioResult.RequestCount;
        RequestCountOk = _scenarioResult.Ok.RequestCount;
        RequestCountNotOk = _scenarioResult.Failed.RequestCount;
        MinResponseTime = _scenarioResult.Ok.ResponseTimeMin;
        MaxResponseTime = _scenarioResult.Ok.ResponseTimeMax;
        Duration = _scenarioResult.TotalExecutionDuration;
    }
}