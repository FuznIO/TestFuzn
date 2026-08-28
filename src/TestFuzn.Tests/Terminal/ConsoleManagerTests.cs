using System.Reflection;
using Fuzn.TestFuzn.ConsoleOutput;
using Fuzn.TestFuzn.Contracts.Adapters;
using Fuzn.TestFuzn.Contracts.Results.Load;
using Fuzn.TestFuzn.Contracts.Results.Standard;
using Fuzn.TestFuzn.Internals;
using Fuzn.TestFuzn.Internals.ConsoleOutput;
using Fuzn.TestFuzn.Internals.Execution;
using Fuzn.TestFuzn.Internals.Execution.Producers.Simulations;
using Fuzn.TestFuzn.Internals.Logger;
using Fuzn.TestFuzn.Internals.Results.Load;
using Fuzn.TestFuzn.Internals.State;
using Fuzn.TestFuzn.Internals.Terminal;

namespace Fuzn.TestFuzn.Tests.Terminal;

/// <summary>
/// Pins the live view wiring in <see cref="ConsoleManager"/> hermetically: a real in-memory
/// <see cref="ScenarioLoadCollector"/> (owned by a real <see cref="TestExecutionState"/>) is
/// driven through init, measurement and cleanup while the render loop runs on its real
/// thread-pool task but advances only when the test releases its delay — so every assertion
/// runs with the loop parked. Pinned: the 1 Hz sampling gated on init completion over 4 fps
/// rendering, the single force-refreshed Record after cleanup, the terminal restored exactly
/// once on every exit path with the summary written afterwards, the live view's own failure
/// being reported without displacing the run's failure, and the keys polled on every tick —
/// the quit key landing in the Ctrl+C stop path while the loop renders on until Stop, other
/// keys drained and ignored, and the reader never consulted without a live view.
/// </summary>
[TestClass]
public class ConsoleManagerTests : Test
{
    private const string ScenarioName = "Checkout flow";
    private const string StepName = "Checkout step";
    private const string EnterSequence = AnsiCodes.EnterAlternateScreen + AnsiCodes.HideCursor + AnsiCodes.DisableAutoWrap;
    private const string RestoreSequence = AnsiCodes.Reset + AnsiCodes.EnableAutoWrap + AnsiCodes.ShowCursor + AnsiCodes.ExitAlternateScreen;
    private const string TerminalEventPrefix = "terminal:";
    private const string SummaryEvent = "summary";
    private const string MarkupEventPrefix = "markup:";

    private static DateTime At(double seconds)
    {
        return SyntheticLoadSnapshots.At(seconds);
    }

    [Test]
    public async Task Verify_sampling_is_gated_on_init_completion_over_the_render_cadence()
    {
        await Scenario()
            .Step("Before init completes every sample is the placeholder, not a model snapshot, and each render tick repaints", async context =>
            {
                var harness = new Harness();
                harness.ConsoleManager.StartRealtimeConsoleOutputIfEnabled();
                await harness.Host.WaitForParkedTick();

                Assert.AreEqual(EnterSequence, harness.Writer.Writes[0]);
                Assert.HasCount(2, harness.Writer.Writes);
                var placeholder = Assert.ContainsSingle(harness.ConsoleManager.LiveSnapshots);
                Assert.AreEqual(ScenarioName, placeholder.ScenarioName);
                Assert.AreEqual(LoadTestPhase.Init, placeholder.Phase);
                Assert.AreEqual("init", placeholder.PhaseLabel);
                Assert.AreEqual(TimeSpan.Zero, placeholder.Duration);
                Assert.IsNull(placeholder.PlannedDuration);

                harness.Collector.MarkPhaseAsStarted(LoadTestPhase.Init, At(0));
                await harness.Host.RunTick(At(1));

                var elapsedPlaceholder = harness.ConsoleManager.LiveSnapshots[0];
                Assert.AreEqual("init", elapsedPlaceholder.PhaseLabel);
                Assert.AreEqual(TimeSpan.FromSeconds(1), elapsedPlaceholder.Duration);
                // A model built before init would carry the (empty) plan as a zero planned
                // duration; the placeholder has none.
                Assert.IsNull(elapsedPlaceholder.PlannedDuration);
                Assert.IsEmpty(elapsedPlaceholder.Samples);
                Assert.HasCount(3, harness.Writer.Writes);

                // A quarter second later: a render tick, not a sample tick — the same snapshot
                // instance is shown again, only the spinner repaints.
                await harness.Host.RunTick(At(1.25));

                Assert.AreSame(elapsedPlaceholder, harness.ConsoleManager.LiveSnapshots[0]);
                Assert.HasCount(4, harness.Writer.Writes);

                await harness.ConsoleManager.StopRealtimeConsoleOutput();
            })
            .Step("The first sample after init completes is the model's baseline; the next closes the first interval", async context =>
            {
                var harness = new Harness();
                harness.ConsoleManager.StartRealtimeConsoleOutputIfEnabled();
                await harness.Host.WaitForParkedTick();

                harness.Collector.MarkPhaseAsStarted(LoadTestPhase.Init, At(0));
                harness.CompleteInitAndStartMeasurement(At(1.5));
                harness.RecordIterations(3);
                await harness.Host.RunTick(At(2));

                var baseline = harness.ConsoleManager.LiveSnapshots[0];
                Assert.AreEqual(LoadTestPhase.Measurement, baseline.Phase);
                Assert.AreEqual(TimeSpan.FromSeconds(10), baseline.PlannedDuration);
                Assert.AreEqual(3, baseline.RequestCountOk);
                // The three requests are in the baseline: no interval has closed yet.
                Assert.IsEmpty(baseline.Samples);

                harness.RecordIterations(5);
                await harness.Host.RunTick(At(3));

                var firstInterval = harness.ConsoleManager.LiveSnapshots[0];
                Assert.AreEqual(8, firstInterval.RequestCountOk);
                var sample = Assert.ContainsSingle(firstInterval.Samples);
                Assert.AreEqual(5, sample.OkDelta);
                Assert.AreEqual(0, sample.FailedDelta);
                Assert.AreEqual(5.0, sample.RequestsPerSecond, 1e-9);

                await harness.ConsoleManager.StopRealtimeConsoleOutput();
            })
            .Run();
    }

