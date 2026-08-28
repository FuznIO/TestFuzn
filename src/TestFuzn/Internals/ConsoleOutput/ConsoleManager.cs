using System.Runtime.ExceptionServices;
using Fuzn.TestFuzn.Contracts;
using Fuzn.TestFuzn.Internals.ConsoleOutput;
using Fuzn.TestFuzn.Internals.Execution;
using Fuzn.TestFuzn.Internals.State;
using Fuzn.TestFuzn.Internals.Terminal;

namespace Fuzn.TestFuzn.Internals.Logger;

/// <summary>
/// Drives the standalone runner's real-time console output for load tests and writes the final
/// summary. On a terminal that supports the live view, a background loop samples every
/// scenario's load collector at 1 Hz into its <see cref="ScenarioLiveMetrics"/> model and
/// renders the <see cref="LiveDashboard"/> at ~4 fps, from before init until after cleanup.
/// Stopping — on completion, cancellation or an exception, via
/// <see cref="StopRealtimeConsoleOutput"/> from the test runner's finally — makes one last
/// force-refreshed sample (the tick that lands the "completed" phase and freezes the run
/// duration; it is not painted, since leaving the alternate screen would discard the frame)
/// and restores the terminal. <see cref="Complete"/> then writes the summary to the normal
/// screen buffer, after the alternate screen has been left, so it lands in scrollback.
/// Frameworks without real-time output (MSTest), standard tests and redirected or non-ANSI
/// output get no live view, only the summary. A failure inside the live view never hides the
/// results: the terminal is restored at once and the run continues without a live view, the
/// summary is still written, and the failure is then always printed to the normal buffer —
/// and rethrown from Complete only when the run has no failure of its own, so a step or assert
/// failure stays the reported outcome and the runner's own follow-ups still happen. The
/// terminal and the clock come from an <see cref="ILiveViewHost"/>: the real console and wall
/// clock in production, fakes in tests.
/// </summary>
internal class ConsoleManager
{
    /// <summary>The data cadence: one force-refreshed collector snapshot per scenario per second.</summary>
    private static readonly TimeSpan SampleInterval = TimeSpan.FromSeconds(1);

    private readonly TestExecutionState _testExecutionState;
    private readonly ConsoleWriter _consoleWriter;
    private readonly ILiveViewHost _liveViewHost;
    private readonly CancellationTokenSource _ctSource = new CancellationTokenSource();
    private Task? _realtimeLogging;
    private LiveDashboard? _liveDashboard;
    private ScenarioLiveMetrics?[] _liveMetrics = Array.Empty<ScenarioLiveMetrics?>();
    private LiveMetricsSnapshot[] _liveSnapshots = Array.Empty<LiveMetricsSnapshot>();
    private DateTime _nextSampleTime;
    private bool _isLiveViewStopped;
    private Exception? _liveViewException;

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

    /// <summary>
    /// The scenarios' current live views, in scenario order — exactly what the dashboard
    /// renders. Empty until live output starts; each entry is replaced by a fresh immutable
    /// snapshot on every sample.
    /// </summary>
    internal IReadOnlyList<LiveMetricsSnapshot> LiveSnapshots => _liveSnapshots;

    public void StartRealtimeConsoleOutputIfEnabled()
    {
        if (!_testExecutionState.TestFramework.SupportsRealTimeConsoleOutput
            || _testExecutionState.TestType != TestType.Load)
            return;

        // Gate before anything reads the console dimensions: a redirected output fabricates a
        // size and a console-less Windows process throws on reading it. Redirected or non-ANSI
        // output gets no live view for now — only the summary.
        var capabilities = _liveViewHost.DetectCapabilities();
        if (!capabilities.SupportsLiveView)
            return;

        var scenarioCount = _testExecutionState.Scenarios.Count;
        _liveMetrics = new ScenarioLiveMetrics?[scenarioCount];
        _liveSnapshots = new LiveMetricsSnapshot[scenarioCount];
        for (var index = 0; index < scenarioCount; index++)
            _liveSnapshots[index] = CreateInitSnapshot(_testExecutionState.Scenarios[index].Name, TimeSpan.Zero);

        // The dashboard reads the snapshot array on every render; the sampling loop replaces
        // entries with fresh immutable snapshots, so a frame never sees a torn view.
        _liveDashboard = new LiveDashboard(_liveViewHost.CreateTerminalWriter(), capabilities, () => _liveSnapshots);
        _realtimeLogging = Task.Run(() => RunLiveDashboard(_liveDashboard, _ctSource.Token));
    }

