using System.Runtime.ExceptionServices;
using Fuzn.TestFuzn.Contracts;
using Fuzn.TestFuzn.Internals.ConsoleOutput;
using Fuzn.TestFuzn.Internals.Execution;
using Fuzn.TestFuzn.Internals.State;
using Fuzn.TestFuzn.Internals.Terminal;

namespace Fuzn.TestFuzn.Internals.Logger;

internal class ConsoleManager
{
    private static readonly TimeSpan SampleInterval = TimeSpan.FromSeconds(1);

    private static readonly TimeSpan TickInterval = LiveDashboard.RenderInterval;

    private readonly TestExecutionState _testExecutionState;
    private readonly ConsoleWriter _consoleWriter;
    private readonly ILiveViewHost _liveViewHost;
    private readonly CancellationTokenSource _ctSource = new CancellationTokenSource();
    private Task? _liveViewLoop;
    private LiveDashboard? _liveDashboard;
    private LiveStatsWriter? _liveStatsWriter;
    private ScenarioLiveMetrics?[] _liveMetrics = Array.Empty<ScenarioLiveMetrics?>();
    private LiveMetricsSnapshot[] _liveSnapshots = Array.Empty<LiveMetricsSnapshot>();
    private DateTime _nextSampleTime;
    private bool _isLiveViewStopped;
    private Exception? _liveViewException;
    private Task? _stopRequest;

    public ConsoleManager(
        TestExecutionState testExecutionState,
        ConsoleWriter consoleWriter)
        : this(testExecutionState, consoleWriter, new ConsoleLiveViewHost())
    {
    }

    internal ConsoleManager(
        TestExecutionState testExecutionState,
        ConsoleWriter consoleWriter,
        ILiveViewHost liveViewHost)
    {
        if (testExecutionState == null)
            throw new ArgumentNullException(nameof(testExecutionState), "Test execution state cannot be null.");
        if (consoleWriter == null)
            throw new ArgumentNullException(nameof(consoleWriter), "Console writer cannot be null.");
        if (liveViewHost == null)
            throw new ArgumentNullException(nameof(liveViewHost), "Live view host cannot be null.");

        _testExecutionState = testExecutionState;
        _consoleWriter = consoleWriter;
        _liveViewHost = liveViewHost;
    }

    internal IReadOnlyList<LiveMetricsSnapshot> LiveSnapshots => _liveSnapshots;

    internal Task? StopRequest => _stopRequest;

    internal Task? LiveViewLoop => _liveViewLoop;

    public void StartRealtimeConsoleOutputIfEnabled()
    {
        if (!_testExecutionState.TestFramework.SupportsRealTimeConsoleOutput
            || _testExecutionState.TestType != TestType.Load)
            return;

        var capabilities = _liveViewHost.DetectCapabilities();

        var scenarioCount = _testExecutionState.Scenarios.Count;
        _liveMetrics = new ScenarioLiveMetrics?[scenarioCount];
        _liveSnapshots = new LiveMetricsSnapshot[scenarioCount];
        for (var index = 0; index < scenarioCount; index++)
            _liveSnapshots[index] = CreateInitSnapshot(_testExecutionState.Scenarios[index].Name, TimeSpan.Zero);

        if (!capabilities.SupportsLiveView)
        {
            var liveStatsWriter = new LiveStatsWriter(_liveViewHost.CreateTerminalWriter(), scenarioCount);
            _liveStatsWriter = liveStatsWriter;
            _liveViewLoop = Task.Run(() => RunLiveView(null, null, liveStatsWriter, _ctSource.Token));
            return;
        }

        var terminalReader = _liveViewHost.CreateTerminalReader();
        if (terminalReader == null)
            throw new InvalidOperationException("The live view host returned no terminal reader.");

        var liveDashboard = new LiveDashboard(_liveViewHost.CreateTerminalWriter(), capabilities, () => _liveSnapshots, header: BuildHeader());
        _liveDashboard = liveDashboard;
        _liveViewLoop = Task.Run(() => RunLiveView(liveDashboard, terminalReader, null, _ctSource.Token));
    }

