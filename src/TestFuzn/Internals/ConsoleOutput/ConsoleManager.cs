using System.Runtime.ExceptionServices;
using Fuzn.TestFuzn.Contracts;
using Fuzn.TestFuzn.Internals.ConsoleOutput;
using Fuzn.TestFuzn.Internals.Execution;
using Fuzn.TestFuzn.Internals.State;
using Fuzn.TestFuzn.Internals.Terminal;
using Fuzn.TestFuzn.Internals.Thresholds;

namespace Fuzn.TestFuzn.Internals.Logger;

/// <summary>
/// Drives the standalone runner's real-time console output for load tests and writes the final
/// summary. A background loop samples every scenario's load collector at 1 Hz into its
/// <see cref="ScenarioLiveMetrics"/> model, from before init until after cleanup, and shows the
/// samples in one of two ways, chosen once from the terminal's capabilities: on a terminal that
/// supports the live view, the <see cref="LiveDashboard"/> rendered at ~4 fps on the alternate
/// screen; on any terminal without live view support — a redirected or non-ANSI output (docker
/// logs, CI, a dumb terminal), or an ANSI output whose input is redirected (stdin from
/// /dev/null, as several CI runners do) — the plain stats lines of a
/// <see cref="LiveStatsWriter"/> — one line per scenario per sample plus the phase transitions,
/// with no escape sequence at all, the terminal size never read and no key polled, since a
/// redirected output has no size and a redirected input no keys.
/// Stopping — on completion, cancellation or an exception, via
/// <see cref="StopRealtimeConsoleOutput"/> from the test runner's finally — makes one last
/// force-refreshed sample (the tick that lands the "completed" phase and freezes the run
/// duration; not painted on the dashboard, since leaving the alternate screen would discard the
/// frame, but written by the stats writer as its final line with the run's outcome) and restores
/// the terminal when the dashboard was shown. <see cref="Complete"/> then writes the summary to
/// the normal screen buffer, after the alternate screen has been left, so it lands in scrollback.
/// While the dashboard runs, the loop also polls the keyboard on every tick: the quit key
/// (<see cref="LiveDashboardLayout.QuitKey"/>, either case) requests a graceful stop through
/// the same cancellation path Ctrl+C takes (<see cref="TestExecutionState.RequestStop"/>), so
/// the run winds down exactly as after Ctrl+C while the loop keeps rendering through cleanup
/// (the request runs the run's cancellation callbacks off the loop thread, and the stop
/// observes its outcome); every other key goes to <see cref="LiveDashboardKeyHandler"/>, which
/// derives the next <see cref="LiveDashboardViewState"/> — the view, the step selection, the
/// pause, the time window, the help — from the one this manager owns and hands the dashboard
/// on every frame, so a key shows on the next tick's frame; a key without a binding changes
/// nothing. Keys are read nowhere else, so input is never touched without the dashboard.
/// Pausing (<see cref="LiveDashboardKeyHandler.PauseKey"/>) freezes the picture, not the
/// sampling: the loop keeps sampling at 1 Hz, but from the pause on the dashboard is given the
/// snapshots as they were when the pause began, so the frame — the badge added — holds still
/// and, with the dashboard's spinner held too, nothing is written until a key changes the
/// state (the keys act on the frozen snapshots as well, and the frame is repainted over them,
/// so the viewer can switch views or move the selection through a frozen picture — never
/// onto a step it does not show) or the terminal is resized (the frozen frame laid out again
/// at the new size); resuming hands the live snapshots back, so the next frame jumps to now.
/// Frameworks without real-time output (MSTest) and standard tests get no live view, only the
/// summary. A failure inside the live view never hides the results: the terminal is restored
/// at once and the run continues without a live view, the summary is still written, and the
/// failure is then always printed to the normal buffer — and rethrown from Complete only when
/// the run has no failure of its own, so a step or assert failure stays the reported outcome
/// and the runner's own follow-ups still happen. The terminal and the clock come from an
/// <see cref="ILiveViewHost"/>: the real console and wall clock in production, fakes in tests.
/// </summary>
internal class ConsoleManager
{
    /// <summary>The data cadence: one force-refreshed collector snapshot per scenario per second.</summary>
    private static readonly TimeSpan SampleInterval = TimeSpan.FromSeconds(1);

    /// <summary>
    /// The loop cadence in both output modes: the dashboard's render interval. The stats writer
    /// has nothing to do on a tick between samples, and one cadence keeps the sampling — the
    /// due-time check below — identical in both modes.
    /// </summary>
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
    private LiveMetricsSnapshot[]? _frozenSnapshots;
    private LiveDashboardViewState _viewState = LiveDashboardViewState.Default;
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

    /// <summary>
    /// The scenarios' current live views, in scenario order — exactly what the dashboard
    /// renders and the stats writer writes. Empty until live output starts; each entry is replaced
    /// by a fresh immutable snapshot on every sample.
    /// </summary>
    internal IReadOnlyList<LiveMetricsSnapshot> LiveSnapshots => _liveSnapshots;

