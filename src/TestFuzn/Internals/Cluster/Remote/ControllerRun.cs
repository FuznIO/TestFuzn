using System.IO.Compression;
using System.Text.Json;
using Fuzn.TestFuzn.Internals.Cluster.Protocol;
using Fuzn.TestFuzn.Contracts;
using Fuzn.TestFuzn.Contracts.Results.Load;
using Fuzn.TestFuzn.Internals;
using Fuzn.TestFuzn.Internals.Cluster;
using Fuzn.TestFuzn.Internals.Cluster.LoadChild;
using Fuzn.TestFuzn.Internals.Execution;
using Fuzn.TestFuzn.Internals.Results.Load;
using Fuzn.TestFuzn.Internals.State;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Fuzn.TestFuzn.Internals.Cluster.Remote;

// The controller child: claims the load workers, sends start and start-measurement, merges the workers'
// cumulative stats every tick, runs the asserts, sinks and run-once hooks, and hands the merged stats to
// Cockpit for attached clients. It makes no load itself. Everything goes through Cockpit with the run token
// the worker app gave it; it calls Cockpit twice a tick and never listens.
internal sealed class ControllerRun
{
    private static readonly TimeSpan GapWarning = TimeSpan.FromSeconds(10);

    // When a run stops, the workers' last statuses arrive milliseconds apart. A point for such a short
    // interval holds counts from the seconds before it and draws a false spike, so it waits for the next.
    private static readonly TimeSpan MinPointsInterval = TimeSpan.FromSeconds(1);

    private readonly TestExecutionState _state;
    private readonly IServiceProvider _serviceProvider;
    private readonly ControllerJob _job;
    private readonly string? _workerKey;
    private readonly TimeSpan _connectionTimeout;
    private readonly object _lock = new();
    private readonly List<WorkerSlot> _slots = new();
    private readonly HashSet<string> _completedScenarios = new();
    private readonly HashSet<string> _inputDataWarnings = new();
    private readonly Dictionary<string, ScenarioStatsState> _previousPointStates = new();
    private DateTime _lastPointsUtc;
    private DateTime _lastExchangeWarningUtc;
    private CockpitConnection? _cockpit;
    private string? _pendingCommand;
    private bool _isStopRequestSeen;
    private bool _isStopRequested;
    private string? _failure;
    private bool _isStarted;
    private bool _isMeasurementStarted;
    private string _liveState = "Preparing";

    public ControllerRun(TestExecutionState state, IServiceProvider serviceProvider)
    {
        _state = state;
        _serviceProvider = serviceProvider;

        var cluster = state.TestSession.Cluster;
        if (!cluster.Args.TryGetValue(LoadChildProtocol.ControllerArgument, out var controllerFile))
            throw new InvalidOperationException("The controller was started without --cluster-controller.");

        _job = ProtocolJson.Deserialize<ControllerJob>(File.ReadAllText(controllerFile));
        _workerKey = Environment.GetEnvironmentVariable(CockpitConnection.WorkerKeyVariable);
        if (string.IsNullOrWhiteSpace(_job.CockpitUrl) || string.IsNullOrWhiteSpace(_workerKey))
            throw new InvalidOperationException($"The controller was started without a Cockpit address or {CockpitConnection.WorkerKeyVariable}; update the worker app.");

        _connectionTimeout = _job.ConnectionTimeoutSeconds > 0
            ? TimeSpan.FromSeconds(_job.ConnectionTimeoutSeconds)
            : ClusterProtocol.DefaultConnectionTimeout;
    }

    public string Result { get; private set; } = RunResults.Failed;
    public string? Error { get; private set; }

    public async Task Run()
    {
        var startedUtc = DateTime.UtcNow;
        foreach (var collector in _state.LoadCollectors.Values)
            collector.MarkPhaseAsStarted(LoadTestPhase.Init, startedUtc);

        _cockpit = new CockpitConnection(_job.CockpitUrl!, CockpitConnection.WorkerKeyHeader, _workerKey!.Trim(), "The worker app gives the controller its own Cockpit address and worker key.");

        // The first call marks the run as running.
        await Exchange(new ControllerPollRequest());
        _ = Task.Run(ReadInput);

        var isBeforeOnceDone = false;
        try
        {
            await OnceHooks.RunBefore(_state, _serviceProvider);
            isBeforeOnceDone = true;

            await ClaimWorkers();
            await Loop();
        }
        catch (Exception ex)
        {
            _state.TestSession.Logger.LogError(ex, $"Run {_job.RunId} failed.");
            Fail(ex.Message);
        }

        // After a failure the claimed workers still get "stop", and the controller waits for their final status.
        if (_slots.Count > 0 && !AllFinal())
            await Loop();

        await Finish(isBeforeOnceDone);

        // A stopped run ends like a local stop: the reports are written, then the test is cancelled.
        if (Result == RunResults.Stopped)
            await _state.RequestStop();
    }

