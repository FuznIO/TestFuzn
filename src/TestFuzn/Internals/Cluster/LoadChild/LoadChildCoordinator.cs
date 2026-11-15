using Fuzn.TestFuzn.Internals.State;

namespace Fuzn.TestFuzn.Internals.Cluster.LoadChild;

// Runs inside a load child: reads the worker app's commands from stdin and writes status.json. The child
// has no network code; the worker app passes both on through Cockpit.
internal sealed class LoadChildCoordinator : IDisposable
{
    private static readonly TimeSpan StatusInterval = TimeSpan.FromSeconds(1);

    private readonly object _lock = new();
    private readonly object _writeLock = new();
    private readonly string _statusPath;
    private readonly TextReader _input;
    private readonly HealthSampler _healthSampler = new();
    private readonly TaskCompletionSource _start = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _startMeasurement = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _stopRequested = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly CancellationTokenSource _statusLoopStop = new();
    private readonly HashSet<string> _pendingWarmup = new();
    private TestExecutionState? _state;
    private string _phase = LoadChildStates.Preparing;
    private string? _error;
    private bool _isFinished;

    private LoadChildCoordinator(LoadChildRunSettings settings, string statusPath, TextReader input)
    {
        Settings = settings;
        _statusPath = statusPath;
        _input = input;
    }

    public LoadChildRunSettings Settings { get; }

    public string? ResultsDirectory { get; set; }

    public string Phase
    {
        get
        {
            lock (_lock)
                return _phase;
        }
    }

    public static LoadChildCoordinator FromRunFile(string runFilePath, TextReader input)
    {
        if (!File.Exists(runFilePath))
            throw new FileNotFoundException($"The run file '{runFilePath}' does not exist.", runFilePath);

        var settings = ClusterJson.Deserialize<LoadChildRunSettings>(File.ReadAllText(runFilePath));
        if (settings.Protocol != LoadChildProtocol.Version)
            throw new InvalidOperationException($"run.json uses protocol {settings.Protocol}; this TestFuzn version understands protocol {LoadChildProtocol.Version}. Update the worker app or TestFuzn so they match.");

        var directory = Path.GetDirectoryName(Path.GetFullPath(runFilePath))!;
        return new LoadChildCoordinator(settings, Path.Combine(directory, LoadChildProtocol.StatusFileName), input);
    }

    public void Start()
    {
        WriteStatus();
        _ = Task.Run(ReadInput);
        _ = Task.Run(WriteStatusLoop);
    }

    public void Attach(TestExecutionState state)
    {
        bool isStopRequested;
        lock (_lock)
        {
            _state = state;
            isStopRequested = _stopRequested.Task.IsCompleted;
        }

        if (isStopRequested)
            _ = state.RequestStop();
    }

    public async Task ReadyAndWaitForStart()
    {
        lock (_lock)
        {
            if (_state == null)
                throw new InvalidOperationException("Attach the test execution state before reporting Ready.");

            foreach (var scenario in _state.Scenarios)
            {
                if (scenario.SimulationsInternal.Any(simulation => simulation.IsWarmup))
                    _pendingWarmup.Add(scenario.Name);
            }

            SetPhase(LoadChildStates.Ready);
        }

        WriteStatus();
        await WaitFor(_start.Task);

        lock (_lock)
            SetPhase(_pendingWarmup.Count > 0 ? LoadChildStates.Warmup : LoadChildStates.Measuring);

        WriteStatus();
    }

    // The only sync point during a run: every worker finishes its warmup before any of them measures.
    public async Task WarmupDoneAndWaitForMeasurement(string scenarioName)
    {
        lock (_lock)
        {
            _pendingWarmup.Remove(scenarioName);
            if (_pendingWarmup.Count == 0)
                SetPhase(LoadChildStates.WarmupDone);
        }

        WriteStatus();
        await WaitFor(_startMeasurement.Task);

        lock (_lock)
        {
            if (_phase == LoadChildStates.WarmupDone)
                SetPhase(LoadChildStates.Measuring);
        }

        WriteStatus();
    }

