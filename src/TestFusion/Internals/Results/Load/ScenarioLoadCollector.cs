using TestFusion.Contracts.Results.Feature;
using TestFusion.Contracts.Results.Load;
using TestFusion.Internals.Execution;

namespace TestFusion.Internals.Results.Load;

internal class ScenarioLoadCollector
{
    private readonly object _lock = new object();
    private string _featureName;
    private string _scenarioName;
    private DateTime _initStartTime;
    private DateTime _initEndTime;
    private DateTime _warmupStartTime;
    private DateTime _warmupEndTime;
    private DateTime _measurementStartTime;
    private DateTime _measurementEndTime;
    private DateTime _cleanupStartTime;
    private DateTime _cleanupEndTime;
    private DateTime _startTime;
    private DateTime _endTime;
    private TestPhase _testPhase = TestPhase.Init;
    private bool _isCompleted;
    private ScenarioStatus _status;
    private Dictionary<string, StepLoadCollector> _steps = new();
    private int _requestsPerSecond = 0;
    private int _requestCount = 0;
    private StatsCollector _ok = new();
    private StatsCollector _failed = new();
    private int _warmupRequestCountOk = 0;
    private int _warmupRequestCountFailed = 0;
    private ScenarioLoadResult _cachedCurrentResult;
    private DateTime _lastUpdated;
    private List<Exception>? _assertWhenDoneExceptions;

    public string ScenarioName { get => _scenarioName; set => _scenarioName = value; }

    internal void Init(Scenario scenario, string featureName)
    {
        MarkPhaseAsStartedNoLocking(TestPhase.Init);
        _startTime = DateTime.UtcNow;
        _featureName = featureName;
        _scenarioName = scenario.Name;
        _status = ScenarioStatus.Passed;
        foreach (var step in scenario.Steps)
        {
            _steps.Add(step.Name, new StepLoadCollector(step.Name));
        }
    }

    internal void RecordWarmup(ScenarioStatus status)
    {
        lock (_lock)
        {
            if (status == ScenarioStatus.Passed)
                _warmupRequestCountOk++;
            else if (status == ScenarioStatus.Failed)
                _warmupRequestCountFailed++;
        }
    }

    internal void RecordMeasurement(ScenarioStatus status, IterationFeatureResult result)
    {
        lock (_lock)
        {
            // Do not update _status here, only AssertWhileRunning and AssertWhenDone can fail the scenario
            _lastUpdated = DateTime.UtcNow;
            _requestCount++;

            var testRunTimeInSeconds = (int) (_lastUpdated - _startTime).TotalSeconds;
            if (testRunTimeInSeconds == 0)
                _requestsPerSecond = _requestCount;
            else
                _requestsPerSecond = _requestCount / testRunTimeInSeconds;

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

    private void MarkPhaseAsStartedNoLocking(TestPhase testPhase)
    { 
        switch (testPhase)
        {
            case TestPhase.Init:
                _initStartTime = DateTime.UtcNow;
                break;
            case TestPhase.Warmup:
                _warmupStartTime = DateTime.UtcNow;
                break;
            case TestPhase.Measurement:
                _measurementStartTime = DateTime.UtcNow;
                break;
            case TestPhase.Cleanup:
                _cleanupStartTime = DateTime.UtcNow;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(testPhase), testPhase, null);
        }
    }

    internal void MarkPhaseAsStarted(TestPhase testPhase)
    {
        lock (_lock)
        {
            MarkPhaseAsCompleted(testPhase);
        }
    }

    internal void MarkPhaseAsCompleted(TestPhase testPhase)
    {
        lock (_lock)
        {
            switch (testPhase)
            {
                case TestPhase.Init:
                {
                    _initEndTime = DateTime.UtcNow;
                    break;
                }
                case TestPhase.Warmup:
                {
                    _warmupEndTime = DateTime.UtcNow;
                    break;
                }
                case TestPhase.Measurement:
                {
                    _measurementEndTime = _lastUpdated;
                    _isCompleted = true;
                    _endTime = _lastUpdated;
                    _ = GetCurrentResult(true);
                    break;
                }
                case TestPhase.Cleanup:
                {
                    _cleanupEndTime = DateTime.UtcNow;
                    break;
                }
                default:
                    throw new ArgumentOutOfRangeException(nameof(testPhase), testPhase, null);
            }
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
            result.InitStartTime = _initStartTime;
            result.InitEndTime = _initEndTime;
            // Warmup phase.
            result.WarmupStartTime = _warmupStartTime;
            result.WarmupRequestCountOk = _warmupRequestCountOk;
            result.WarmupRequestCountFailed = _warmupRequestCountFailed;
            result.WarmupEndTime = _warmupEndTime;
            // Measurement phase.
            result.MeasurementStartTime = _measurementStartTime;
            result.MeasurementEndTime = _measurementEndTime;
            result.StartTime = _startTime;
            result.EndTime = _endTime;
            
            result.Created = _lastUpdated;
            result.Status = _status;
            result.RequestsPerSecond = _requestsPerSecond;
            result.RequestCount = _requestCount;
            result.Ok = _ok.GetCurrentResult();
            result.Failed = _failed.GetCurrentResult();
            result.Steps = new Dictionary<string, StepLoadResult>();
            foreach (var step in _steps)
            {
                result.Steps.Add(step.Key, step.Value.GetCurrentResult());
            }
            result.AssertWhenDoneExceptions = _assertWhenDoneExceptions;
            // Cleanup phase.
            result.CleanupStartTime = _cleanupStartTime;
            result.CleanupEndTime = _cleanupEndTime;

            result.IsCompleted = _isCompleted;
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