    // All or nothing, decided by Cockpit in one go: it knows which workers are free. The controller's own
    // machine is taken last, so it carries no load when others are free.
    private async Task ClaimWorkers()
    {
        var request = new ClaimWorkersRequest();
        for (var index = 0; index < _job.Workers; index++)
            request.RunFiles.Add(JsonSerializer.SerializeToElement(CreateRunFile(index), ClusterJson.Options));

        // Workers of a run that has just ended are free within seconds, so the claim tries again a little.
        var deadline = DateTime.UtcNow + PoolMessages.FinishingWait;
        var reply = await _cockpit!.Post<ClaimWorkersReply>(CockpitPaths.ControllerClaim(_job.RunId), request, CancellationToken.None);
        while (!reply.Accepted && reply.Free + reply.Busy.Count(worker => worker.BusyRunEnded) >= _job.Workers && DateTime.UtcNow < deadline)
        {
            await Task.Delay(TimeSpan.FromSeconds(1));
            reply = await _cockpit.Post<ClaimWorkersReply>(CockpitPaths.ControllerClaim(_job.RunId), request, CancellationToken.None);
        }

        if (!reply.Accepted || reply.Workers.Count != _job.Workers)
            throw new InvalidOperationException(PoolMessages.NotEnoughWorkers(_job.Pool, reply.Free, _job.Workers, reply.Busy, reply.Refusals));

        lock (_lock)
        {
            for (var index = 0; index < reply.Workers.Count; index++)
                _slots.Add(new WorkerSlot(reply.Workers[index], index));
        }

        _state.TestSession.Logger.LogInformation($"Run {_job.RunId}: claimed {string.Join(", ", _slots.Select(slot => slot.Info.WorkerId))}.");
    }

    private LoadChildRunSettings CreateRunFile(int workerIndex)
    {
        var onceData = new Dictionary<string, JsonElement>();
        foreach (var scenario in _state.Scenarios)
        {
            if (scenario.OnceDataJson != null)
                onceData[scenario.Name] = JsonDocument.Parse(scenario.OnceDataJson).RootElement.Clone();
        }

        return new LoadChildRunSettings
        {
            Protocol = LoadChildProtocol.Version,
            RunId = _job.RunId,
            TestName = _job.TestName,
            WorkerIndex = workerIndex,
            WorkerCount = _job.Workers,
            Seed = _job.Seed,
            TargetEnvironment = _job.TargetEnvironment,
            OnceData = onceData
        };
    }

    // One tick every 2 s until every worker has a final status. The first call brings the statuses; the
    // second, after the merge, the merged stats and the command the merge may have changed.
    private async Task Loop()
    {
        using var timer = new PeriodicTimer(ClusterProtocol.StatusInterval);
        while (true)
        {
            await Exchange(new ControllerPollRequest { LoadCommand = CurrentCommand() });
            CheckWorkers();

            var allFinal = AllFinal();
            Progress();
            var isMerged = await TickStats(allFinal);

            _state.Workers = WorkerSummaries();
            var request = new ControllerPollRequest { LoadCommand = CurrentCommand(), Live = BuildLive(_liveState) };
            if (isMerged)
                request.Points = BuildPoints(allFinal);
            await Exchange(request);

            if (allFinal)
                return;

            await timer.WaitForNextTickAsync();
        }
    }

    // A command stays until every status shows the new state. Cockpit keeps the last one it got, and a load
    // child ignores a command it already has, so null (nothing new) is safe.
    private string? CurrentCommand()
    {
        lock (_lock)
            return _pendingCommand != null && !IsCommandDone(_pendingCommand) ? _pendingCommand : null;
    }