    /// <summary>
    /// The viewer's current interaction state — what the dashboard renders under: the
    /// default until a key changes it, then whatever the last handled key left. Read on the
    /// loop thread; a test reads it with the loop parked.
    /// </summary>
    internal LiveDashboardViewState ViewState => _viewState;

    /// <summary>
    /// The quit key's stop request, once one has been made (null before): completes when the
    /// run's cancellation callbacks have run, off the loop thread, and faults when one of them
    /// threw — observed by <see cref="StopRealtimeConsoleOutput"/>.
    /// </summary>
    internal Task? StopRequest => _stopRequest;

    /// <summary>
    /// The live view loop's task, once one has started (null before): completes when the loop
    /// has ended, on Stop's cancellation or on a failure of its own, which it records rather
    /// than faults with — so a test can wait for a loop that ended by itself.
    /// </summary>
    internal Task? LiveViewLoop => _liveViewLoop;

    public void StartRealtimeConsoleOutputIfEnabled()
    {
        if (!_testExecutionState.TestFramework.SupportsRealTimeConsoleOutput
            || _testExecutionState.TestType != TestType.Load)
            return;

        // Gate before anything reads the console dimensions: a redirected output fabricates a
        // size and a console-less Windows process throws on reading it.
        var capabilities = _liveViewHost.DetectCapabilities();

        var scenarioCount = _testExecutionState.Scenarios.Count;
        _liveMetrics = new ScenarioLiveMetrics?[scenarioCount];
        _liveSnapshots = new LiveMetricsSnapshot[scenarioCount];
        for (var index = 0; index < scenarioCount; index++)
            _liveSnapshots[index] = CreateInitSnapshot(_testExecutionState.Scenarios[index], TimeSpan.Zero);

        if (!capabilities.SupportsLiveView)
        {
            // No live view support — a redirected or non-ANSI output, or a redirected input:
            // the same samples as plain stats lines. The writer is only ever written to — its
            // size is never read — and no reader is created: reading keys from a redirected
            // input throws, and nothing polls one here.
            var liveStatsWriter = new LiveStatsWriter(_liveViewHost.CreateTerminalWriter(), scenarioCount);
            _liveStatsWriter = liveStatsWriter;
            _liveViewLoop = Task.Run(() => RunLiveView(null, null, liveStatsWriter, _ctSource.Token));
            return;
        }

        // Keys are polled behind this same gate and nowhere else: the live view requires an
        // interactive terminal, so input is a TTY here, whereas reading keys from a redirected
        // input throws. A host without a reader fails here, loud, before the alternate screen.
        var terminalReader = _liveViewHost.CreateTerminalReader();
        if (terminalReader == null)
            throw new InvalidOperationException("The live view host returned no terminal reader.");

        // The dashboard reads the snapshot array on every render; the sampling loop replaces
        // entries with fresh immutable snapshots, so a frame never sees a torn view — and
        // while the viewer has paused, the copy frozen at the pause instead.
        var liveDashboard = new LiveDashboard(_liveViewHost.CreateTerminalWriter(), capabilities, SnapshotsForFrame);
        _liveDashboard = liveDashboard;
        _liveViewLoop = Task.Run(() => RunLiveView(liveDashboard, terminalReader, null, _ctSource.Token));
    }

    /// <summary>
    /// The live view loop, one for both output modes: on every tick handles the keys pressed
    /// since the last one and renders (the dashboard mode), and samples when the 1 Hz cadence
    /// is due, writing the sample's stats lines (the stats writer mode), until cancelled. The
    /// stats writer mode never touches keys or the terminal size and does nothing on a tick
    /// between samples. A failure ends the loop: the dashboard's terminal is restored right
    /// away — the run keeps going without a live view instead of leaving a frozen alternate
    /// screen — and the failure is kept for <see cref="Complete"/> to report after the summary;
    /// the task itself never faults.
    /// </summary>
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
                    liveDashboard.Render(_viewState);

