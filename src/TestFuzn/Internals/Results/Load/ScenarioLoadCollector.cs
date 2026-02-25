using Fuzn.TestFuzn.Contracts.Results.Standard;
using Fuzn.TestFuzn.Contracts.Results.Load;
using Fuzn.TestFuzn.Internals.Execution;

namespace Fuzn.TestFuzn.Internals.Results.Load;

internal class ScenarioLoadCollector
{
    private readonly object _lock = new object();
    private string _scenarioName;
    private string _description;
    private string _id;
    private DateTime _initStartTime;
    private DateTime _initEndTime;
    private DateTime _warmupStartTime;
    private DateTime _warmupEndTime;
    private DateTime _measurementStartTime;
    private DateTime _measurementEndTime;
    private DateTime _cleanupStartTime;
    private DateTime _cleanupEndTime;
    private bool _isCompleted;
    private TestStatus _status;
    private Dictionary<string, StepLoadCollector> _steps = new();
    private int _requestsPerSecond = 0;
    private int _requestCount = 0;
    private StatsCollector _ok = new();
    private StatsCollector _failed = new();
    private int _warmupRequestCountOk = 0;
    private int _warmupRequestCountFailed = 0;
    private ScenarioLoadResult _cachedCurrentResult;
    private DateTime _lastUpdated;
    private Exception _assertWhileRunningException;
    private Exception _assertWhenDoneException;
    private Scenario _scenario;

    public string ScenarioName { get => _scenarioName; set => _scenarioName = value; }
    public string Id { get => _id; set => _id = value; }

    public ScenarioLoadCollector(Scenario scenario)
    {
        _scenarioName = scenario.Name;
        _id = scenario.Id;
        _description = scenario.Description;
        _status = TestStatus.Passed;
        foreach (var step in scenario.Steps)
        {
            _steps.Add(step.Name, new StepLoadCollector(step.Name, step.Id));
        }
        _scenario = scenario;
    }

    internal void RecordWarmup(TestStatus status)
    {
        lock (_lock)
        {
            if (status == TestStatus.Passed)
                _warmupRequestCountOk++;
            else if (status == TestStatus.Failed)
                _warmupRequestCountFailed++;
        }
    }

    internal void RecordMeasurement(TestStatus status, IterationResult result)
    {
        lock (_lock)
        {
            // Do not update _status here, only AssertWhileRunning and AssertWhenDone can fail the scenario
            _lastUpdated = DateTime.UtcNow;
            _requestCount++;

            var testRunTimeInSeconds = (int) (_lastUpdated - _measurementStartTime).TotalSeconds;
            if (testRunTimeInSeconds == 0)
                _requestsPerSecond = _requestCount;
            else
                _requestsPerSecond = _requestCount / testRunTimeInSeconds;

            if (status == TestStatus.Passed)
                _ok.Record(result.ExecutionDuration, _measurementStartTime, _lastUpdated);
            else if (status == TestStatus.Failed)
                _failed.Record(result.ExecutionDuration, _measurementStartTime, _lastUpdated);
            else
                throw new Exception($"Invalid scenario status: {status}");

            foreach (var step in _steps)
            {
                var stepIterationResult = result.StepResults[step.Key];

                step.Value.Record(stepIterationResult, _measurementStartTime, _lastUpdated);
            }
        }
    }

    internal void MarkPhaseAsStarted(LoadTestPhase testPhase, DateTime timestamp)
    {
        lock (_lock)
        {
            switch (testPhase)
            {
                case LoadTestPhase.Init:
                    _initStartTime = timestamp;
                    break;
                case LoadTestPhase.Warmup:
                    _warmupStartTime = timestamp;
                    break;
                case LoadTestPhase.Measurement:
                    _measurementStartTime = timestamp;
                    break;
                case LoadTestPhase.Cleanup:
                    _cleanupStartTime = timestamp;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(testPhase), testPhase, null);
            }
        }
    }

    internal void MarkPhaseAsCompleted(LoadTestPhase testPhase, DateTime timestamp)
    {
        lock (_lock)
        {
            switch (testPhase)
            {
                case LoadTestPhase.Init:
                {
                    _initEndTime = timestamp;
                    break;
                }
                case LoadTestPhase.Warmup:
                {
                    _warmupEndTime = timestamp;
                    break;
                }
                case LoadTestPhase.Measurement:
                {
                    _measurementEndTime = _lastUpdated;
                    _isCompleted = true;
                    _ = GetCurrentResult(true);
                    break;
                }
                case LoadTestPhase.Cleanup:
                {
                    _cleanupEndTime = timestamp;
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
            result.ScenarioName = _scenarioName;
            result.Id = _id;
            result.Description = _description;

            result.Simulations = new List<string>();
            foreach (var simulation in _scenario.SimulationsInternal)
            {
                result.Simulations.Add(simulation.GetDescription());
            }
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
            result.AssertWhileRunningException = _assertWhileRunningException;
            result.AssertWhenDoneException = _assertWhenDoneException;
            // Cleanup phase.
            result.CleanupStartTime = _cleanupStartTime;
            result.CleanupEndTime = _cleanupEndTime;

            result.IsCompleted = _isCompleted;
            _cachedCurrentResult = result;

            return result;
        }
    }

    internal void SetAssertWhileRunningException(Exception exception)
    {
        lock (_lock)
        {
            _assertWhileRunningException = exception;
            _lastUpdated = DateTime.UtcNow;
        }
    }

    internal void SetAssertWhenDoneException(Exception exception)
    {
        lock (_lock)
        {
            _assertWhenDoneException = exception;
            _lastUpdated = DateTime.UtcNow;
        }
    }

    internal void SetStatus(TestStatus status)
    {
        lock (_lock)
        {
            _status = status;
            _lastUpdated = DateTime.UtcNow;
        }
    }
}