    // Statuses in, a stop request in; a failed call only costs this tick, and a long silence makes the
    // workers lost, like a silent worker would.
    private async Task<bool> Exchange(ControllerPollRequest request)
    {
        ControllerPollReply reply;
        try
        {
            reply = await _cockpit!.Post<ControllerPollReply>(CockpitPaths.ControllerPoll(_job.RunId), request, CancellationToken.None);
        }
        catch (CockpitException ex)
        {
            if (DateTime.UtcNow - _lastExchangeWarningUtc > TimeSpan.FromSeconds(30))
            {
                _lastExchangeWarningUtc = DateTime.UtcNow;
                _state.TestSession.Logger.LogWarning($"Run {_job.RunId}: {ex.Message}");
            }

            return false;
        }

        var now = DateTime.UtcNow;
        lock (_lock)
        {
            foreach (var worker in reply.Workers)
            {
                var slot = _slots.FirstOrDefault(s => s.Index == worker.Index);
                if (slot == null || worker.Status == null || worker.StatusSeq <= slot.StatusSeq)
                    continue;

                LoadChildStatus? child = null;
                try
                {
                    if (worker.Status.Status != null)
                        child = ClusterJson.Deserialize<LoadChildStatus>(worker.Status.Status.Value.GetRawText());
                }
                catch (JsonException)
                {
                    continue;
                }

                var age = worker.StatusAgeMs;
                if (age == null)
                    age = 0;
                slot.Record(worker.Status, child, now - TimeSpan.FromMilliseconds(age.Value), worker.StatusSeq);
            }
        }

        if (reply.StopRequestedBy != null && !_isStopRequestSeen)
        {
            _isStopRequestSeen = true;
            RequestStop($"stop requested by {reply.StopRequestedBy}");
        }

        return true;
    }

    private bool IsCommandDone(string command)
    {
        return command switch
        {
            RunCommands.Start => _slots.All(slot => slot.ChildState is not (null or LoadChildStates.Preparing or LoadChildStates.Ready)),
            RunCommands.StartMeasurement => _slots.All(slot => slot.ChildState is not (null or LoadChildStates.Warmup or LoadChildStates.WarmupDone)),
            RunCommands.Stop => _slots.All(IsFinal),
            _ => true
        };
    }

    private void CheckWorkers()
    {
        var now = DateTime.UtcNow;
        List<string> problems = new();
        lock (_lock)
        {
            foreach (var slot in _slots)
            {
                if (IsFinal(slot) && (slot.Child == null || slot.Child.State != LoadChildStates.Failed) && (slot.Latest == null || slot.Latest.Error == null))
                    continue;

                if (now - slot.LastStatusUtc > _connectionTimeout)
                {
                    slot.IsLost = true;
                    problems.Add($"Lost load worker {slot.Info.WorkerId}: no status for {_connectionTimeout.TotalSeconds:0} s. The report is incomplete.");
                }
                else if (slot.Child != null && slot.Child.State == LoadChildStates.Failed)
                    problems.Add($"Load worker {slot.Info.WorkerId} failed: {slot.Child.Error}");
                else if (slot.Latest != null && slot.Latest.Error != null)
                    problems.Add($"Load worker {slot.Info.WorkerId} failed: {slot.Latest.Error}");
                else if (slot.Latest != null && slot.Latest.Phase == WorkerJobPhases.Exited && (slot.Child == null || slot.Child.State is not (LoadChildStates.Completed or LoadChildStates.Stopped)))
                    problems.Add($"The load child on {slot.Info.WorkerId} exited with code {slot.Latest.ExitCode}.{Environment.NewLine}{slot.Latest.OutputTail}");
            }
        }

        foreach (var problem in problems)
            Fail(problem);
    }

    private void Progress()
    {
        var now = DateTime.UtcNow;
        lock (_lock)
        {
            if (_isStopRequested || _slots.Count == 0)
                return;

            if (!_isStarted && _slots.All(slot => slot.ChildState == LoadChildStates.Ready))
            {
                InitCompleted(now);
                _isStarted = true;
                _pendingCommand = RunCommands.Start;
                _liveState = "Running";

                foreach (var scenario in _state.Scenarios)
                {
                    if (HasWarmup(scenario))
                        _state.LoadCollectors[scenario.Name].MarkPhaseAsStarted(LoadTestPhase.Warmup, now);
                    else
                        StartMeasurement(scenario, now);
                }
            }

            // The only sync point: measurement starts on every worker once all have finished warmup.
            if (_isStarted && _pendingCommand == RunCommands.Start && _state.Scenarios.Any(HasWarmup)
                && _slots.All(slot => slot.ChildState is LoadChildStates.WarmupDone or LoadChildStates.Measuring || IsFinal(slot)))
            {
                _pendingCommand = RunCommands.StartMeasurement;
                foreach (var scenario in _state.Scenarios.Where(HasWarmup))
                {
                    _state.LoadCollectors[scenario.Name].MarkPhaseAsCompleted(LoadTestPhase.Warmup, now);
                    StartMeasurement(scenario, now);
                }
            }
        }
    }