    private async Task RunLiveView(LiveDashboard? liveDashboard, ITerminalReader? terminalReader, LiveStatsWriter? liveStatsWriter, CancellationToken cancellationToken)
    {
        try
        {
            if (liveDashboard != null)
                liveDashboard.Start();

            _nextSampleTime = _liveViewHost.UtcNow;

            while (!cancellationToken.IsCancellationRequested)
            {
                if (terminalReader != null)
                    HandleKeys(terminalReader);

                var utcNow = _liveViewHost.UtcNow;
                if (utcNow >= _nextSampleTime)
                {
                    SampleLiveMetrics(utcNow);
                    ScheduleNextSample(utcNow);

                    if (liveStatsWriter != null)
                        liveStatsWriter.WriteSample(_liveSnapshots);
                }

                if (liveDashboard != null)
                    liveDashboard.Render();

                await _liveViewHost.Delay(TickInterval, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _liveViewException = exception;
            if (liveDashboard != null)
                RestoreTerminal(liveDashboard);
        }
    }

    private void HandleKeys(ITerminalReader terminalReader)
    {
        while (terminalReader.TryReadKey(out var key))
        {
            if (!IsQuitKey(key))
                continue;

            if (_stopRequest == null)
                _stopRequest = _testExecutionState.RequestStop();
        }
    }

    private static bool IsQuitKey(ConsoleKeyInfo key)
    {
        if ((key.Modifiers & (ConsoleModifiers.Alt | ConsoleModifiers.Control)) != 0)
            return false;

        return char.ToLowerInvariant(key.KeyChar) == LiveDashboardLayout.QuitKey;
    }

    private void ScheduleNextSample(DateTime utcNow)
    {
        _nextSampleTime = _nextSampleTime + SampleInterval;
        if (_nextSampleTime <= utcNow)
            _nextSampleTime = utcNow + SampleInterval;
    }

    private void SampleLiveMetrics(DateTime utcNow)
    {
        for (var index = 0; index < _testExecutionState.Scenarios.Count; index++)
        {
            var scenario = _testExecutionState.Scenarios[index];
            var snapshot = _testExecutionState.LoadCollectors[scenario.Name].GetCurrentResult(true);

            var metrics = _liveMetrics[index];
            if (metrics == null)
            {
                if (snapshot.InitEndTime == default)
                {
                    var elapsed = TimeSpan.Zero;
                    if (snapshot.InitStartTime != default && utcNow > snapshot.InitStartTime)
                        elapsed = utcNow - snapshot.InitStartTime;

                    _liveSnapshots[index] = CreateInitSnapshot(scenario.Name, elapsed);
                    continue;
                }

                metrics = new ScenarioLiveMetrics(scenario.Name, SimulationsOf(scenario));
                _liveMetrics[index] = metrics;
            }

            metrics.Record(snapshot, utcNow);
            _liveSnapshots[index] = metrics.Current;
        }
    }

    private LoadViewHeader BuildHeader()
    {
        var configuration = _testExecutionState.TestSession.Configuration;
        if (configuration == null)
            return LoadViewHeader.Empty;

        return new LoadViewHeader
        {
            ExecutionEnvironment = TextOrEmpty(configuration.ExecutionEnvironment),
            TargetEnvironment = TextOrEmpty(configuration.TargetEnvironment)
        };
    }

    private static string TextOrEmpty(string? value)
    {
        if (value == null)
            return string.Empty;

        return value;
    }

    private static IReadOnlyList<SimulationInfo> SimulationsOf(Scenario scenario)
    {
        var simulations = new List<SimulationInfo>();
        foreach (var simulation in scenario.SimulationsInternal)
            simulations.Add(new SimulationInfo { Description = simulation.GetDescription(), IsWarmup = simulation.IsWarmup });

        return simulations;
    }

    private static LiveMetricsSnapshot CreateInitSnapshot(string scenarioName, TimeSpan elapsed)
    {
        return new LiveMetricsSnapshot
        {
            ScenarioName = scenarioName,
            Phase = LoadTestPhase.Init,
            Duration = elapsed
        };
    }

    public async Task StopRealtimeConsoleOutput()
    {
        if (_liveViewLoop == null || _isLiveViewStopped)
            return;

        _isLiveViewStopped = true;
        try
        {
            await _ctSource.CancelAsync();
            await _liveViewLoop;

            if (_stopRequest != null)
                await _stopRequest;

            if (_liveViewException == null)
            {
                SampleLiveMetrics(_liveViewHost.UtcNow);

                if (_liveStatsWriter != null)
                    _liveStatsWriter.WriteFinal(_liveSnapshots, _testExecutionState.ExecutionStatus == ExecutionStatus.Stopped);
            }
        }
        catch (Exception exception)
        {
            _liveViewException = exception;
        }
        finally
        {
            if (_liveDashboard != null)
                RestoreTerminal(_liveDashboard);
        }
    }

    private void RestoreTerminal(LiveDashboard liveDashboard)
    {
        try
        {
            liveDashboard.Dispose();
        }
        catch (Exception exception)
        {
            if (_liveViewException == null)
                _liveViewException = exception;
        }
    }

    public async Task Complete()
    {
        await StopRealtimeConsoleOutput();

        _consoleWriter.WriteSummary(_testExecutionState);

        if (_liveViewException == null)
            return;

        _testExecutionState.TestFramework.WriteMarkup(
            "[red]Live view failed: " + MarkupParser.Escape(_liveViewException.GetType().Name + ": " + _liveViewException.Message) + "[/]");

        if (_testExecutionState.FirstException == null && _testExecutionState.ExecutionStoppedReason == null)
            ExceptionDispatchInfo.Capture(_liveViewException).Throw();
    }
}