                // Returns normally on cancellation (DelayHelper.Delay swallows the
                // TaskCanceledException) — load-bearing: a normal stop must not take the
                // exception path below, or the final Record after cleanup would be skipped.
                await _liveViewHost.Delay(TickInterval, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // A stop request surfacing as an exception is still a normal stop.
        }
        catch (Exception exception)
        {
            _liveViewException = exception;
            if (liveDashboard != null)
                RestoreTerminal(liveDashboard);
        }
    }

    /// <summary>
    /// Drains every key pressed since the last tick — the reader never blocks, so the loop keeps
    /// its cadence — and acts on the quit key: a stop request on the state, the same
    /// cancellation path Ctrl+C takes, so producers stop, in-flight iterations and cleanup
    /// complete and the summary follows, as after Ctrl+C. The request runs the run's
    /// cancellation callbacks off this thread — one per in-flight delay, consumer and request
    /// on a real load run — so the loop keeps painting while the pipeline tears down, as it
    /// does when Ctrl+C runs them on the signal thread; the request's task is kept for
    /// <see cref="StopRealtimeConsoleOutput"/> to observe. The loop itself is not stopped here:
    /// it keeps rendering through cleanup until the runner's Stop. A repeated quit key finds
    /// the stop already requested and is a no-op. Every other key goes to the
    /// <see cref="LiveDashboardKeyHandler"/> over the snapshots on view
    /// (<see cref="SnapshotsForFrame"/>: the frozen copy while paused, so a key acts on the
    /// picture the viewer sees — a step the run has found since the pause began is not on a
    /// frozen picture, so it cannot be selected through one), and the state it returns is
    /// what the tick renders; the tick a pause begins on freezes a copy of the live snapshots
    /// for the dashboard, and the tick it ends on lets them go. Runs on the loop thread; the
    /// request is safe against the runner thread stopping the live view at the same time.
    /// </summary>
    private void HandleKeys(ITerminalReader terminalReader)
    {
        while (terminalReader.TryReadKey(out var key))
        {
            if (IsQuitKey(key))
            {
                if (_stopRequest == null)
                    _stopRequest = _testExecutionState.RequestStop();

                continue;
            }

            var viewState = LiveDashboardKeyHandler.Apply(_viewState, key, SnapshotsForFrame());
            if (viewState.IsPaused && !_viewState.IsPaused)
                _frozenSnapshots = (LiveMetricsSnapshot[])_liveSnapshots.Clone();
            else if (!viewState.IsPaused)
                _frozenSnapshots = null;

            _viewState = viewState;
        }
    }

    /// <summary>
    /// The snapshots the dashboard lays a frame out from: the live ones, or while the viewer
    /// has paused the copy frozen when the pause began, so the picture holds still while the
    /// sampling behind it goes on.
    /// </summary>
    private IReadOnlyList<LiveMetricsSnapshot> SnapshotsForFrame()
    {
        if (_frozenSnapshots != null)
            return _frozenSnapshots;

        return _liveSnapshots;
    }

    /// <summary>
    /// The quit key in either case — Shift is what makes it upper case — but not a chord with
    /// Alt or Control: on a pty Alt+q arrives as ESC q and reads as q with the Alt modifier.
    /// </summary>
    private static bool IsQuitKey(ConsoleKeyInfo key)
    {
        if ((key.Modifiers & (ConsoleModifiers.Alt | ConsoleModifiers.Control)) != 0)
            return false;

        return char.ToLowerInvariant(key.KeyChar) == LiveDashboardLayout.QuitKey;
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
    /// simulations are final. The scenario's declared thresholds, fixed since the builder ran,
    /// are handed to the model the same way, for it to evaluate live on every sample. Until then
    /// the scenario shows as an init placeholder with its elapsed time.
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

                    _liveSnapshots[index] = CreateInitSnapshot(scenario, elapsed);
                    continue;
                }

                metrics = new ScenarioLiveMetrics(scenario.Name, scenario.SimulationsInternal.ToArray(), scenario.Thresholds.ToArray());
                _liveMetrics[index] = metrics;
            }

            metrics.Record(snapshot, utcNow);
            _liveSnapshots[index] = metrics.Current;
        }
    }

    /// <summary>
    /// The view shown for a scenario before its model exists: name, init phase, elapsed time,
    /// and its declared thresholds in their pre-sample state (every one Ok with a Current of
    /// 0), so the set of thresholds on view is the same from the first frame on.
    /// </summary>
    private static LiveMetricsSnapshot CreateInitSnapshot(Scenario scenario, TimeSpan elapsed)
    {
        return new LiveMetricsSnapshot
        {
            ScenarioName = scenario.Name,
            Phase = LoadTestPhase.Init,
            PhaseLabel = "init",
            Duration = elapsed,
            Thresholds = ThresholdEvaluator.InitialStates(scenario.Thresholds.ToArray())
        };
    }

    /// <summary>
    /// Stops the live view, if one is running: cancels the loop, waits for it, makes the final
    /// force-refreshed sample — the stats writer's final line — and, when the dashboard was shown,
    /// leaves the alternate screen. Safe to call more than once and when no live view was
    /// started, and never throws — the test runner calls it from its finally, where a console
    /// failure must not mask the run's own outcome; a failure is kept for <see cref="Complete"/>
    /// to report after the summary.
    /// </summary>
    public async Task StopRealtimeConsoleOutput()
    {
        if (_liveViewLoop == null || _isLiveViewStopped)
            return;

        _isLiveViewStopped = true;
        try
        {
            await _ctSource.CancelAsync();
            await _liveViewLoop;

            // The quit key's stop request, if one was made: by the time the runner stops the
            // live view its callbacks have run (the run reached cleanup on them), so awaiting
            // it here only surfaces a callback that threw — as a live view failure, reported
            // after the summary, rather than an unobserved task.
            if (_stopRequest != null)
                await _stopRequest;

            // The final tick, after cleanup has completed: the one force-refreshed Record that
            // lands the "completed" phase label and freezes the run duration. Not painted on
            // the dashboard — the terminal is restored right after, and leaving the alternate
            // screen would discard the frame anyway — but the stats writer writes it as its final
            // line, with the run's outcome: stopped when the run was, by Ctrl+C, the quit key
            // or an assert that stops the run.
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