    private void StartMeasurement(Scenario scenario, DateTime now)
    {
        _state.LoadCollectors[scenario.Name].MarkPhaseAsStarted(LoadTestPhase.Measurement, now);
        if (!_isMeasurementStarted)
        {
            _isMeasurementStarted = true;
            _state.TestResult.MarkPhaseAsStarted(StandardTestPhase.Execute, now);
        }
    }

    private static bool HasWarmup(Scenario scenario) => scenario.SimulationsInternal.Any(simulation => simulation.IsWarmup);

    // The load children send the simulations unsplit with Ready, so the report shows the total load.
    private void InitCompleted(DateTime now)
    {
        var stats = _slots.Select(slot => slot.Child != null ? slot.Child.Stats : null).FirstOrDefault(s => s != null);
        foreach (var scenario in _state.Scenarios)
        {
            ScenarioStatsState? scenarioStats = null;
            if (stats != null)
                scenarioStats = stats.Scenarios.FirstOrDefault(s => s.Name == scenario.Name);
            if (scenario.SimulationsInternal.Count == 0 && scenarioStats != null)
            {
                foreach (var simulation in scenarioStats.Simulations)
                    scenario.SimulationsInternal.Add(new DescribedLoadConfiguration(simulation.Description, simulation.IsWarmup));
            }

            var collector = _state.LoadCollectors[scenario.Name];
            collector.CacheSimulationDescriptions(scenario);
            collector.MarkPhaseAsCompleted(LoadTestPhase.Init, now);
        }

        _state.TestResult.MarkPhaseAsCompleted(StandardTestPhase.Init, now);
    }

    // A tick counts only when every worker sent a status since the last one; otherwise one stale worker
    // would make a false dip that could fail AssertWhileRunning or distort the charts.
    private async Task<bool> TickStats(bool allFinal)
    {
        bool isComplete;
        lock (_lock)
        {
            if (_slots.Count == 0 || _slots.Any(slot => slot.Child == null || slot.Child.Stats == null))
                return false;

            isComplete = allFinal || _slots.All(slot => slot.IsFresh || IsFinal(slot));
            if (!isComplete)
                return false;

            foreach (var slot in _slots)
                slot.IsFresh = false;

            Merge();
        }

        var now = DateTime.UtcNow;
        foreach (var scenario in _state.Scenarios)
        {
            var collector = _state.LoadCollectors[scenario.Name];
            var result = collector.GetCurrentResult(true);

            if (!_completedScenarios.Contains(scenario.Name) && result.MeasurementStartTime != default && AllWorkersCompleted(scenario.Name))
            {
                _completedScenarios.Add(scenario.Name);
                collector.MarkPhaseAsCompleted(LoadTestPhase.Measurement, now);
            }

            if (!_isStopRequested)
                RunAsserts(scenario, collector, result);

            if (result.MeasurementStartTime != default && _state.TestSession.Cluster.SinksEnabled)
            {
                var handler = _serviceProvider.GetRequiredService<ExecuteScenarioMessageHandler>();
                await handler.WriteToSinksAndSnapshotCollector(_state, scenario, () => collector.GetCurrentResult(true), true);
            }
        }

        return true;
    }

    private bool AllWorkersCompleted(string scenarioName)
    {
        lock (_lock)
        {
            return _slots.All(slot =>
            {
                var scenarioStats = FindScenario(slot, scenarioName);
                return (scenarioStats != null && scenarioStats.Phase == ScenarioPhase.Completed)
                    || (slot.Child != null && LoadChildStates.IsFinal(slot.Child.State));
            });
        }
    }

    private static ScenarioStatsState? FindScenario(WorkerSlot slot, string scenarioName)
    {
        if (slot.Child == null || slot.Child.Stats == null)
            return null;

        return slot.Child.Stats.Scenarios.FirstOrDefault(s => s.Name == scenarioName);
    }

    private void Merge()
    {
        foreach (var scenario in _state.Scenarios)
        {
            var states = _slots
                .Select(slot => FindScenario(slot, scenario.Name))
                .Where(s => s != null)
                .Cast<ScenarioStatsState>()
                .ToList();

            if (states.Count == 0)
                continue;

            // Each worker measures with its own stopwatch, so clock skew between machines does not matter.
            var duration = TimeSpan.FromTicks(states.Max(s => s.MeasurementDurationTicks));
            _state.LoadCollectors[scenario.Name].LoadMerged(states, duration);
            CheckInputData(scenario.Name, states);
        }
    }

