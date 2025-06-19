using NATS.Client.ObjectStore;
using TestFusion.Results.Feature;
using TestFusion.Results.Load;

namespace TestFusion.Internals.Results.Load;

internal class ScenarioLoadCollector
{
    private readonly object _lock = new object();
    private string _featureName;
    private string _scenarioName;
    private DateTime _startTime;
    private DateTime _endTime;
    private bool _isCompleted;
    private ScenarioStatus _status;
    private Dictionary<string, StepLoadCollector> _steps = new();
    private int _requestsPerSecond = 0;
    private StatsCollector _ok = new();
    private int _count = 0;
    private StatsCollector _failed = new();    
    private ScenarioLoadResult _cachedCurrentResult;
    private DateTime _lastUpdated;
    private List<Exception>? _assertWhenDoneExceptions;

    public string ScenarioName { get => _scenarioName; set => _scenarioName = value; }

    internal void Init(Scenario scenario, string featureName)
    {
        _startTime = DateTime.UtcNow;
        _featureName = featureName;
        _scenarioName = scenario.Name;
        _status = ScenarioStatus.Passed;
        foreach (var step in scenario.Steps)
        {
            _steps.Add(step.Name, new StepLoadCollector(step.Name));
        }
    }

    internal void Record(ScenarioStatus status, IterationResult result)
    {
        lock (_lock)
        {
            // Do not update _status here, only AssertWhileRunning and AssertWhenDone can fail the scenario
            _lastUpdated = DateTime.UtcNow;
            _count++;

            var testRunTimeInSeconds = (int) (_lastUpdated - _startTime).TotalSeconds;
            if (testRunTimeInSeconds == 0)
                _requestsPerSecond = _count;
            else
                _requestsPerSecond = _count / testRunTimeInSeconds;

            if (status == ScenarioStatus.Passed)
            {
                _ok.Record(result.ExecutionDuration, _startTime, _lastUpdated);
            }
            else if (status == ScenarioStatus.Failed)
            {
                _failed.Record(result.ExecutionDuration, _startTime, _lastUpdated);
            }

            foreach (var step in _steps)
            {
                var stepIterationResult = result.StepResults[step.Key];

                step.Value.Record(stepIterationResult, _startTime, _lastUpdated);
            }
        }
    }

    internal void MarkAsCompleted()
    {
        lock (_lock)
        {
            _isCompleted = true;
            _endTime = _lastUpdated;
            _ = GetCurrentResult(true);
        }
    }

    internal ScenarioLoadResult GetCurrentResult(bool forceRefresh = false)
    {
        lock (_lock)
        {
            if (!forceRefresh)
            {
                if (_cachedCurrentResult != null && _cachedCurrentResult.Created == _lastUpdated)
                    return _cachedCurrentResult;

                if (_cachedCurrentResult != null)
                {
                    if (_lastUpdated.AddSeconds(1) < DateTime.UtcNow)
                        return _cachedCurrentResult;
                }
            }

            var result = new ScenarioLoadResult();
            result.FeatureName = _featureName;
            result.ScenarioName = _scenarioName;
            result.StartTime = _startTime;
            result.EndTime = _endTime;
            result.IsCompleted = _isCompleted;
            result.Created = _lastUpdated;
            result.Status = _status;
            result.RequestsPerSecond = _requestsPerSecond;
            result.RequestCount = _count;
            result.Ok = _ok.GetCurrentResult();
            result.Failed = _failed.GetCurrentResult();
            result.Steps = new Dictionary<string, StepLoadResult>();
            foreach (var step in _steps)
            {
                result.Steps.Add(step.Key, step.Value.GetCurrentResult());
            }
            result.AssertWhenDoneExceptions = _assertWhenDoneExceptions;

            _cachedCurrentResult = result;

            return result;
        }
    }

    internal void AssertWhenDoneException(Exception exception)
    {
        lock (_lock)
        {
            _assertWhenDoneExceptions ??= [exception];
            _lastUpdated = DateTime.UtcNow;
        }
    }

    internal void SetStatus(ScenarioStatus status)
    {
        lock (_lock)
        {
            _status = status;
            _lastUpdated = DateTime.UtcNow;
        }
    }
}
