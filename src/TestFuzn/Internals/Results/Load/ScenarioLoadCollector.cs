using Fuzn.TestFuzn.Contracts.Results.Standard;
using Fuzn.TestFuzn.Contracts.Results.Load;
using Fuzn.TestFuzn.Contracts.Results.Standard;
using Fuzn.TestFuzn.Internals.Cluster;
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
    private Dictionary<string, StepLoadCollector> _hooks = new();
    private int _perSecond = 0;
    private StatsCollector _ok = new();
    private StatsCollector _failed = new();
    private int _warmupOkCount = 0;
    private int _warmupFailedCount = 0;
    private ScenarioLoadResult _cachedCurrentResult;
    private DateTime _lastUpdated;
    private Exception _assertWhileWarmingUpException;
    private Exception _assertWhileRunningException;
    private Exception _assertWhenDoneException;
    private List<string> _simulationDescriptions;
    private List<SimulationState> _simulations = new();
    private int? _inputDataCount;
    private string? _inputDataHash;
    private TimeSpan? _mergedMeasurementDuration;

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
    }

    internal void CacheSimulationDescriptions(Scenario scenario)
    {
        _simulationDescriptions = new List<string>();
        _simulations = new List<SimulationState>();
        foreach (var simulation in scenario.SimulationsInternal)
        {
            _simulationDescriptions.Add(simulation.GetDescription());
            _simulations.Add(new SimulationState { Description = simulation.GetDescription(), IsWarmup = simulation.IsWarmup });
        }
    }

    internal void SetInputDataInfo(int count, string hash)
    {
        lock (_lock)
        {
            _inputDataCount = count;
            _inputDataHash = hash;
        }
    }

    // The raw totals of the ok iterations, for the report's timeline.
    internal StatsState GetOkState()
    {
        lock (_lock)
            return _ok.GetState();
    }

    internal ScenarioStatsState GetState()
    {
        lock (_lock)
        {
            var state = new ScenarioStatsState();
            state.Name = _scenarioName;
            state.Phase = CurrentPhase();
            state.Simulations = new List<SimulationState>(_simulations);
            state.MeasurementDurationTicks = MeasurementDuration().Ticks;
            state.WarmupOk = _warmupOkCount;
            state.WarmupFailed = _warmupFailedCount;
            state.Ok = _ok.GetState();
            state.Failed = _failed.GetState();
            foreach (var step in _steps)
                state.Steps.Add(step.Value.GetState());
            foreach (var hook in _hooks)
                state.Hooks.Add(hook.Value.GetState());
            state.InputDataCount = _inputDataCount;
            state.InputDataHash = _inputDataHash;
            return state;
        }
    }

    private string CurrentPhase()
    {
        if (_isCompleted)
            return ScenarioPhase.Completed;
        if (_measurementStartTime != default)
            return ScenarioPhase.Measurement;
        if (_warmupStartTime != default)
            return ScenarioPhase.Warmup;

        return ScenarioPhase.Init;
    }

    private TimeSpan MeasurementDuration()
    {
        if (_mergedMeasurementDuration != null)
            return _mergedMeasurementDuration.Value;
        if (_measurementStartTime == default)
            return TimeSpan.Zero;

        var end = _isCompleted ? _measurementEndTime : DateTime.UtcNow;
        if (end < _measurementStartTime)
            return TimeSpan.Zero;

        return end - _measurementStartTime;
    }

    // Replaces the recorded stats with the sum of the given states, e.g. one per load worker.
    internal void LoadMerged(IReadOnlyList<ScenarioStatsState> states, TimeSpan measurementDuration)
    {
        lock (_lock)
        {
            _ok = new StatsCollector();
            _failed = new StatsCollector();
            _warmupOkCount = 0;
            _warmupFailedCount = 0;

            var steps = new Dictionary<string, StepLoadCollector>();
            foreach (var step in _steps)
                steps.Add(step.Key, new StepLoadCollector(step.Key, step.Value.Id));
            var hooks = new Dictionary<string, StepLoadCollector>();

            foreach (var state in states)
            {
                _ok.Add(state.Ok);
                _failed.Add(state.Failed);
                _warmupOkCount += state.WarmupOk;
                _warmupFailedCount += state.WarmupFailed;

                foreach (var stepState in state.Steps)
                {
                    if (!steps.TryGetValue(stepState.Name, out var step))
                    {
                        step = new StepLoadCollector(stepState.Name, stepState.Id);
                        steps.Add(stepState.Name, step);
                    }

                    step.Add(stepState);
                }

                foreach (var hookState in state.Hooks)
                {
                    if (!hooks.TryGetValue(hookState.Name, out var hook))
                    {
                        hook = new StepLoadCollector(hookState.Name, hookState.Id);
                        hooks.Add(hookState.Name, hook);
                    }

                    hook.Add(hookState);
                }

                if (_simulations.Count == 0 && state.Simulations.Count > 0)
                {
                    _simulations = new List<SimulationState>(state.Simulations);
                    _simulationDescriptions = state.Simulations.Select(s => s.Description).ToList();
                }
            }

            _steps = steps;
            _hooks = hooks;
            _mergedMeasurementDuration = measurementDuration;
            _ok.SetPerSecond(measurementDuration);
            _failed.SetPerSecond(measurementDuration);
            foreach (var step in _steps.Values)
                step.SetPerSecond(measurementDuration);
            _perSecond = StatsCollector.PerSecond(_ok.Count + _failed.Count, measurementDuration);
            _lastUpdated = DateTime.UtcNow;
        }
    }

    internal IReadOnlyList<SimulationState> Simulations()
    {
        lock (_lock)
            return new List<SimulationState>(_simulations);
    }

    internal ScenarioTimesState GetTimes()
    {
        lock (_lock)
        {
            return new ScenarioTimesState
            {
                InitStartTime = _initStartTime,
                InitEndTime = _initEndTime,
                WarmupStartTime = _warmupStartTime,
                WarmupEndTime = _warmupEndTime,
                MeasurementStartTime = _measurementStartTime,
                MeasurementEndTime = _measurementEndTime,
                CleanupStartTime = _cleanupStartTime,
                CleanupEndTime = _cleanupEndTime,
                IsCompleted = _isCompleted,
                Status = _status.ToString(),
                AssertWhileWarmingUpError = _assertWhileWarmingUpException != null ? _assertWhileWarmingUpException.Message : null,
                AssertWhileRunningError = _assertWhileRunningException != null ? _assertWhileRunningException.Message : null,
                AssertWhenDoneError = _assertWhenDoneException != null ? _assertWhenDoneException.Message : null
            };
        }
    }

    // The times come from the controller's clock; offset moves them onto this machine's clock.
    internal void ApplyTimes(ScenarioTimesState times, TimeSpan offset)
    {
        lock (_lock)
        {
            _initStartTime = Shift(times.InitStartTime, offset);
            _initEndTime = Shift(times.InitEndTime, offset);
            _warmupStartTime = Shift(times.WarmupStartTime, offset);
            _warmupEndTime = Shift(times.WarmupEndTime, offset);
            _measurementStartTime = Shift(times.MeasurementStartTime, offset);
            _measurementEndTime = Shift(times.MeasurementEndTime, offset);
            _cleanupStartTime = Shift(times.CleanupStartTime, offset);
            _cleanupEndTime = Shift(times.CleanupEndTime, offset);
            _isCompleted = times.IsCompleted;
            if (Enum.TryParse<TestStatus>(times.Status, out var status))
                _status = status;
            _assertWhileWarmingUpException = RemoteError(times.AssertWhileWarmingUpError);
            _assertWhileRunningException = RemoteError(times.AssertWhileRunningError);
            _assertWhenDoneException = RemoteError(times.AssertWhenDoneError);
            _lastUpdated = DateTime.UtcNow;
        }
    }

    private static DateTime Shift(DateTime time, TimeSpan offset)
    {
        if (time == default)
            return default;

        return DateTime.SpecifyKind(time + offset, DateTimeKind.Utc);
    }

    private static Exception? RemoteError(string? message)
    {
        if (message == null)
            return null;

        return new RemoteRunException(message);
    }

    internal void RecordWarmup(TestStatus status)
    {
        lock (_lock)
        {
            if (status == TestStatus.Passed)
                _warmupOkCount++;
            else if (status == TestStatus.Failed)
                _warmupFailedCount++;
        }
    }

    internal WarmupStats GetWarmupStats()
    {
        lock (_lock)
        {
            var duration = _warmupStartTime != default ? DateTime.UtcNow - _warmupStartTime : TimeSpan.Zero;
            return new WarmupStats(_warmupOkCount, _warmupFailedCount, duration);
        }
    }

    internal void RecordMeasurement(TestStatus status, IterationResult result)
    {
        lock (_lock)
        {
            // Do not update _status here, only AssertWhileRunning and AssertWhenDone can fail the scenario
            _lastUpdated = DateTime.UtcNow;

            if (status == TestStatus.Passed)
                _ok.Record(result.ExecuteDuration(), _measurementStartTime, _lastUpdated);
            else if (status == TestStatus.Failed)
                _failed.Record(result.ExecuteDuration(), _measurementStartTime, _lastUpdated);
            else
                throw new Exception($"Invalid scenario status: {status}");

            _perSecond = StatsCollector.PerSecond(_ok.Count + _failed.Count, _lastUpdated - _measurementStartTime);

            foreach (var step in _steps)
            {
                if (!result.StepResults.TryGetValue(step.Key, out var stepIterationResult))
                    continue;

                step.Value.Record(stepIterationResult, _measurementStartTime, _lastUpdated);
            }
        }
    }

    internal void RecordHookFailure(string hook, Exception exception)
    {
        lock (_lock)
        {
            if (!_hooks.TryGetValue(hook, out var collector))
            {
                collector = new StepLoadCollector(hook, hook);
                _hooks.Add(hook, collector);
            }

            collector.Record(new StepStandardResult { Name = hook, Id = hook, Status = StepStatus.Failed, Exception = exception }, _measurementStartTime, _lastUpdated);
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
                    // Merged stats end where the longest worker's measurement ended, not when the controller noticed.
                    if (_mergedMeasurementDuration != null && _measurementStartTime != default)
                        _measurementEndTime = _measurementStartTime + _mergedMeasurementDuration.Value;
                    else
                        _measurementEndTime = _lastUpdated != default ? _lastUpdated : timestamp;
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

            result.Simulations = _simulationDescriptions;
            result.InitStartTime = _initStartTime;
            result.InitEndTime = _initEndTime;
            // Warmup phase.
            result.WarmupStartTime = _warmupStartTime;
            result.WarmupOkCount = _warmupOkCount;
            result.WarmupFailedCount = _warmupFailedCount;
            result.WarmupEndTime = _warmupEndTime;
            // Measurement phase.
            result.MeasurementStartTime = _measurementStartTime;
            result.MeasurementEndTime = _measurementEndTime;
            
            result.Created = _lastUpdated;
            result.Status = _status;
            result.PerSecond = _perSecond;
            result.Ok = _ok.GetCurrentResult();
            result.Failed = _failed.GetCurrentResult();
            result.Steps = new Dictionary<string, StepLoadResult>();
            foreach (var step in _steps)
            {
                result.Steps.Add(step.Key, step.Value.GetCurrentResult());
            }
            result.Hooks = _hooks.Values.Select(hook => hook.GetCurrentResult()).ToList();
            result.AssertWhileWarmingUpException = _assertWhileWarmingUpException;
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

    internal void SetAssertWhileWarmingUpException(Exception exception)
    {
        lock (_lock)
        {
            _assertWhileWarmingUpException = exception;
            _lastUpdated = DateTime.UtcNow;
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