    private void CheckInputData(string scenarioName, List<ScenarioStatsState> states)
    {
        var fingerprints = states.Where(s => s.InputDataCount != null).Select(s => $"{s.InputDataCount}/{s.InputDataHash}").Distinct().ToList();
        if (fingerprints.Count <= 1 || !_inputDataWarnings.Add(scenarioName))
            return;

        var counts = string.Join(", ", states.Select(s => s.InputDataCount));
        AddWarning($"The input data of scenario '{scenarioName}' differs between workers (counts {counts}). Each worker loads the list itself; use a stable order (e.g. ORDER BY) or BeforeScenarioOnce.");
    }

    private void RunAsserts(Scenario scenario, ScenarioLoadCollector collector, ScenarioLoadResult result)
    {
        var isWarmingUp = result.WarmupStartTime != default && result.WarmupEndTime == default;
        var isMeasuring = result.MeasurementStartTime != default && !result.IsCompleted;

        try
        {
            if (isWarmingUp && scenario.AssertWhileWarmingUpAction != null)
            {
                var context = ContextFactory.CreateScenarioContext(_state.TestSession, _serviceProvider, _state.TestFramework, "AssertWhileWarmingUp", CancellationToken.None, scenario);
                scenario.AssertWhileWarmingUpAction(context, collector.GetWarmupStats());
            }
        }
        catch (Exception ex)
        {
            collector.SetAssertWhileWarmingUpException(ex);
            AssertFailed(collector, ex);
            return;
        }

        try
        {
            if (isMeasuring && scenario.AssertWhileRunningAction != null)
            {
                var context = ContextFactory.CreateScenarioContext(_state.TestSession, _serviceProvider, _state.TestFramework, "AssertWhileRunning", CancellationToken.None, scenario);
                scenario.AssertWhileRunningAction(context, new AssertScenarioStats(result));
            }
        }
        catch (Exception ex)
        {
            collector.SetAssertWhileRunningException(ex);
            AssertFailed(collector, ex);
        }
    }

    private void AssertFailed(ScenarioLoadCollector collector, Exception exception)
    {
        _state.ExecutionStatus = ExecutionStatus.Stopped;
        _state.ExecutionStoppedReason = exception;
        _state.TestResult.Status = TestStatus.Failed;
        if (_state.FirstException == null)
            _state.FirstException = exception;
        collector.SetStatus(TestStatus.Failed);
        StopAll(isFailure: true);
    }

    private void Fail(string reason)
    {
        lock (_lock)
        {
            if (_failure == null)
                _failure = reason;
        }

        StopAll(isFailure: true);
    }

    public void RequestStop(string reason)
    {
        _state.TestSession.Logger.LogInformation($"Run {_job.RunId}: {reason}");
        StopAll(isFailure: false);
    }

    private void StopAll(bool isFailure)
    {
        lock (_lock)
        {
            if (_isStopRequested)
                return;

            _isStopRequested = true;
            _pendingCommand = RunCommands.Stop;
            _liveState = "Stopping";
            if (!isFailure)
                _state.ExecutionStatus = ExecutionStatus.Stopped;
        }
    }

    private bool AllFinal()
    {
        lock (_lock)
            return _slots.Count > 0 && _slots.All(IsFinal);
    }

    private static bool IsFinal(WorkerSlot slot)
    {
        return slot.IsLost
            || (slot.Latest != null && slot.Latest.Phase == WorkerJobPhases.Exited)
            || (slot.Child != null && LoadChildStates.IsFinal(slot.Child.State));
    }