    public void Finish(Exception? failure)
    {
        lock (_lock)
        {
            if (_isFinished)
                return;

            _isFinished = true;
            var isStopped = _stopRequested.Task.IsCompleted
                || (_state != null && _state.ExecutionStatus == ExecutionStatus.Stopped && (_state.TestResult == null || _state.TestResult.Status != TestStatus.Failed));

            if (failure != null && !(failure is OperationCanceledException && isStopped))
            {
                _phase = LoadChildStates.Failed;
                _error = failure.GetType().Name + ": " + failure.Message;
            }
            else if (isStopped)
            {
                _phase = LoadChildStates.Stopped;
            }
            else
            {
                _phase = LoadChildStates.Completed;
            }
        }

        _statusLoopStop.Cancel();
        WriteStatus();
    }

    public void RequestStop()
    {
        TestExecutionState? state;
        lock (_lock)
        {
            if (_isFinished)
                return;

            state = _state;
            SetPhase(LoadChildStates.Stopping);
            _stopRequested.TrySetResult();
        }

        if (state != null)
            _ = state.RequestStop();

        WriteStatus();
    }

    private async Task WaitFor(Task command)
    {
        await Task.WhenAny(command, _stopRequested.Task);
        if (_stopRequested.Task.IsCompleted)
        {
            var token = _state != null ? _state.CancellationToken : new CancellationToken(true);
            throw new OperationCanceledException("The run was stopped.", token);
        }
    }

    private void SetPhase(string phase)
    {
        // Stopping and the final states are never left again.
        if (_phase == LoadChildStates.Stopping || LoadChildStates.IsFinal(_phase))
            return;

        _phase = phase;
    }

    private async Task ReadInput()
    {
        try
        {
            string? line;
            while ((line = await _input.ReadLineAsync()) != null)
            {
                switch (line.Trim())
                {
                    case LoadChildProtocol.StartCommand:
                        _start.TrySetResult();
                        break;
                    case LoadChildProtocol.StartMeasurementCommand:
                        _startMeasurement.TrySetResult();
                        break;
                    case LoadChildProtocol.StopCommand:
                        RequestStop();
                        break;
                }
            }
        }
        catch (Exception)
        {
            // An unreadable stdin means the same as a closed one.
        }

        // stdin closed: the worker app is gone, so nobody can stop this child any more.
        RequestStop();
    }

    private async Task WriteStatusLoop()
    {
        try
        {
            while (!_statusLoopStop.IsCancellationRequested)
            {
                await Task.Delay(StatusInterval, _statusLoopStop.Token);
                WriteStatus();
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void WriteStatus()
    {
        lock (_writeLock)
        {
            LoadChildStatus status;
            lock (_lock)
            {
                status = new LoadChildStatus
                {
                    RunId = Settings.RunId,
                    State = _phase,
                    Error = _error,
                    ResultsDirectory = ResultsDirectory,
                    Stats = BuildStats(_state)
                };
            }

            var temporaryPath = _statusPath + ".tmp";
            File.WriteAllText(temporaryPath, ClusterJson.Serialize(status));

            // The worker app may hold the file open for a moment, and virus scanners lock new files on Windows.
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    File.Move(temporaryPath, _statusPath, overwrite: true);
                    return;
                }
                catch (IOException) when (attempt < 20)
                {
                    Thread.Sleep(25);
                }
                catch (UnauthorizedAccessException) when (attempt < 20)
                {
                    Thread.Sleep(25);
                }
            }
        }
    }

    private LoadStatsState BuildStats(TestExecutionState? state)
    {
        var stats = new LoadStatsState();
        stats.Health = _healthSampler.Sample(state);
        if (state == null)
            return stats;

        foreach (var scenario in state.Scenarios)
        {
            if (state.LoadCollectors.TryGetValue(scenario.Name, out var collector))
                stats.Scenarios.Add(collector.GetState());
        }

        return stats;
    }

    public void Dispose()
    {
        _statusLoopStop.Cancel();
        _statusLoopStop.Dispose();
    }
}