    /// <summary>
    /// The live view loop: samples on the 1 Hz cadence and renders on every ~4 fps tick until
    /// cancelled. A failure restores the terminal right away — the run keeps going without a
    /// live view instead of leaving a frozen alternate screen — and is kept for
    /// <see cref="Complete"/> to report after the summary; the task itself never faults.
    /// </summary>
    private async Task RunLiveDashboard(LiveDashboard liveDashboard, CancellationToken cancellationToken)
    {
        try
        {
            liveDashboard.Start();
            _nextSampleTime = _liveViewHost.UtcNow;

            while (!cancellationToken.IsCancellationRequested)
            {
                var utcNow = _liveViewHost.UtcNow;
                if (utcNow >= _nextSampleTime)
                {
                    SampleLiveMetrics(utcNow);
                    ScheduleNextSample(utcNow);
                }

                liveDashboard.Render();

                // Returns normally on cancellation (DelayHelper.Delay swallows the
                // TaskCanceledException) — load-bearing: a normal stop must not take the
                // exception path below, or the final Record after cleanup would be skipped.
                await _liveViewHost.Delay(LiveDashboard.RenderInterval, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // A stop request surfacing as an exception is still a normal stop.
        }
        catch (Exception exception)
        {
            _liveViewException = exception;
            RestoreTerminal(liveDashboard);
        }
    }

    /// <summary>
    /// Advances the next sample time from the due time, so the loop's lateness does not drift
    /// the cadence; after a stall the cadence restarts from now instead of catching up in a burst.
    /// </summary>
    private void ScheduleNextSample(DateTime utcNow)
    {
        _nextSampleTime = _nextSampleTime + SampleInterval;
        if (_nextSampleTime <= utcNow)
            _nextSampleTime = utcNow + SampleInterval;
    }

    /// <summary>
    /// Feeds every scenario's live metrics model one force-refreshed collector snapshot taken
    /// now — the model's wiring contract, since the collector's cached result freezes across
    /// warmup and again after the last measurement. A scenario's model is created at the first
    /// sample taken after its init phase has completed: the model builds its simulation plan
    /// from the scenario's simulations, which init fills in late (after the before-test and
    /// before-scenario hooks), and the collector marks init complete — under its lock — only
    /// once they are in place, so a snapshot carrying InitEndTime is the safe signal that the
    /// simulations are final. Until then the scenario shows as an init placeholder with its
    /// elapsed time.
    /// </summary>
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

                metrics = new ScenarioLiveMetrics(scenario.Name, scenario.SimulationsInternal.ToArray());
                _liveMetrics[index] = metrics;
            }

            metrics.Record(snapshot, utcNow);
            _liveSnapshots[index] = metrics.Current;
        }
    }

    /// <summary>The view shown for a scenario before its model exists: name, init phase and elapsed time.</summary>
    private static LiveMetricsSnapshot CreateInitSnapshot(string scenarioName, TimeSpan elapsed)
    {
        return new LiveMetricsSnapshot
        {
            ScenarioName = scenarioName,
            Phase = LoadTestPhase.Init,
            PhaseLabel = "init",
            Duration = elapsed
        };
    }

    /// <summary>
    /// Stops the live view, if one is running, and restores the terminal: cancels the loop,
    /// waits for it, makes the final force-refreshed sample and leaves the alternate screen.
    /// Safe to call more than once and when no live view was started, and never throws — the
    /// test runner calls it from its finally, where a console failure must not mask the run's
    /// own outcome; a failure is kept for <see cref="Complete"/> to report after the summary.
    /// </summary>
    public async Task StopRealtimeConsoleOutput()
    {
        if (_liveDashboard == null || _realtimeLogging == null || _isLiveViewStopped)
            return;

        _isLiveViewStopped = true;
        try
        {
            await _ctSource.CancelAsync();
            await _realtimeLogging;

            // The final tick, after cleanup has completed: the one force-refreshed Record that
            // lands the "completed" phase label and freezes the run duration. Not painted — the
            // terminal is restored right after, and leaving the alternate screen would discard
            // the frame anyway.
            if (_liveViewException == null)
                SampleLiveMetrics(_liveViewHost.UtcNow);
        }
        catch (Exception exception)
        {
            _liveViewException = exception;
        }
        finally
        {
            RestoreTerminal(_liveDashboard);
        }
    }

    /// <summary>
    /// Leaves the alternate screen and restores the cursor, auto-wrap and styling; a no-op once
    /// done. A failure here is recorded rather than thrown, so restoring never masks an earlier one.
    /// </summary>
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

        // The live view's own failure is always reported, after the summary and in the normal
        // buffer, but it becomes the run's outcome only when the run has no failure of its own:
        // a step or assert failure must stay the reported one, and the runner's follow-ups
        // (internal-state assertions, the first exception's rethrow) must still run.
        _testExecutionState.TestFramework.WriteMarkup(
            "[red]Live view failed: " + MarkupParser.Escape(_liveViewException.GetType().Name + ": " + _liveViewException.Message) + "[/]");

        if (_testExecutionState.FirstException == null && _testExecutionState.ExecutionStoppedReason == null)
            ExceptionDispatchInfo.Capture(_liveViewException).Throw();
    }
}