    private async Task Finish(bool isBeforeOnceDone)
    {
        var now = DateTime.UtcNow;
        lock (_lock)
        {
            if (_slots.Any(slot => slot.Child != null && slot.Child.Stats != null))
                Merge();
        }

        foreach (var scenario in _state.Scenarios)
        {
            var collector = _state.LoadCollectors[scenario.Name];
            var result = collector.GetCurrentResult(true);
            if (result.MeasurementStartTime != default && !result.IsCompleted)
                collector.MarkPhaseAsCompleted(LoadTestPhase.Measurement, now);
        }

        if (_isMeasurementStarted)
            _state.TestResult.MarkPhaseAsCompleted(StandardTestPhase.Execute, now);

        if (_failure == null && !_isStopRequested && _isStarted)
            ExecutionManager.AssertWhenDone(_state, _serviceProvider);

        var cleanupStarted = DateTime.UtcNow;
        _state.TestResult.MarkPhaseAsStarted(StandardTestPhase.Cleanup, cleanupStarted);
        foreach (var collector in _state.LoadCollectors.Values)
            collector.MarkPhaseAsStarted(LoadTestPhase.Cleanup, cleanupStarted);

        // Also after a stop or failure, so data created in BeforeScenarioOnce is cleaned up.
        if (isBeforeOnceDone)
        {
            try
            {
                await OnceHooks.RunAfter(_state, _serviceProvider);
            }
            catch (Exception ex)
            {
                lock (_lock)
                {
                    if (_failure == null)
                        _failure = "AfterScenarioOnce failed: " + ex.Message;
                }
            }
        }

        var cleanupEnded = DateTime.UtcNow;
        foreach (var collector in _state.LoadCollectors.Values)
            collector.MarkPhaseAsCompleted(LoadTestPhase.Cleanup, cleanupEnded);
        _state.TestResult.MarkPhaseAsCompleted(StandardTestPhase.Cleanup, cleanupEnded);

        _state.Workers = WorkerSummaries();
        AddFinalWarnings();

        // The workers may clean up now; the run's end releases them too if this call is lost.
        for (var attempt = 0; attempt < 3; attempt++)
        {
            if (await Exchange(new ControllerPollRequest { Release = true }))
                break;

            await Task.Delay(TimeSpan.FromSeconds(1));
        }

        if (_failure != null)
        {
            _state.TestResult.Status = TestStatus.Failed;
            if (_state.FirstException == null)
                _state.FirstException = new RemoteRunException(_failure);
        }

        if (_state.TestResult.Status == TestStatus.Failed || _state.FirstException != null)
        {
            Result = RunResults.Failed;
            Error = _failure;
            if (Error == null && _state.FirstException != null)
                Error = _state.FirstException.Message;
        }
        else if (_isStopRequested)
        {
            Result = RunResults.Stopped;
        }
        else
        {
            Result = RunResults.Passed;
        }

        _liveState = "Ending";
    }

    private void AddFinalWarnings()
    {
        lock (_lock)
        {
            foreach (var slot in _slots)
            {
                if (slot.LongestGap > GapWarning && !slot.IsLost)
                    AddWarning($"{slot.Info.WorkerId}: no status for {slot.LongestGap.TotalSeconds:0} s during the run; its counts were recovered from its next status, but the timeline has a gap.");
                if (slot.CpuMax > WorkerSummary.CpuWarningPercent)
                    AddWarning($"{slot.Info.WorkerId}: CPU reached {slot.CpuMax:0} % (above {WorkerSummary.CpuWarningPercent:0} %). Durations may be too high because the load machine was busy; use more workers or less load per worker.");
            }
        }
    }

    private void AddWarning(string warning)
    {
        lock (_state.Warnings)
        {
            if (!_state.Warnings.Contains(warning))
                _state.Warnings.Add(warning);
        }
    }

    private List<WorkerSummary> WorkerSummaries()
    {
        lock (_lock)
        {
            return _slots.Select(slot =>
            {
                LoadStatsState? stats = null;
                if (slot.Child != null)
                    stats = slot.Child.Stats;

                HealthState? health = null;
                List<ScenarioStatsState>? scenarios = null;
                if (stats != null)
                {
                    health = stats.Health;
                    scenarios = stats.Scenarios;
                }
                if (scenarios == null)
                    scenarios = new List<ScenarioStatsState>();

                var state = "Claimed";
                if (slot.IsLost)
                    state = RunResults.Lost;
                else if (slot.ChildState != null)
                    state = slot.ChildState;
                else if (slot.Latest != null && slot.Latest.Phase != null)
                    state = slot.Latest.Phase;

                return new WorkerSummary
                {
                    Id = slot.Info.WorkerId,
                    State = state,
                    Os = slot.Info.Os,
                    CpuCores = slot.Info.CpuCores,
                    Version = slot.Info.Version,
                    CpuPercent = health != null ? health.CpuPercent : 0,
                    MemoryMb = health != null ? health.MemoryMb : 0,
                    IterationStartLagMs = health != null ? health.IterationStartLagMs : 0,
                    Iterations = scenarios.Sum(s => (long) s.Ok.Count + s.Failed.Count),
                    Errors = scenarios.Sum(s => (long) s.Failed.Count),
                    CpuAveragePercent = slot.CpuSamples > 0 ? Math.Round(slot.CpuSum / slot.CpuSamples, 1) : 0,
                    CpuMaxPercent = slot.CpuMax,
                    MemoryMaxMb = slot.MemoryMax,
                    IterationStartLagMaxMs = slot.LagMax,
                    LogFolder = slot.Child != null ? slot.Child.ResultsDirectory : null
                };
            }).ToList();
        }
    }