    [Test]
    public async Task Verify_stop_makes_exactly_one_final_record_after_cleanup()
    {
        await Scenario()
            .Step("Stop records once after cleanup: the view reads completed with the duration frozen at the run's length", async context =>
            {
                var harness = new Harness();
                harness.ConsoleManager.StartRealtimeConsoleOutputIfEnabled();
                await harness.Host.WaitForParkedTick();

                harness.Collector.MarkPhaseAsStarted(LoadTestPhase.Init, At(0));
                harness.CompleteInitAndStartMeasurement(At(1));
                await harness.Host.RunTick(At(2));
                Assert.IsEmpty(harness.ConsoleManager.LiveSnapshots[0].Samples);

                harness.RecordIterations(3);
                harness.CompleteMeasurementAndCleanup(At(10), At(12));
                harness.Host.UtcNow = At(13);
                await harness.ConsoleManager.StopRealtimeConsoleOutput();

                var final = harness.ConsoleManager.LiveSnapshots[0];
                Assert.AreEqual(LoadTestPhase.Cleanup, final.Phase);
                Assert.AreEqual(3, final.RequestCountOk);
                Assert.AreEqual("completed", final.PhaseLabel);
                Assert.IsTrue(final.IsCompleted);
                Assert.AreEqual(TimeSpan.FromSeconds(12), final.Duration);
                Assert.AreEqual(1.0, final.ProgressFraction);
                // The final Record closed the interval from the baseline at 2 s to 13 s.
                var sample = Assert.ContainsSingle(final.Samples);
                Assert.AreEqual(3, sample.OkDelta);
                Assert.AreEqual(At(13), sample.Timestamp);
            })
            .Step("A second Stop and a Complete after it add no further Record", async context =>
            {
                var harness = new Harness();
                harness.ConsoleManager.StartRealtimeConsoleOutputIfEnabled();
                await harness.Host.WaitForParkedTick();

                harness.Collector.MarkPhaseAsStarted(LoadTestPhase.Init, At(0));
                harness.CompleteInitAndStartMeasurement(At(1));
                await harness.Host.RunTick(At(2));
                harness.RecordIterations(3);
                harness.CompleteMeasurementAndCleanup(At(10), At(12));
                harness.Host.UtcNow = At(13);
                await harness.ConsoleManager.StopRealtimeConsoleOutput();
                var final = harness.ConsoleManager.LiveSnapshots[0];
                Assert.ContainsSingle(final.Samples);

                harness.Host.UtcNow = At(20);
                await harness.ConsoleManager.StopRealtimeConsoleOutput();
                Assert.AreSame(final, harness.ConsoleManager.LiveSnapshots[0]);

                harness.Host.UtcNow = At(30);
                await harness.ConsoleManager.Complete();
                Assert.AreSame(final, harness.ConsoleManager.LiveSnapshots[0]);
                Assert.AreEqual(TimeSpan.FromSeconds(12), harness.ConsoleManager.LiveSnapshots[0].Duration);
            })
            .Run();
    }

    [Test]
    public async Task Verify_terminal_is_restored_once_and_the_summary_follows_on_completion()
    {
        await Scenario()
            .Step("Normal completion: enter once, restore once as the last terminal write, then the summary", async context =>
            {
                var harness = new Harness();
                harness.ConsoleManager.StartRealtimeConsoleOutputIfEnabled();
                await harness.Host.WaitForParkedTick();

                harness.Collector.MarkPhaseAsStarted(LoadTestPhase.Init, At(0));
                harness.CompleteInitAndStartMeasurement(At(1));
                harness.RecordIterations(3);
                await harness.Host.RunTick(At(2));
                harness.CompleteMeasurementAndCleanup(At(10), At(12));
                harness.Host.UtcNow = At(13);

                await harness.ConsoleManager.Complete();

                harness.AssertEnteredAndRestoredOnce();
                harness.AssertSummaryFollowsRestore();
                Assert.IsEmpty(harness.MarkupEvents());
            })
            .Step("Cancellation (the framework token cancelled mid-run) leaves the loop running until Stop, which restores once, and the summary follows", async context =>
            {
                var harness = new Harness();
                harness.ConsoleManager.StartRealtimeConsoleOutputIfEnabled();
                await harness.Host.WaitForParkedTick();

                harness.Collector.MarkPhaseAsStarted(LoadTestPhase.Init, At(0));
                harness.TestFramework.Cancel();
                harness.AssertStoppedLikeCtrlC();

                // The loop is driven by the manager's own stop, not by the run's cancellation:
                // it keeps rendering through cleanup after Ctrl+C.
                await harness.Host.RunTick(At(1));
                Assert.AreEqual(TimeSpan.FromSeconds(1), harness.ConsoleManager.LiveSnapshots[0].Duration);

                harness.CompleteInitAndStartMeasurement(At(1.5));
                harness.CompleteMeasurementAndCleanup(At(2), At(3));
                harness.Host.UtcNow = At(4);
                await harness.ConsoleManager.StopRealtimeConsoleOutput();
                harness.AssertEnteredAndRestoredOnce();
                Assert.AreEqual("completed", harness.ConsoleManager.LiveSnapshots[0].PhaseLabel);

                await harness.ConsoleManager.Complete();

                harness.AssertEnteredAndRestoredOnce();
                harness.AssertSummaryFollowsRestore();
            })
            .Run();
    }