    public LiveMessage BuildLive(string state)
    {
        var message = new LiveMessage
        {
            RunId = _job.RunId,
            ControllerUtc = DateTime.UtcNow,
            State = state,
            Result = state == RunStates.Ended ? Result : null,
            Error = state == RunStates.Ended ? Error : null,
            Workers = _state.Workers
        };

        foreach (var scenario in _state.Scenarios)
        {
            var collector = _state.LoadCollectors[scenario.Name];
            message.Scenarios.Add(new LiveScenario { Stats = collector.GetState(), Times = collector.GetTimes() });
        }

        lock (_state.Warnings)
            message.Warnings = new List<string>(_state.Warnings);

        return message;
    }

    // The same per-interval points a sink gets, for Cockpit's cluster page. Its own previous states, so the
    // sinks' intervals stay as they are. Null when there is nothing to send yet.
    private LivePointsMessage? BuildPoints(bool isFinal)
    {
        var now = DateTime.UtcNow;
        if (!isFinal && now - _lastPointsUtc < MinPointsInterval)
            return null;
        _lastPointsUtc = now;
        var message = new LivePointsMessage { RunId = _job.RunId, TestName = _job.TestName, ControllerUtc = now };
        long timeMs = 0;
        foreach (var scenario in _state.Scenarios)
        {
            var collector = _state.LoadCollectors[scenario.Name];
            var result = collector.GetCurrentResult(true);
            if (result.MeasurementStartTime == default)
                continue;

            var current = collector.GetState();
            _previousPointStates.TryGetValue(scenario.Name, out var previous);
            _previousPointStates[scenario.Name] = current;

            var scenarioTimeMs = (long) (now - result.MeasurementStartTime).TotalMilliseconds;
            timeMs = Math.Max(timeMs, scenarioTimeMs);
            foreach (var point in IntervalStats.Points(current, previous, scenarioTimeMs))
            {
                message.Points.Add(new LivePoint
                {
                    Path = point.Path,
                    Phase = point.Phase,
                    TMs = point.TimeMs,
                    OkCount = point.OkCount,
                    FailedCount = point.FailedCount,
                    DurationMeanMs = point.MeanMs,
                    DurationP50Ms = point.P50Ms,
                    DurationP90Ms = point.P90Ms,
                    DurationP95Ms = point.P95Ms,
                    DurationP99Ms = point.P99Ms,
                    VirtualUsers = point.VirtualUsers,
                    StatusCodes = point.StatusCodes
                });
            }

            message.Errors.AddRange(LiveErrors.Of(result));
            var stoppedReason = LiveErrors.StoppedReason(result);
            if (stoppedReason != null && message.StoppedReason == null)
                message.StoppedReason = stoppedReason;
        }

        if (message.Points.Count == 0)
            return null;

        foreach (var worker in _state.Workers)
        {
            message.Generators.Add(new LiveGenerator
            {
                GeneratorName = worker.Id,
                State = worker.State,
                TMs = timeMs,
                CpuPercent = worker.CpuPercent,
                MemoryMb = worker.MemoryMb,
                StartLagMs = worker.IterationStartLagMs
            });
        }

        return message;
    }