    [Test]
    public async Task Verify_render_loop_failure_restores_immediately_and_is_reported_after_the_summary()
    {
        await Scenario()
            .Step("A failing size read ends the loop, restores the terminal at once, skips the final Record, and Complete reports then rethrows it", async context =>
            {
                var harness = new Harness();
                harness.ConsoleManager.StartRealtimeConsoleOutputIfEnabled();
                await harness.Host.WaitForParkedTick();

                harness.Collector.MarkPhaseAsStarted(LoadTestPhase.Init, At(0));
                harness.CompleteInitAndStartMeasurement(At(1));
                await harness.Host.RunTick(At(2));
                Assert.AreEqual(LoadTestPhase.Measurement, harness.ConsoleManager.LiveSnapshots[0].Phase);

                harness.Writer.WindowSizeReadFailure = new IOException("Handle [stdout] is invalid");
                harness.Host.UtcNow = At(3);
                harness.Host.Release();
                await harness.WaitForRestore();

                harness.AssertEnteredAndRestoredOnce();

                // Cleanup completes after the live view died: Stop must not record the final
                // tick on a failed live view, so the view stays where the failure left it.
                harness.CompleteMeasurementAndCleanup(At(4), At(5));
                harness.Host.UtcNow = At(6);
                await harness.ConsoleManager.StopRealtimeConsoleOutput();

                harness.AssertEnteredAndRestoredOnce();
                Assert.AreEqual(LoadTestPhase.Measurement, harness.ConsoleManager.LiveSnapshots[0].Phase);
                Assert.IsFalse(harness.ConsoleManager.LiveSnapshots[0].IsCompleted);

                var thrown = await Assert.ThrowsExactlyAsync<IOException>(async () => await harness.ConsoleManager.Complete());
                Assert.AreEqual("Handle [stdout] is invalid", thrown.Message);

                harness.AssertEnteredAndRestoredOnce();
                harness.AssertSummaryFollowsRestore();
                var failureLine = Assert.ContainsSingle(harness.MarkupEvents());
                Assert.AreEqual("[red]Live view failed: IOException: Handle [[stdout]] is invalid[/]", failureLine);
                Assert.IsGreaterThan(harness.Events.IndexOf(SummaryEvent), harness.Events.IndexOf(MarkupEventPrefix + failureLine));
            })
            .Step("With a step failure on the run, Complete prints the live view failure but does not throw it", async context =>
            {
                var harness = new Harness();
                harness.ConsoleManager.StartRealtimeConsoleOutputIfEnabled();
                await harness.Host.WaitForParkedTick();

                harness.Writer.WindowSizeReadFailure = new IOException("The handle is invalid.");
                harness.Host.Release();
                await harness.WaitForRestore();

                harness.State.FirstException = new InvalidOperationException("Checkout step failed");
                await harness.ConsoleManager.Complete();

                harness.AssertEnteredAndRestoredOnce();
                harness.AssertSummaryFollowsRestore();
                var failureLine = Assert.ContainsSingle(harness.MarkupEvents());
                Assert.AreEqual("[red]Live view failed: IOException: The handle is invalid.[/]", failureLine);
            })
            .Step("With a controlled stop on the run, Complete prints the live view failure but does not throw it", async context =>
            {
                var harness = new Harness();
                harness.ConsoleManager.StartRealtimeConsoleOutputIfEnabled();
                await harness.Host.WaitForParkedTick();

                harness.Writer.WindowSizeReadFailure = new IOException("The handle is invalid.");
                harness.Host.Release();
                await harness.WaitForRestore();

                harness.State.ExecutionStoppedReason = new InvalidOperationException("Assert while running failed");
                await harness.ConsoleManager.Complete();

                harness.AssertEnteredAndRestoredOnce();
                harness.AssertSummaryFollowsRestore();
                Assert.ContainsSingle(harness.MarkupEvents());
            })
            .Run();
    }

    [Test]
    public async Task Verify_no_live_view_without_real_time_support_or_a_live_terminal()
    {
        await Scenario()
            .Step("A framework without real-time output gets no live view and only the summary, and never reads a key", async context =>
            {
                var harness = new Harness();
                harness.TestFramework.SupportsRealTimeConsoleOutput = false;
                harness.Collector.MarkPhaseAsStarted(LoadTestPhase.Init, At(0));
                harness.CompleteInitAndStartMeasurement(At(1));
                harness.CompleteMeasurementAndCleanup(At(2), At(3));
                harness.Host.Reader.Press('q');

                harness.ConsoleManager.StartRealtimeConsoleOutputIfEnabled();
                await harness.ConsoleManager.Complete();

                Assert.IsEmpty(harness.Writer.Writes);
                Assert.IsEmpty(harness.ConsoleManager.LiveSnapshots);
                Assert.AreEqual(0, harness.Host.DetectCapabilitiesCallCount);
                Assert.AreEqual(0, harness.Host.CreateTerminalReaderCallCount);
                Assert.AreEqual(0, harness.Host.Reader.TryReadKeyCallCount);
                Assert.AreEqual(1, harness.Host.Reader.PendingKeyCount);
                Assert.AreEqual(ExecutionStatus.Running, harness.State.ExecutionStatus);
            })
            .Step("A terminal without live view support (redirected output and input) gets no live view and only the summary, and never reads a key", async context =>
            {
                var harness = new Harness();
                harness.Host.Capabilities = TerminalCapabilities.Resolve(isOutputRedirected: true, isInputRedirected: true, isVirtualTerminalEnabled: true, term: "xterm-256color", colorTerm: null, noColor: null);
                harness.Collector.MarkPhaseAsStarted(LoadTestPhase.Init, At(0));
                harness.CompleteInitAndStartMeasurement(At(1));
                harness.CompleteMeasurementAndCleanup(At(2), At(3));
                harness.Host.Reader.Press('q');

                harness.ConsoleManager.StartRealtimeConsoleOutputIfEnabled();
                await harness.ConsoleManager.Complete();

                Assert.IsEmpty(harness.Writer.Writes);
                Assert.IsEmpty(harness.ConsoleManager.LiveSnapshots);
                Assert.AreEqual(1, harness.Host.DetectCapabilitiesCallCount);
                Assert.AreEqual(1, harness.Events.Count(eventName => eventName == SummaryEvent));
                Assert.AreEqual(0, harness.Host.CreateTerminalReaderCallCount);
                Assert.AreEqual(0, harness.Host.Reader.TryReadKeyCallCount);
                Assert.AreEqual(1, harness.Host.Reader.PendingKeyCount);
                Assert.AreEqual(ExecutionStatus.Running, harness.State.ExecutionStatus);
            })
            .Run();
    }

    [Test]
    public async Task Verify_quit_key_requests_the_same_stop_as_Ctrl_C_and_the_loop_renders_on_until_Stop()
    {
        await Scenario()
            .Step("q on a tick lands in the Ctrl+C stop path, and the loop renders on through cleanup until Stop restores once, then the summary", async context =>
            {
                var harness = new Harness();
                harness.ConsoleManager.StartRealtimeConsoleOutputIfEnabled();
                await harness.Host.WaitForParkedTick();
                Assert.AreEqual(1, harness.Host.CreateTerminalReaderCallCount);
                Assert.AreEqual(ExecutionStatus.Running, harness.State.ExecutionStatus);

                harness.Collector.MarkPhaseAsStarted(LoadTestPhase.Init, At(0));
                harness.Host.Reader.Press('q');
                await harness.Host.RunTick(At(1));

                // The stop request runs the run's cancellation callbacks off the loop thread;
                // once they have run, the state reads exactly as after Ctrl+C.
                await harness.WaitForStopRequest();
                harness.AssertStoppedLikeCtrlC();
                Assert.AreEqual(0, harness.Host.Reader.PendingKeyCount);
                // The tick went on to sample and render after the key.
                Assert.HasCount(3, harness.Writer.Writes);
                Assert.AreEqual(TimeSpan.FromSeconds(1), harness.ConsoleManager.LiveSnapshots[0].Duration);

                // The loop is driven by the manager's own stop, not by the run's cancellation:
                // it keeps rendering and sampling through cleanup, as after Ctrl+C.
                await harness.Host.RunTick(At(1.25));
                Assert.HasCount(4, harness.Writer.Writes);

                harness.CompleteInitAndStartMeasurement(At(1.5));
                await harness.Host.RunTick(At(2));
                Assert.HasCount(5, harness.Writer.Writes);
                Assert.AreEqual(LoadTestPhase.Measurement, harness.ConsoleManager.LiveSnapshots[0].Phase);

                harness.CompleteMeasurementAndCleanup(At(3), At(4));
                harness.Host.UtcNow = At(5);
                await harness.ConsoleManager.StopRealtimeConsoleOutput();
                harness.AssertEnteredAndRestoredOnce();
                Assert.AreEqual("completed", harness.ConsoleManager.LiveSnapshots[0].PhaseLabel);

                await harness.ConsoleManager.Complete();
                harness.AssertEnteredAndRestoredOnce();
                harness.AssertSummaryFollowsRestore();
                Assert.IsEmpty(harness.MarkupEvents());
            })
            .Step("Q, with Shift held, stops the same way", async context =>
            {
                var harness = new Harness();
                harness.ConsoleManager.StartRealtimeConsoleOutputIfEnabled();
                await harness.Host.WaitForParkedTick();
                harness.Collector.MarkPhaseAsStarted(LoadTestPhase.Init, At(0));

                harness.Host.Reader.Press('Q');
                await harness.Host.RunTick(At(1));

                await harness.WaitForStopRequest();
                harness.AssertStoppedLikeCtrlC();

                await harness.ConsoleManager.Complete();
                harness.AssertEnteredAndRestoredOnce();
                harness.AssertSummaryFollowsRestore();
            })
            .Run();
    }