    // Stores the report files (no logs) in Cockpit under the run id, then ends the run; attached clients
    // download the report when they see the run end.
    public async Task StoreReportAndEnd(TestSession session)
    {
        var summary = BuildLive(RunStates.Ended);
        var zipPath = Path.Combine(Path.GetTempPath(), $"testfuzn-report-{_job.RunId}-{Guid.NewGuid():N}.zip");
        try
        {
            await using (var zip = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var archive = new ZipArchive(zip, ZipArchiveMode.Create))
            {
                var root = session.TestsResultsDirectory;
                foreach (var path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                {
                    var name = Path.GetFileName(path);
                    if (name.EndsWith(".log", StringComparison.OrdinalIgnoreCase) || name == ".testfuzn")
                        continue;

                    archive.CreateEntryFromFile(path, Path.GetRelativePath(root, path).Replace('\\', '/'), CompressionLevel.Fastest);
                }

                var entry = archive.CreateEntry(ClusterProtocol.RunSummaryFileName, CompressionLevel.Fastest);
                await using var writer = new StreamWriter(entry.Open());
                await writer.WriteAsync(ProtocolJson.Serialize(summary));
            }

            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    await using var upload = File.OpenRead(zipPath);
                    await _cockpit!.Put(CockpitPaths.ControllerReport(_job.RunId), upload, CancellationToken.None);
                    break;
                }
                catch (CockpitException ex) when (attempt < 5)
                {
                    _state.TestSession.Logger.LogWarning($"Run {_job.RunId}: could not store the report in Cockpit, trying again: {ex.Message}");
                    await Task.Delay(TimeSpan.FromSeconds(2 * attempt));
                }
            }
        }
        catch (Exception ex)
        {
            Result = RunResults.Failed;
            if (Error == null)
                Error = $"Could not store the report in Cockpit: {ex.Message}";
        }
        finally
        {
            try
            {
                File.Delete(zipPath);
            }
            catch (IOException)
            {
            }
        }

        // The client waits for the end, so it is sent again if Cockpit was briefly unreachable.
        var end = new EndRunRequest { Result = Result, Error = Error, Live = BuildLive(RunStates.Ended) };
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await _cockpit!.Post(CockpitPaths.ControllerEnd(_job.RunId), end, CancellationToken.None);
                break;
            }
            catch (CockpitException ex) when (attempt < 5)
            {
                _state.TestSession.Logger.LogWarning($"Run {_job.RunId}: could not end the run in Cockpit, trying again: {ex.Message}");
                await Task.Delay(TimeSpan.FromSeconds(2));
            }
            catch (CockpitException ex)
            {
                _state.TestSession.Logger.LogError($"Run {_job.RunId}: could not end the run in Cockpit: {ex.Message}");
                break;
            }
        }

        _cockpit!.Dispose();
    }

    // The worker app holds stdin open: "stop" or a closed stdin means the worker app is stopping or gone.
    private async Task ReadInput()
    {
        try
        {
            string? line;
            while ((line = await Console.In.ReadLineAsync()) != null)
            {
                if (line.Trim() == RunCommands.Stop)
                    RequestStop("the worker app asked the controller to stop");
            }
        }
        catch (Exception)
        {
        }

        RequestStop("the worker app is gone (stdin closed)");
    }

    private sealed class WorkerSlot
    {
        private DateTime? _previousStatusUtc;

        public WorkerSlot(WorkerInfo info, int index)
        {
            Info = info;
            Index = index;
            LastStatusUtc = DateTime.UtcNow;
        }

        public WorkerInfo Info { get; }
        public int Index { get; }
        public WorkerStatusMessage? Latest { get; private set; }
        public LoadChildStatus? Child { get; private set; }
        public DateTime LastStatusUtc { get; private set; }
        public long StatusSeq { get; private set; }
        public bool IsFresh { get; set; }
        public bool IsLost { get; set; }
        public double CpuSum { get; private set; }
        public int CpuSamples { get; private set; }
        public double CpuMax { get; private set; }
        public double MemoryMax { get; private set; }
        public double LagMax { get; private set; }
        public TimeSpan LongestGap { get; private set; }

        public string? ChildState
        {
            get
            {
                var child = Child;
                if (child == null)
                    return null;

                return child.State;
            }
        }

        public void Record(WorkerStatusMessage status, LoadChildStatus? child, DateTime receivedUtc, long seq)
        {
            StatusSeq = seq;
            if (_previousStatusUtc != null && receivedUtc - _previousStatusUtc > LongestGap)
                LongestGap = receivedUtc - _previousStatusUtc.Value;

            _previousStatusUtc = receivedUtc;
            LastStatusUtc = receivedUtc;
            Latest = status;
            IsFresh = true;

            if (child == null)
                return;

            Child = child;
            HealthState? health = null;
            if (child.Stats != null)
                health = child.Stats.Health;
            if (health == null || child.State is LoadChildStates.Preparing or LoadChildStates.Ready)
                return;

            CpuSum += health.CpuPercent;
            CpuSamples++;
            CpuMax = Math.Max(CpuMax, health.CpuPercent);
            MemoryMax = Math.Max(MemoryMax, health.MemoryMb);
            LagMax = Math.Max(LagMax, health.IterationStartLagMs);
        }
    }
}