    [Test]
    public async Task Verify_other_keys_are_drained_and_ignored_and_a_repeated_quit_key_is_a_no_op()
    {
        await Scenario()
            .Step("Other keys are drained on the tick and change nothing: the run keeps going", async context =>
            {
                var harness = new Harness();
                harness.ConsoleManager.StartRealtimeConsoleOutputIfEnabled();
                await harness.Host.WaitForParkedTick();
                harness.Collector.MarkPhaseAsStarted(LoadTestPhase.Init, At(0));
                var readsBefore = harness.Host.Reader.TryReadKeyCallCount;

                harness.Host.Reader.Press('x');
                harness.Host.Reader.Press(' ');
                harness.Host.Reader.Press(new ConsoleKeyInfo('\r', ConsoleKey.Enter, shift: false, alt: false, control: false));
                harness.Host.Reader.Press(new ConsoleKeyInfo('\0', ConsoleKey.UpArrow, shift: false, alt: false, control: false));
                // Ctrl+Q is not the quit key: the typed character is what counts.
                harness.Host.Reader.Press(new ConsoleKeyInfo('\u0011', ConsoleKey.Q, shift: false, alt: false, control: true));
                // Nor is a chord with Alt or Control that still carries the character: on a pty
                // Alt+q arrives as ESC q and reads as q with the Alt modifier, and some hosts
                // report Ctrl+q as q with Control rather than as the control character.
                harness.Host.Reader.Press(new ConsoleKeyInfo('q', ConsoleKey.Q, shift: false, alt: true, control: false));
                harness.Host.Reader.Press(new ConsoleKeyInfo('q', ConsoleKey.Q, shift: false, alt: false, control: true));
                await harness.Host.RunTick(At(1));

                // Seven keys read, then the read that found the buffer empty.
                Assert.AreEqual(readsBefore + 8, harness.Host.Reader.TryReadKeyCallCount);
                Assert.IsNull(harness.ConsoleManager.StopRequest);
                Assert.AreEqual(0, harness.Host.Reader.PendingKeyCount);
                Assert.AreEqual(ExecutionStatus.Running, harness.State.ExecutionStatus);
                Assert.IsFalse(harness.State.CancellationToken.IsCancellationRequested);
                Assert.IsNull(harness.State.ExecutionStoppedReason);
                Assert.HasCount(3, harness.Writer.Writes);

                await harness.ConsoleManager.StopRealtimeConsoleOutput();
            })
            .Step("A repeated q — twice on one tick, again on a later one — leaves the stop as it is, and a key pressed once the live view has stopped is never read", async context =>
            {
                var harness = new Harness();
                harness.ConsoleManager.StartRealtimeConsoleOutputIfEnabled();
                await harness.Host.WaitForParkedTick();
                harness.Collector.MarkPhaseAsStarted(LoadTestPhase.Init, At(0));

                harness.Host.Reader.Press('q');
                harness.Host.Reader.Press('q');
                await harness.Host.RunTick(At(1));
                await harness.WaitForStopRequest();
                harness.AssertStoppedLikeCtrlC();
                Assert.AreEqual(0, harness.Host.Reader.PendingKeyCount);

                harness.Host.Reader.Press('q');
                await harness.Host.RunTick(At(1.25));
                harness.AssertStoppedLikeCtrlC();
                Assert.AreEqual(0, harness.Host.Reader.PendingKeyCount);
                Assert.HasCount(4, harness.Writer.Writes);

                harness.CompleteInitAndStartMeasurement(At(1.5));
                harness.CompleteMeasurementAndCleanup(At(2), At(3));
                harness.Host.UtcNow = At(4);
                await harness.ConsoleManager.StopRealtimeConsoleOutput();
                var readsAfterStop = harness.Host.Reader.TryReadKeyCallCount;

                // Only the loop reads keys: once it has stopped, a key press stays unread.
                harness.Host.Reader.Press('q');
                await harness.ConsoleManager.Complete();
                Assert.AreEqual(readsAfterStop, harness.Host.Reader.TryReadKeyCallCount);
                Assert.AreEqual(1, harness.Host.Reader.PendingKeyCount);
                harness.AssertEnteredAndRestoredOnce();
                harness.AssertSummaryFollowsRestore();
            })
            .Step("A failing key read is a live view failure like any other: the terminal is restored at once, the run goes on, and Complete reports it after the summary", async context =>
            {
                var harness = new Harness();
                harness.ConsoleManager.StartRealtimeConsoleOutputIfEnabled();
                await harness.Host.WaitForParkedTick();
                harness.Collector.MarkPhaseAsStarted(LoadTestPhase.Init, At(0));

                harness.Host.Reader.ReadFailure = new InvalidOperationException("Cannot read keys when input is redirected.");
                harness.Host.UtcNow = At(1);
                harness.Host.Release();
                await harness.WaitForRestore();

                harness.AssertEnteredAndRestoredOnce();
                Assert.AreEqual(ExecutionStatus.Running, harness.State.ExecutionStatus);

                harness.CompleteInitAndStartMeasurement(At(2));
                harness.CompleteMeasurementAndCleanup(At(3), At(4));
                harness.Host.UtcNow = At(5);
                var thrown = await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () => await harness.ConsoleManager.Complete());
                Assert.AreEqual("Cannot read keys when input is redirected.", thrown.Message);

                harness.AssertEnteredAndRestoredOnce();
                harness.AssertSummaryFollowsRestore();
                var failureLine = Assert.ContainsSingle(harness.MarkupEvents());
                Assert.AreEqual("[red]Live view failed: InvalidOperationException: Cannot read keys when input is redirected.[/]", failureLine);
            })
            .Run();
    }

    [Test]
    public async Task Verify_a_failing_stop_request_is_reported_after_the_summary_and_a_missing_reader_fails_at_start()
    {
        await Scenario()
            .Step("A cancellation callback that throws on the quit key's stop request does not take the loop down: it renders on, Stop observes the failure, and Complete reports it after the summary and throws it since the run has no failure of its own", async context =>
            {
                var harness = new Harness();
                // Registered after Init's status registration, so it runs before it: the status
                // still lands on Stopped and the failure is aggregated onto the request's task.
                harness.State.CancellationToken.Register(() => throw new InvalidOperationException("Producer callback failed"));
                harness.ConsoleManager.StartRealtimeConsoleOutputIfEnabled();
                await harness.Host.WaitForParkedTick();
                harness.Collector.MarkPhaseAsStarted(LoadTestPhase.Init, At(0));

                harness.Host.Reader.Press('q');
                await harness.Host.RunTick(At(1));
                await harness.WaitForStopRequest();
                harness.AssertStoppedLikeCtrlC();
                var stopRequest = harness.ConsoleManager.StopRequest;
                Assert.IsNotNull(stopRequest);
                Assert.IsTrue(stopRequest.IsFaulted);

                // The loop renders on: the failure is the stop's to report, not the loop's.
                await harness.Host.RunTick(At(1.25));
                Assert.HasCount(4, harness.Writer.Writes);

                harness.CompleteInitAndStartMeasurement(At(1.5));
                harness.CompleteMeasurementAndCleanup(At(2), At(3));
                harness.Host.UtcNow = At(4);
                var thrown = await Assert.ThrowsExactlyAsync<AggregateException>(async () => await harness.ConsoleManager.Complete());
                var callbackFailure = Assert.ContainsSingle(thrown.InnerExceptions);
                Assert.AreEqual("Producer callback failed", callbackFailure.Message);

                harness.AssertEnteredAndRestoredOnce();
                harness.AssertSummaryFollowsRestore();
                var failureLine = Assert.ContainsSingle(harness.MarkupEvents());
                Assert.StartsWith("[red]Live view failed: AggregateException: ", failureLine);
                Assert.Contains("Producer callback failed", failureLine);
            })
            .Step("A host that hands out no terminal reader fails loud at start, before the alternate screen is entered; a later Complete still writes the summary", async context =>
            {
                var harness = new Harness();
                harness.Host.ReturnsNoReader = true;
                harness.Collector.MarkPhaseAsStarted(LoadTestPhase.Init, At(0));

                var thrown = Assert.ThrowsExactly<InvalidOperationException>(() => harness.ConsoleManager.StartRealtimeConsoleOutputIfEnabled());
                Assert.AreEqual("The live view host returned no terminal reader.", thrown.Message);
                Assert.AreEqual(1, harness.Host.CreateTerminalReaderCallCount);
                Assert.IsEmpty(harness.Writer.Writes);

                harness.CompleteInitAndStartMeasurement(At(1));
                harness.CompleteMeasurementAndCleanup(At(2), At(3));
                await harness.ConsoleManager.Complete();

                Assert.IsEmpty(harness.Writer.Writes);
                Assert.AreEqual(1, harness.Events.Count(eventName => eventName == SummaryEvent));
            })
            .Run();
    }

    /// <summary>
    /// One hermetic console manager over a real in-memory collector: the execution state (which
    /// owns the scenario's <see cref="ScenarioLoadCollector"/>), the fake host and framework
    /// adapter, and the shared event log both write to — terminal writes prefixed
    /// <c>terminal:</c>, the adapter's summary as <c>summary</c> and its markup lines prefixed
    /// <c>markup:</c> — so ordering across the two can be asserted.
    /// </summary>
    private sealed class Harness
    {
        private readonly TaskCompletionSource _restored = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        public List<string> Events { get; } = new List<string>();
        public FakeLiveViewHost Host { get; }
        public FakeTestFrameworkAdapter TestFramework { get; }
        public Scenario Scenario { get; }
        public TestExecutionState State { get; }
        public ScenarioLoadCollector Collector { get; }
        public ConsoleManager ConsoleManager { get; }

        public FakeTerminalWriter Writer => Host.Writer;

        public Harness()
        {
            Host = new FakeLiveViewHost();
            Host.Writer.WriteObserver = text =>
            {
                lock (Events)
                    Events.Add(TerminalEventPrefix + text);

                if (text == RestoreSequence)
                    _restored.TrySetResult();
            };
            TestFramework = new FakeTestFrameworkAdapter(Events);

            Scenario = new Scenario(ScenarioName);
            Scenario.Id = "checkout-flow";
            Scenario.Steps.Add(new Step { Name = StepName, Id = "checkout-step" });
            // A simulations action is what makes a scenario a load test; the simulations
            // themselves are added when init completes, as SetupSimulations does.
            Scenario.SimulationsAction = (scenarioContext, simulations) => Task.CompletedTask;

            // The summary's non-realtime branch links to the report directory, which the real
            // session init would have set.
            var testSession = new TestSession("console-manager-tests");
            testSession.TestsResultsDirectory = Path.Combine(Path.GetTempPath(), "TestFuznResults");
            testSession.TestRunId = "console-manager-tests-run";

            State = new TestExecutionState(testSession);
            State.Init(TestFramework, new FakeTest(), Scenario);
            Collector = State.LoadCollectors[ScenarioName];
            ConsoleManager = new ConsoleManager(State, new ConsoleWriter(), Host);
        }

        /// <summary>Init completes with the plan in place (a 10 s fixed load) and measurement starts, as the init manager does.</summary>
        public void CompleteInitAndStartMeasurement(DateTime at)
        {
            Scenario.SimulationsInternal.Add(new FixedLoadConfiguration(5, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(10)));
            Collector.MarkPhaseAsCompleted(LoadTestPhase.Init, at);
            Collector.MarkPhaseAsStarted(LoadTestPhase.Measurement, at);
        }

        public void CompleteMeasurementAndCleanup(DateTime measurementEnd, DateTime cleanupEnd)
        {
            Collector.MarkPhaseAsCompleted(LoadTestPhase.Measurement, measurementEnd);
            Collector.MarkPhaseAsStarted(LoadTestPhase.Cleanup, measurementEnd);
            Collector.MarkPhaseAsCompleted(LoadTestPhase.Cleanup, cleanupEnd);
        }

        /// <summary>Records passed measurement iterations whose single step passed, 10 ms each.</summary>
        public void RecordIterations(int count)
        {
            for (var index = 0; index < count; index++)
            {
                var iterationResult = new IterationResult();
                iterationResult.ExecuteStartTime = SyntheticLoadSnapshots.BaseTime;
                iterationResult.ExecuteEndTime = SyntheticLoadSnapshots.BaseTime + TimeSpan.FromMilliseconds(10);

                var stepResult = new StepStandardResult();
                stepResult.Name = StepName;
                stepResult.Id = "checkout-step";
                stepResult.Status = StepStatus.Passed;
                stepResult.Duration = TimeSpan.FromMilliseconds(10);
                iterationResult.StepResults.Add(StepName, stepResult);

                Collector.RecordMeasurement(TestStatus.Passed, iterationResult);
            }
        }

        /// <summary>Waits until the restore sequence has been written — the render loop's own restore after a failure.</summary>
        public async Task WaitForRestore()
        {
            await _restored.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }

        public List<string> MarkupEvents()
        {
            lock (Events)
                return Events.Where(eventName => eventName.StartsWith(MarkupEventPrefix, StringComparison.Ordinal)).Select(eventName => eventName.Substring(MarkupEventPrefix.Length)).ToList();
        }

        /// <summary>The alternate screen was entered once (the first write) and restored once (the last write); nothing follows the restore.</summary>
        public void AssertEnteredAndRestoredOnce()
        {
            Assert.IsNotEmpty(Writer.Writes);
            Assert.AreEqual(EnterSequence, Writer.Writes[0]);
            Assert.AreEqual(1, Writer.Writes.Count(write => write == EnterSequence));
            Assert.AreEqual(1, Writer.Writes.Count(write => write == RestoreSequence));
            Assert.AreEqual(RestoreSequence, Writer.Writes[Writer.Writes.Count - 1]);
        }

        /// <summary>The summary was written exactly once, after the terminal was restored.</summary>
        public void AssertSummaryFollowsRestore()
        {
            List<string> events;
            lock (Events)
                events = Events.ToList();

            Assert.AreEqual(1, events.Count(eventName => eventName == SummaryEvent));
            Assert.IsGreaterThan(events.IndexOf(TerminalEventPrefix + RestoreSequence), events.IndexOf(SummaryEvent));
        }

        /// <summary>
        /// Waits until the quit key's stop request has run the run's cancellation callbacks,
        /// whatever their outcome — the outcome is Stop's to observe. Throws when no quit key
        /// has been handled.
        /// </summary>
        public async Task WaitForStopRequest()
        {
            var stopRequest = ConsoleManager.StopRequest;
            if (stopRequest == null)
                throw new InvalidOperationException("No stop request has been made: no quit key has been handled.");

            await Task.WhenAny(stopRequest).WaitAsync(TimeSpan.FromSeconds(10));
        }

        /// <summary>
        /// The state as Ctrl+C leaves it, read from the code that handles it: the standalone
        /// adapter's CancelKeyPress handler cancels its token, which TestExecutionState.Init
        /// links, so the state's token is cancelled and Init's registration marks the status
        /// Stopped; neither a stopped reason nor a first exception is set — only the assert
        /// hooks set those. The Ctrl+C step and the quit key steps assert through this one
        /// helper, so a divergence between the two paths fails both.
        /// </summary>
        public void AssertStoppedLikeCtrlC()
        {
            Assert.AreEqual(ExecutionStatus.Stopped, State.ExecutionStatus);
            Assert.IsTrue(State.CancellationToken.IsCancellationRequested);
            Assert.IsNull(State.ExecutionStoppedReason);
            Assert.IsNull(State.FirstException);
        }
    }

    /// <summary>
    /// A hermetic <see cref="ILiveViewHost"/>: a fake writer, a fake reader the test presses
    /// keys into, hand-resolved live capabilities (interactive, ANSI, no color so frames stay
    /// plain), a clock the test sets, and a delay the test releases one tick at a time — the
    /// render loop runs on its real thread-pool task but advances only when told to, and parks
    /// in the delay after every tick, so keys pressed while it is parked are read on the next.
    /// </summary>
    private sealed class FakeLiveViewHost : ILiveViewHost
    {
        private static readonly TimeSpan ParkTimeout = TimeSpan.FromSeconds(10);

        private readonly object _gate = new object();
        private readonly SemaphoreSlim _parked = new SemaphoreSlim(0);
        private TaskCompletionSource _release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        private DateTime _utcNow = SyntheticLoadSnapshots.BaseTime;

        public FakeTerminalWriter Writer { get; } = new FakeTerminalWriter { WindowWidth = 60, WindowHeight = 8 };

        public FakeTerminalReader Reader { get; } = new FakeTerminalReader();

        public TerminalCapabilities Capabilities { get; set; } = new TerminalCapabilities(isInteractive: true, supportsAnsi: true, colorMode: ColorMode.None);

        public int DetectCapabilitiesCallCount { get; private set; }

        public int CreateTerminalReaderCallCount { get; private set; }

        /// <summary>When set, the host hands out no reader — a broken host the manager must fail loud on.</summary>
        public bool ReturnsNoReader { get; set; }

        public DateTime UtcNow
        {
            get
            {
                lock (_gate)
                    return _utcNow;
            }
            set
            {
                lock (_gate)
                    _utcNow = value;
            }
        }

        public TerminalCapabilities DetectCapabilities()
        {
            DetectCapabilitiesCallCount++;
            return Capabilities;
        }

        public ITerminalWriter CreateTerminalWriter()
        {
            return Writer;
        }

        public ITerminalReader CreateTerminalReader()
        {
            CreateTerminalReaderCallCount++;
            if (ReturnsNoReader)
                return null!;

            return Reader;
        }

        public Task Delay(TimeSpan interval, CancellationToken cancellationToken)
        {
            Task release;
            lock (_gate)
                release = _release.Task;

            _parked.Release();
            return WaitForReleaseOrCancellation(release, cancellationToken);
        }

        /// <summary>Waits until the loop has finished a tick and is parked in the delay.</summary>
        public async Task WaitForParkedTick()
        {
            if (!await _parked.WaitAsync(ParkTimeout))
                throw new TimeoutException("The live view loop did not park in the delay within " + ParkTimeout + ".");
        }

        /// <summary>Sets the clock, releases the parked loop for one tick and waits until it parks again.</summary>
        public async Task RunTick(DateTime utcNow)
        {
            UtcNow = utcNow;
            Release();
            await WaitForParkedTick();
        }

        /// <summary>Releases the parked loop without waiting — for a tick that ends the loop.</summary>
        public void Release()
        {
            TaskCompletionSource previous;
            lock (_gate)
            {
                previous = _release;
                _release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            previous.TrySetResult();
        }

        // Returns normally on cancellation, as DelayHelper.Delay does.
        private static async Task WaitForReleaseOrCancellation(Task release, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
                return;

            var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using (cancellationToken.Register(() => cancelled.TrySetResult()))
                await Task.WhenAny(release, cancelled.Task);
        }
    }

    /// <summary>A standalone-style adapter (real-time output supported) that records its summary and markup writes into the shared event log.</summary>
    private sealed class FakeTestFrameworkAdapter : ITestFrameworkAdapter
    {
        private readonly List<string> _events;
        private readonly CancellationTokenSource _cancellation = new CancellationTokenSource();

        public FakeTestFrameworkAdapter(List<string> events)
        {
            _events = events;
        }

        public bool SupportsRealTimeConsoleOutput { get; set; } = true;

        public CancellationToken CancellationToken => _cancellation.Token;

        public ConsoleColor ForegroundColor { get; set; }

        public ConsoleColor BackgroundColor { get; set; }

        public int WindowWidth => 80;

        public string TestResultsDirectory => Path.GetTempPath();

        /// <summary>What Ctrl+C does on the standalone adapter.</summary>
        public void Cancel()
        {
            _cancellation.Cancel();
        }

        public Task ExecuteTestMethod(ITest test, MethodInfo methodInfo)
        {
            return Task.CompletedTask;
        }

        public CursorPosition GetCursorPosition()
        {
            return new CursorPosition(0, 0);
        }

        public void SetCursorPosition(int left, int top)
        {
        }

        public void Write(string message, params object?[] args)
        {
            Record("write:" + message);
        }

        public void WriteTable(TableData table)
        {
            Record("table");
        }

        public void WriteMarkup(string text)
        {
            Record(MarkupEventPrefix + text);
        }

        public void WritePanel(string[] messages, string header)
        {
            Record("panel:" + header);
        }

        public void WriteAdvancedTable(AdvancedTable table)
        {
            Record("advanced-table");
        }

        public void WriteSummary(DateTime testRunStartDateTime, TimeSpan totalRunDuration, Dictionary<Scenario, ScenarioLoadResult> scenarioLoadResults)
        {
            Record(SummaryEvent);
        }

        public void SetCurrentTestAsSkipped()
        {
        }

        public void ThrowTestFuznIsNotInitializedException()
        {
            throw new InvalidOperationException("TestFuzn is not initialized.");
        }

        private void Record(string eventName)
        {
            lock (_events)
                _events.Add(eventName);
        }
    }

    private sealed class FakeTest : ITest
    {
        public object TestFramework { get; set; } = null!;

        public MethodInfo TestMethodInfo { get; set; } = null!;

        public TestInfo TestInfo { get; set; } = new TestInfo
        {
            Name = "Checkout_load",
            FullName = "Fuzn.TestFuzn.Tests.Terminal.ConsoleManagerTests.Checkout_load",
            Id = "checkout-load"
        };
    }
}
