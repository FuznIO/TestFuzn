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
using Fuzn.TestFuzn.Internals.Thresholds;

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
/// the quit key landing in the Ctrl+C stop path while the loop renders on until Stop, every
/// other key reaching the view state through the key handler and showing on that tick's
/// frame, a pause freezing the frame but not the sampling (no write while the samples keep
/// coming, a repaint over the frozen snapshots on a key — which acts on the frozen picture,
/// not on the live one — the current snapshots on resume), keys without a binding drained
/// and ignored, and the reader never consulted without a live view. On a
/// redirected or non-ANSI output the same loop writes plain stats lines instead: pinned are
/// the exact lines per sample and per phase transition with hand-derived numbers from the real
/// collector, the final line's outcome vocabulary, the summary following the final line, and
/// that neither a key nor the terminal size is ever read there.
/// </summary>
[TestClass]
public class ConsoleManagerTests : Test
{
    private const string ScenarioName = "Checkout flow";
    private const string StepName = "Checkout step";
    private const string EnterSequence = AnsiCodes.EnterAlternateScreen + AnsiCodes.HideCursor + AnsiCodes.DisableAutoWrap;
    private const string RestoreSequence = AnsiCodes.Reset + AnsiCodes.EnableAutoWrap + AnsiCodes.ShowCursor + AnsiCodes.ExitAlternateScreen;
    private const string TerminalEventPrefix = "terminal:";
    private const string SummaryEvent = FakeTestFrameworkAdapter.SummaryEvent;
    private const string MarkupEventPrefix = FakeTestFrameworkAdapter.MarkupEventPrefix;

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
    public async Task Verify_declared_thresholds_reach_every_live_snapshot()
    {
        await Scenario()
            .Step("The init placeholder carries the scenario's thresholds in their pre-sample state, and the model evaluates them on every sample from its first interval on", async context =>
            {
                var harness = new Harness();
                new ThresholdsBuilder(harness.Scenarios[0].Thresholds)
                    .ResponseTimePercentile95(TimeSpan.FromMilliseconds(5))
                    .RequestsPerSecond(minimum: 2);
                harness.ConsoleManager.StartRealtimeConsoleOutputIfEnabled();
                await harness.Host.WaitForParkedTick();

                var placeholder = Assert.ContainsSingle(harness.ConsoleManager.LiveSnapshots);
                Assert.HasCount(2, placeholder.Thresholds);
                Assert.AreEqual(ThresholdMetric.ResponseTimePercentile95, placeholder.Thresholds[0].Threshold.Metric);
                Assert.AreEqual(ThresholdMetric.RequestsPerSecond, placeholder.Thresholds[1].Threshold.Metric);
                foreach (var threshold in placeholder.Thresholds)
                {
                    Assert.AreEqual(ThresholdState.Ok, threshold.State);
                    Assert.AreEqual(0.0, threshold.Current);
                    Assert.AreEqual(TimeSpan.Zero, threshold.BreachedFor);
                }

                harness.Collector.MarkPhaseAsStarted(LoadTestPhase.Init, At(0));
                harness.CompleteInitAndStartMeasurement(At(1));
                await harness.Host.RunTick(At(2));
                Assert.HasCount(2, harness.ConsoleManager.LiveSnapshots[0].Thresholds);

                // Three 10 ms iterations in the first interval: the p95 is past its 5 ms
                // limit; 3 rps is clear of the 2 rps minimum and its 2.5 rps warning band.
                harness.RecordIterations(3);
                await harness.Host.RunTick(At(3));

                var view = harness.ConsoleManager.LiveSnapshots[0];
                Assert.ContainsSingle(view.Samples);
                var p95 = view.Thresholds[0];
                Assert.IsInRange(10.0, 10.01, p95.Current);
                Assert.AreEqual(ThresholdState.Breached, p95.State);
                Assert.AreEqual(TimeSpan.Zero, p95.BreachedFor);
                var rps = view.Thresholds[1];
                Assert.AreEqual(3.0, rps.Current, 1e-9);
                Assert.AreEqual(ThresholdState.Ok, rps.State);

                await harness.ConsoleManager.StopRealtimeConsoleOutput();
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
    public async Task Verify_no_live_view_without_real_time_support()
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
            .Run();
    }

    [Test]
    public async Task Verify_redirected_output_gets_plain_stats_lines_without_escape_sequences()
    {
        await Scenario()
            .Step("Redirected output: a phase line at every label change and one stats line per sample with the collector's numbers, nothing between samples, the final line then the summary — no escape byte, no key read, no size read", async context =>
            {
                var harness = new Harness(isOutputRedirected: true, ScenarioName);
                harness.Host.Reader.Press('q');
                harness.ConsoleManager.StartRealtimeConsoleOutputIfEnabled();
                await harness.Host.WaitForParkedTick();

                // The first sample, before init has even started: the placeholder at zero.
                CollectionAssert.AreEqual(new[]
                {
                    "[Checkout flow] phase: init",
                    "[Checkout flow] elapsed 00:00:00  total 0  ok 0  failed 0  rps N/A  p95 N/A"
                }, harness.PlainLines());

                harness.Collector.MarkPhaseAsStarted(LoadTestPhase.Init, At(0));
                await harness.Host.RunTick(At(1));
                Assert.AreEqual("[Checkout flow] elapsed 00:00:01  total 0  ok 0  failed 0  rps N/A  p95 N/A", harness.PlainLines()[2]);
                Assert.HasCount(3, harness.Writer.Writes);

                // A quarter second later is not a sample tick: nothing is written between samples.
                await harness.Host.RunTick(At(1.25));
                Assert.HasCount(3, harness.Writer.Writes);

                // Init completes with a 10 s fixed load: the label changes and the baseline
                // sample has no closed interval yet, so the rate is not data; p95 is the
                // cumulative Ok p95 of the three 10 ms requests.
                harness.CompleteInitAndStartMeasurement(At(1.5));
                harness.RecordIterations(3);
                await harness.Host.RunTick(At(2));
                harness.RecordIterations(5);
                await harness.Host.RunTick(At(3));
                harness.CompleteMeasurementAndStartCleanup(At(10));
                await harness.Host.RunTick(At(11));

                CollectionAssert.AreEqual(new[]
                {
                    "[Checkout flow] phase: Fixed Load 5 rps",
                    "[Checkout flow] elapsed 00:00:02 / 00:00:10  total 3  ok 3  failed 0  rps N/A  p95 10 ms",
                    "[Checkout flow] elapsed 00:00:03 / 00:00:10  total 8  ok 8  failed 0  rps 5.0  p95 10 ms",
                    "[Checkout flow] phase: cleanup",
                    "[Checkout flow] elapsed 00:00:11 / 00:00:10  total 8  ok 8  failed 0  rps 0.0  p95 10 ms"
                }, harness.PlainLines().Skip(3).ToList());

                harness.CompleteCleanup(At(12));
                harness.Host.UtcNow = At(13);
                await harness.ConsoleManager.Complete();

                // The final Record lands "completed" and the frozen duration; it is written as
                // the final line, not as a phase line, and the summary follows it.
                var lines = harness.PlainLines();
                Assert.HasCount(9, lines);
                Assert.AreEqual("[Checkout flow] completed  elapsed 00:00:12  total 8  ok 8  failed 0  p95 10 ms", lines[8]);
                Assert.AreEqual("completed", harness.ConsoleManager.LiveSnapshots[0].PhaseLabel);
                harness.AssertSummaryFollowsLastTerminalWrite();
                Assert.IsEmpty(harness.MarkupEvents());

                Assert.AreEqual(1, harness.Host.DetectCapabilitiesCallCount);
                Assert.AreEqual(0, harness.Host.CreateTerminalReaderCallCount);
                Assert.AreEqual(0, harness.Host.Reader.TryReadKeyCallCount);
                Assert.AreEqual(1, harness.Host.Reader.PendingKeyCount);
                Assert.AreEqual(0, harness.Writer.WindowWidthReadCount);
                Assert.AreEqual(0, harness.Writer.WindowHeightReadCount);
                Assert.AreEqual(ExecutionStatus.Running, harness.State.ExecutionStatus);
            })
            .Step("An interactive terminal without ANSI support (TERM=dumb) gets the plain lines too: the gate is live view support, not interactivity", async context =>
            {
                var harness = new Harness();
                harness.Host.Capabilities = TerminalCapabilities.Resolve(isOutputRedirected: false, isInputRedirected: false, isVirtualTerminalEnabled: true, term: "dumb", colorTerm: null, noColor: null);
                harness.ConsoleManager.StartRealtimeConsoleOutputIfEnabled();
                await harness.Host.WaitForParkedTick();

                CollectionAssert.AreEqual(new[]
                {
                    "[Checkout flow] phase: init",
                    "[Checkout flow] elapsed 00:00:00  total 0  ok 0  failed 0  rps N/A  p95 N/A"
                }, harness.PlainLines());
                Assert.AreEqual(0, harness.Host.CreateTerminalReaderCallCount);
                Assert.AreEqual(0, harness.Writer.WindowWidthReadCount);
                Assert.AreEqual(0, harness.Writer.WindowHeightReadCount);

                await harness.ConsoleManager.StopRealtimeConsoleOutput();
            })
            .Step("A run stopped through the framework token keeps logging until Stop and ends with the stopped line, then the summary", async context =>
            {
                var harness = new Harness(isOutputRedirected: true, ScenarioName);
                harness.ConsoleManager.StartRealtimeConsoleOutputIfEnabled();
                await harness.Host.WaitForParkedTick();

                harness.Collector.MarkPhaseAsStarted(LoadTestPhase.Init, At(0));
                harness.TestFramework.Cancel();
                harness.AssertStoppedLikeCtrlC();

                await harness.Host.RunTick(At(1));
                Assert.AreEqual("[Checkout flow] elapsed 00:00:01  total 0  ok 0  failed 0  rps N/A  p95 N/A", harness.PlainLines()[2]);

                harness.CompleteInitAndStartMeasurement(At(1.5));
                harness.CompleteMeasurementAndCleanup(At(2), At(3));
                harness.Host.UtcNow = At(4);
                await harness.ConsoleManager.Complete();

                var lines = harness.PlainLines();
                Assert.HasCount(4, lines);
                Assert.AreEqual("[Checkout flow] stopped  elapsed 00:00:03  total 0  ok 0  failed 0  p95 N/A", lines[3]);
                harness.AssertSummaryFollowsLastTerminalWrite();
                Assert.IsEmpty(harness.MarkupEvents());
            })
            .Step("A scenario failed by an assert logs the reason once when it appears and ends with the failed line carrying it — failed wins over the stop the assert caused", async context =>
            {
                var harness = new Harness(isOutputRedirected: true, ScenarioName);
                harness.ConsoleManager.StartRealtimeConsoleOutputIfEnabled();
                await harness.Host.WaitForParkedTick();

                harness.Collector.MarkPhaseAsStarted(LoadTestPhase.Init, At(0));
                harness.CompleteInitAndStartMeasurement(At(1));
                harness.RecordIterations(2);
                await harness.Host.RunTick(At(2));

                harness.FailScenario(harness.Collector, "Assert while running failed: p95 above 5 ms");
                await harness.Host.RunTick(At(3));
                await harness.Host.RunTick(At(4));

                CollectionAssert.AreEqual(new[]
                {
                    "[Checkout flow] phase: Fixed Load 5 rps",
                    "[Checkout flow] elapsed 00:00:02 / 00:00:10  total 2  ok 2  failed 0  rps N/A  p95 10 ms",
                    "[Checkout flow] failed: Assert while running failed: p95 above 5 ms",
                    "[Checkout flow] elapsed 00:00:03 / 00:00:10  total 2  ok 2  failed 0  rps 0.0  p95 10 ms",
                    "[Checkout flow] elapsed 00:00:04 / 00:00:10  total 2  ok 2  failed 0  rps 0.0  p95 10 ms"
                }, harness.PlainLines().Skip(2).ToList());

                harness.CompleteMeasurementAndCleanup(At(5), At(6));
                harness.Host.UtcNow = At(7);
                await harness.ConsoleManager.Complete();

                var lines = harness.PlainLines();
                Assert.HasCount(8, lines);
                Assert.AreEqual("[Checkout flow] failed  elapsed 00:00:06  total 2  ok 2  failed 0  p95 10 ms  reason: Assert while running failed: p95 above 5 ms", lines[7]);
                Assert.AreEqual(ExecutionStatus.Stopped, harness.State.ExecutionStatus);
                harness.AssertSummaryFollowsLastTerminalWrite();
                Assert.IsEmpty(harness.MarkupEvents());
            })
            .Run();
    }

    [Test]
    public async Task Verify_redirected_output_writes_one_line_per_scenario_per_sample_in_scenario_order()
    {
        await Scenario()
            .Step("Two scenarios: every sample writes each scenario's lines in scenario order, transitions tracked per scenario, and the final lines follow the same order", async context =>
            {
                var harness = new Harness(isOutputRedirected: true, ScenarioName, "Search flow");
                harness.ConsoleManager.StartRealtimeConsoleOutputIfEnabled();
                await harness.Host.WaitForParkedTick();

                CollectionAssert.AreEqual(new[]
                {
                    "[Checkout flow] phase: init",
                    "[Checkout flow] elapsed 00:00:00  total 0  ok 0  failed 0  rps N/A  p95 N/A",
                    "[Search flow] phase: init",
                    "[Search flow] elapsed 00:00:00  total 0  ok 0  failed 0  rps N/A  p95 N/A"
                }, harness.PlainLines());

                foreach (var collector in harness.Collectors)
                    collector.MarkPhaseAsStarted(LoadTestPhase.Init, At(0));

                harness.CompleteInitAndStartMeasurement(At(1));
                harness.RecordIterations(harness.Collectors[0], 3);
                harness.RecordIterations(harness.Collectors[1], 1);
                await harness.Host.RunTick(At(2));
                harness.RecordIterations(harness.Collectors[1], 2);
                await harness.Host.RunTick(At(3));

                CollectionAssert.AreEqual(new[]
                {
                    "[Checkout flow] phase: Fixed Load 5 rps",
                    "[Checkout flow] elapsed 00:00:02 / 00:00:10  total 3  ok 3  failed 0  rps N/A  p95 10 ms",
                    "[Search flow] phase: Fixed Load 5 rps",
                    "[Search flow] elapsed 00:00:02 / 00:00:10  total 1  ok 1  failed 0  rps N/A  p95 10 ms",
                    "[Checkout flow] elapsed 00:00:03 / 00:00:10  total 3  ok 3  failed 0  rps 0.0  p95 10 ms",
                    "[Search flow] elapsed 00:00:03 / 00:00:10  total 3  ok 3  failed 0  rps 2.0  p95 10 ms"
                }, harness.PlainLines().Skip(4).ToList());

                harness.CompleteMeasurementAndCleanup(At(10), At(12));
                harness.Host.UtcNow = At(13);
                await harness.ConsoleManager.Complete();

                var lines = harness.PlainLines();
                Assert.HasCount(12, lines);
                Assert.AreEqual("[Checkout flow] completed  elapsed 00:00:12  total 3  ok 3  failed 0  p95 10 ms", lines[10]);
                Assert.AreEqual("[Search flow] completed  elapsed 00:00:12  total 3  ok 3  failed 0  p95 10 ms", lines[11]);
                harness.AssertSummaryFollowsLastTerminalWrite();
                Assert.AreEqual(0, harness.Host.CreateTerminalReaderCallCount);
                Assert.AreEqual(0, harness.Writer.WindowWidthReadCount);
                Assert.AreEqual(0, harness.Writer.WindowHeightReadCount);
            })
            .Run();
    }

    [Test]
    public async Task Verify_plain_lines_write_failure_ends_the_live_view_and_is_reported_after_the_summary()
    {
        await Scenario()
            .Step("A failing write ends the loop on that sample, the run goes on without a final line, and Complete writes the summary, reports the failure and rethrows it since the run has no failure of its own", async context =>
            {
                var harness = new Harness(isOutputRedirected: true, ScenarioName);
                harness.ConsoleManager.StartRealtimeConsoleOutputIfEnabled();
                await harness.Host.WaitForParkedTick();
                Assert.HasCount(2, harness.Writer.Writes);

                harness.Collector.MarkPhaseAsStarted(LoadTestPhase.Init, At(0));
                harness.Writer.WriteFailure = new IOException("Broken pipe");
                harness.Host.UtcNow = At(1);
                harness.Host.Release();
                await harness.WaitForLiveViewLoopToEnd();

                // The sample was taken before its first write failed: the view moved on, the
                // log did not, and nothing was retried.
                Assert.AreEqual(1, harness.Writer.FailedWriteCount);
                Assert.HasCount(2, harness.Writer.Writes);
                Assert.AreEqual(TimeSpan.FromSeconds(1), harness.ConsoleManager.LiveSnapshots[0].Duration);
                Assert.AreEqual(ExecutionStatus.Running, harness.State.ExecutionStatus);

                harness.CompleteInitAndStartMeasurement(At(2));
                harness.CompleteMeasurementAndCleanup(At(3), At(4));
                harness.Host.UtcNow = At(5);
                var thrown = await Assert.ThrowsExactlyAsync<IOException>(async () => await harness.ConsoleManager.Complete());
                Assert.AreEqual("Broken pipe", thrown.Message);

                // No final Record on a failed live view, so no final line was attempted either.
                Assert.AreEqual(1, harness.Writer.FailedWriteCount);
                Assert.HasCount(2, harness.Writer.Writes);
                Assert.AreEqual(TimeSpan.FromSeconds(1), harness.ConsoleManager.LiveSnapshots[0].Duration);
                Assert.AreEqual(1, harness.Events.Count(eventName => eventName == SummaryEvent));
                var failureLine = Assert.ContainsSingle(harness.MarkupEvents());
                Assert.AreEqual("[red]Live view failed: IOException: Broken pipe[/]", failureLine);
                Assert.IsGreaterThan(harness.Events.IndexOf(SummaryEvent), harness.Events.IndexOf(MarkupEventPrefix + failureLine));
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
                // Enter and the arrow found no step on view, so the view state is untouched too.
                Assert.AreEqual(LiveDashboardView.Overview, harness.ConsoleManager.ViewState.View);
                Assert.IsNull(harness.ConsoleManager.ViewState.SelectedStepIndex);
                Assert.IsFalse(harness.ConsoleManager.ViewState.IsPaused);

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
    public async Task Verify_keys_reach_the_view_state_and_a_pause_freezes_the_frame_but_not_the_sampling()
    {
        await Scenario()
            .Step("p pauses on its tick: that frame carries the badge, the samples keep coming while no frame is written, and p again writes a frame from the current snapshots", async context =>
            {
                var harness = new Harness();
                // Twelve rows show both tile rows: the elapsed clock is on the second row's value line.
                harness.Writer.WindowHeight = 12;
                harness.ConsoleManager.StartRealtimeConsoleOutputIfEnabled();
                await harness.Host.WaitForParkedTick();
                harness.Collector.MarkPhaseAsStarted(LoadTestPhase.Init, At(0));
                await harness.Host.RunTick(At(1));
                Assert.HasCount(3, harness.Writer.Writes);
                Assert.Contains("00:00:01", harness.Writer.Writes[2]);
                Assert.IsFalse(harness.ConsoleManager.ViewState.IsPaused);

                harness.Host.Reader.Press(LiveDashboardKeyHandler.PauseKey);
                await harness.Host.RunTick(At(1.25));
                Assert.IsTrue(harness.ConsoleManager.ViewState.IsPaused);
                Assert.HasCount(4, harness.Writer.Writes);
                Assert.Contains(LiveDashboardLayout.PausedBadgeText, harness.Writer.Writes[3]);

                // Three samples arrive while paused: the view moves on, the terminal does not.
                await harness.Host.RunTick(At(2));
                await harness.Host.RunTick(At(3));
                await harness.Host.RunTick(At(4));
                await harness.Host.RunTick(At(4.25));
                Assert.AreEqual(TimeSpan.FromSeconds(4), harness.ConsoleManager.LiveSnapshots[0].Duration);
                Assert.HasCount(4, harness.Writer.Writes);

                harness.Host.Reader.Press(LiveDashboardKeyHandler.PauseKey);
                await harness.Host.RunTick(At(4.5));
                Assert.IsFalse(harness.ConsoleManager.ViewState.IsPaused);
                Assert.HasCount(5, harness.Writer.Writes);
                Assert.DoesNotContain(LiveDashboardLayout.PausedBadgeText, harness.Writer.Writes[4]);
                Assert.Contains("00:00:04", harness.Writer.Writes[4]);

                // And the spinner is back to a frame per tick.
                await harness.Host.RunTick(At(4.75));
                Assert.HasCount(6, harness.Writer.Writes);

                await harness.ConsoleManager.StopRealtimeConsoleOutput();
            })
            .Step("A key while paused repaints over the frozen snapshots — the help footer is the only line written, the clock stands — and resuming jumps to now", async context =>
            {
                var harness = new Harness();
                harness.Writer.WindowHeight = 12;
                harness.ConsoleManager.StartRealtimeConsoleOutputIfEnabled();
                await harness.Host.WaitForParkedTick();
                harness.Collector.MarkPhaseAsStarted(LoadTestPhase.Init, At(0));
                await harness.Host.RunTick(At(1));
                harness.Host.Reader.Press(LiveDashboardKeyHandler.PauseKey);
                await harness.Host.RunTick(At(1.25));
                await harness.Host.RunTick(At(2));
                Assert.HasCount(4, harness.Writer.Writes);
                Assert.AreEqual(TimeSpan.FromSeconds(2), harness.ConsoleManager.LiveSnapshots[0].Duration);

                harness.Host.Reader.Press(new ConsoleKeyInfo(LiveDashboardKeyHandler.HelpKey, ConsoleKey.Oem2, shift: true, alt: false, control: false));
                await harness.Host.RunTick(At(2.25));

                Assert.IsTrue(harness.ConsoleManager.ViewState.ShowHelp);
                Assert.IsTrue(harness.ConsoleManager.ViewState.IsPaused);
                Assert.HasCount(5, harness.Writer.Writes);
                Assert.AreEqual(
                    AnsiCodes.BeginSynchronizedOutput + AnsiCodes.MoveCursor(12, 1) + AnsiCodes.Reset + AnsiCodes.EraseLine + "? close · Esc close · q quit" + AnsiCodes.EndSynchronizedOutput,
                    harness.Writer.Writes[4]);

                await harness.Host.RunTick(At(3));
                Assert.HasCount(5, harness.Writer.Writes);

                harness.Host.Reader.Press(LiveDashboardKeyHandler.PauseKey);
                await harness.Host.RunTick(At(3.25));
                Assert.HasCount(6, harness.Writer.Writes);
                Assert.Contains("00:00:03", harness.Writer.Writes[5]);
                Assert.DoesNotContain(LiveDashboardLayout.PausedBadgeText, harness.Writer.Writes[5]);

                await harness.ConsoleManager.StopRealtimeConsoleOutput();
            })
            .Step("3, the arrows, Escape, Enter and 1 reach the state on their tick, the footer follows the view, and the step keys wait for a step to be on view", async context =>
            {
                // At 60 columns the log's footer keeps four hints before the quit hint (51
                // columns; the fifth would make it 61), the overview's four (51) and the
                // step detail's four (51).
                var harness = new Harness();
                harness.ConsoleManager.StartRealtimeConsoleOutputIfEnabled();
                await harness.Host.WaitForParkedTick();
                harness.Collector.MarkPhaseAsStarted(LoadTestPhase.Init, At(0));

                harness.Host.Reader.Press('3');
                await harness.Host.RunTick(At(1));
                Assert.AreEqual(LiveDashboardView.ErrorLog, harness.ConsoleManager.ViewState.View);
                Assert.Contains("Esc back · ↑↓ scroll · 1 overview · 2 step · q quit", harness.Writer.Writes[2]);

                harness.Host.Reader.Press(ConsoleKey.DownArrow);
                harness.Host.Reader.Press(ConsoleKey.DownArrow);
                await harness.Host.RunTick(At(1.25));
                Assert.AreEqual(2, harness.ConsoleManager.ViewState.ErrorLogScroll);
                Assert.IsNull(harness.ConsoleManager.ViewState.SelectedStepIndex);

                harness.Host.Reader.Press(ConsoleKey.Escape);
                await harness.Host.RunTick(At(1.5));
                Assert.AreEqual(LiveDashboardView.Overview, harness.ConsoleManager.ViewState.View);
                Assert.Contains("1 overview · 2 step · 3 errors · ↑↓ select · q quit", harness.Writer.Writes[4]);

                // The init placeholder has no step, so Enter and the arrow select nothing.
                harness.Host.Reader.Press(ConsoleKey.DownArrow);
                harness.Host.Reader.Press(ConsoleKey.Enter);
                await harness.Host.RunTick(At(1.75));
                Assert.AreEqual(LiveDashboardView.Overview, harness.ConsoleManager.ViewState.View);
                Assert.IsNull(harness.ConsoleManager.ViewState.SelectedStepIndex);

                // Once the model publishes the scenario's step, the arrow selects it and Enter
                // opens its detail.
                harness.CompleteInitAndStartMeasurement(At(1.8));
                harness.RecordIterations(3);
                await harness.Host.RunTick(At(2));
                Assert.ContainsSingle(harness.ConsoleManager.LiveSnapshots[0].Steps);

                harness.Host.Reader.Press(ConsoleKey.DownArrow);
                harness.Host.Reader.Press(ConsoleKey.Enter);
                await harness.Host.RunTick(At(2.25));
                Assert.AreEqual(LiveDashboardView.StepDetail, harness.ConsoleManager.ViewState.View);
                Assert.AreEqual(0, harness.ConsoleManager.ViewState.SelectedStepIndex);
                Assert.Contains("Esc back · ↑↓ step · 1 overview · 3 errors · q quit", harness.Writer.Writes[harness.Writer.Writes.Count - 1]);

                harness.Host.Reader.Press('1');
                await harness.Host.RunTick(At(2.5));
                Assert.AreEqual(LiveDashboardView.Overview, harness.ConsoleManager.ViewState.View);
                Assert.AreEqual(0, harness.ConsoleManager.ViewState.SelectedStepIndex);

                await harness.ConsoleManager.StopRealtimeConsoleOutput();
            })
            .Step("A key while paused acts on the frozen picture, not the live one: paused before the run's step is on view, ↓ and Enter select nothing although the live picture has the step by then, and the same keys reach it once the pause ends", async context =>
            {
                var harness = new Harness();
                harness.ConsoleManager.StartRealtimeConsoleOutputIfEnabled();
                await harness.Host.WaitForParkedTick();
                harness.Collector.MarkPhaseAsStarted(LoadTestPhase.Init, At(0));

                // The pause begins on the init placeholder, which has no step.
                harness.Host.Reader.Press(LiveDashboardKeyHandler.PauseKey);
                await harness.Host.RunTick(At(1));
                Assert.IsTrue(harness.ConsoleManager.ViewState.IsPaused);
                Assert.IsEmpty(harness.ConsoleManager.LiveSnapshots[0].Steps);

                // The run finds its step while the picture is frozen without one.
                harness.CompleteInitAndStartMeasurement(At(1.5));
                harness.RecordIterations(3);
                await harness.Host.RunTick(At(2));
                Assert.ContainsSingle(harness.ConsoleManager.LiveSnapshots[0].Steps);

                // Over the live picture the arrow would select the step and Enter open its
                // detail; the frozen picture has no step to select, so neither moves.
                harness.Host.Reader.Press(ConsoleKey.DownArrow);
                harness.Host.Reader.Press(ConsoleKey.Enter);
                await harness.Host.RunTick(At(2.25));
                Assert.IsNull(harness.ConsoleManager.ViewState.SelectedStepIndex);
                Assert.AreEqual(LiveDashboardView.Overview, harness.ConsoleManager.ViewState.View);

                // Resumed, the picture shows the step and the same keys reach it.
                harness.Host.Reader.Press(LiveDashboardKeyHandler.PauseKey);
                await harness.Host.RunTick(At(2.5));
                Assert.IsFalse(harness.ConsoleManager.ViewState.IsPaused);

                harness.Host.Reader.Press(ConsoleKey.DownArrow);
                harness.Host.Reader.Press(ConsoleKey.Enter);
                await harness.Host.RunTick(At(2.75));
                Assert.AreEqual(0, harness.ConsoleManager.ViewState.SelectedStepIndex);
                Assert.AreEqual(LiveDashboardView.StepDetail, harness.ConsoleManager.ViewState.View);

                await harness.ConsoleManager.StopRealtimeConsoleOutput();
            })
            .Step("The quit key still stops the run through the Ctrl+C path and leaves the view state as the other keys left it", async context =>
            {
                var harness = new Harness();
                harness.ConsoleManager.StartRealtimeConsoleOutputIfEnabled();
                await harness.Host.WaitForParkedTick();
                harness.Collector.MarkPhaseAsStarted(LoadTestPhase.Init, At(0));

                harness.Host.Reader.Press('3');
                harness.Host.Reader.Press('q');
                await harness.Host.RunTick(At(1));
                await harness.WaitForStopRequest();

                harness.AssertStoppedLikeCtrlC();
                Assert.AreEqual(LiveDashboardView.ErrorLog, harness.ConsoleManager.ViewState.View);
                Assert.IsFalse(harness.ConsoleManager.ViewState.IsPaused);

                await harness.ConsoleManager.Complete();
                harness.AssertEnteredAndRestoredOnce();
                harness.AssertSummaryFollowsRestore();
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
    /// One hermetic console manager over real in-memory collectors: the execution state (which
    /// owns each scenario's <see cref="ScenarioLoadCollector"/>), the fake host and framework
    /// adapter, and the shared event log both write to — terminal writes prefixed
    /// <c>terminal:</c>, the adapter's summary as <c>summary</c> and its markup lines prefixed
    /// <c>markup:</c> — so ordering across the two can be asserted. One scenario by default;
    /// the phase helpers drive every scenario in lockstep, as the managers do.
    /// </summary>
    private sealed class Harness
    {
        private readonly TaskCompletionSource _restored = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        public List<string> Events { get; } = new List<string>();
        public FakeLiveViewHost Host { get; }
        public FakeTestFrameworkAdapter TestFramework { get; }
        public IReadOnlyList<Scenario> Scenarios { get; }
        public TestExecutionState State { get; }
        public IReadOnlyList<ScenarioLoadCollector> Collectors { get; }
        public ConsoleManager ConsoleManager { get; }

        /// <summary>The first scenario's collector.</summary>
        public ScenarioLoadCollector Collector => Collectors[0];

        public FakeTerminalWriter Writer => Host.Writer;

        public Harness()
            : this(isOutputRedirected: false, ScenarioName)
        {
        }

        /// <summary>
        /// <paramref name="isOutputRedirected"/> resolves the host's capabilities as a redirected
        /// output and input do (docker logs, CI) — no live view support — instead of the
        /// interactive default; one scenario per name, each with one step.
        /// </summary>
        public Harness(bool isOutputRedirected, params string[] scenarioNames)
        {
            Host = new FakeLiveViewHost();
            if (isOutputRedirected)
                Host.Capabilities = TerminalCapabilities.Resolve(isOutputRedirected: true, isInputRedirected: true, isVirtualTerminalEnabled: true, term: "xterm-256color", colorTerm: null, noColor: null);

            Host.Writer.WriteObserver = text =>
            {
                lock (Events)
                    Events.Add(TerminalEventPrefix + text);

                if (text == RestoreSequence)
                    _restored.TrySetResult();
            };
            TestFramework = new FakeTestFrameworkAdapter(Events);

            var scenarios = new List<Scenario>();
            foreach (var scenarioName in scenarioNames)
            {
                var scenario = new Scenario(scenarioName);
                scenario.Id = scenarioName.ToLowerInvariant().Replace(' ', '-');
                scenario.Steps.Add(new Step { Name = StepName, Id = "checkout-step" });
                // A simulations action is what makes a scenario a load test; the simulations
                // themselves are added when init completes, as SetupSimulations does.
                scenario.SimulationsAction = (scenarioContext, simulations) => Task.CompletedTask;
                scenarios.Add(scenario);
            }

            Scenarios = scenarios;

            // The summary's non-realtime branch links to the report directory, which the real
            // session init would have set.
            var testSession = new TestSession("console-manager-tests");
            testSession.TestsResultsDirectory = Path.Combine(Path.GetTempPath(), "TestFuznResults");
            testSession.TestRunId = "console-manager-tests-run";

            State = new TestExecutionState(testSession);
            State.Init(TestFramework, new FakeTest(), scenarios.ToArray());

            var collectors = new List<ScenarioLoadCollector>();
            foreach (var scenario in scenarios)
                collectors.Add(State.LoadCollectors[scenario.Name]);

            Collectors = collectors;
            ConsoleManager = new ConsoleManager(State, new ConsoleWriter(), Host);
        }

        /// <summary>Init completes with the plan in place (a 10 s fixed load) and measurement starts for every scenario, as the init manager does.</summary>
        public void CompleteInitAndStartMeasurement(DateTime at)
        {
            for (var index = 0; index < Scenarios.Count; index++)
            {
                Scenarios[index].SimulationsInternal.Add(new FixedLoadConfiguration(5, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(10)));
                Collectors[index].MarkPhaseAsCompleted(LoadTestPhase.Init, at);
                Collectors[index].MarkPhaseAsStarted(LoadTestPhase.Measurement, at);
            }
        }

        public void CompleteMeasurementAndCleanup(DateTime measurementEnd, DateTime cleanupEnd)
        {
            CompleteMeasurementAndStartCleanup(measurementEnd);
            CompleteCleanup(cleanupEnd);
        }

        /// <summary>Measurement completes and cleanup starts for every scenario, as the consumer manager and the cleanup manager do.</summary>
        public void CompleteMeasurementAndStartCleanup(DateTime at)
        {
            foreach (var collector in Collectors)
            {
                collector.MarkPhaseAsCompleted(LoadTestPhase.Measurement, at);
                collector.MarkPhaseAsStarted(LoadTestPhase.Cleanup, at);
            }
        }

        public void CompleteCleanup(DateTime at)
        {
            foreach (var collector in Collectors)
                collector.MarkPhaseAsCompleted(LoadTestPhase.Cleanup, at);
        }

        /// <summary>Records passed measurement iterations on the first scenario whose single step passed, 10 ms each.</summary>
        public void RecordIterations(int count)
        {
            RecordIterations(Collector, count);
        }

        /// <summary>Records passed measurement iterations on the given scenario's collector whose single step passed, 10 ms each.</summary>
        public void RecordIterations(ScenarioLoadCollector collector, int count)
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

                collector.RecordMeasurement(TestStatus.Passed, iterationResult);
            }
        }

        /// <summary>
        /// Fails a scenario the way an assert-while-running failure does in the scenario message
        /// handler: the execution stops with the assert exception as its reason and first
        /// exception, and the collector records the exception and the Failed status.
        /// </summary>
        public void FailScenario(ScenarioLoadCollector collector, string reason)
        {
            var exception = new InvalidOperationException(reason);
            State.ExecutionStatus = ExecutionStatus.Stopped;
            State.ExecutionStoppedReason = exception;
            State.FirstException = exception;
            collector.SetAssertWhileRunningException(exception);
            collector.SetStatus(TestStatus.Failed);
        }

        /// <summary>Waits until the restore sequence has been written — the render loop's own restore after a failure.</summary>
        public async Task WaitForRestore()
        {
            await _restored.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }

        /// <summary>Waits until the live view loop has ended by itself — the plain-lines loop after a failure, which restores nothing. Throws when no live view was started.</summary>
        public async Task WaitForLiveViewLoopToEnd()
        {
            var liveViewLoop = ConsoleManager.LiveViewLoop;
            if (liveViewLoop == null)
                throw new InvalidOperationException("No live view loop has started.");

            await liveViewLoop.WaitAsync(TimeSpan.FromSeconds(10));
        }

        /// <summary>
        /// The lines written so far, in order, each write checked to be one plain line: no
        /// escape byte anywhere, ending in exactly one line terminator and carrying no other
        /// line break. Returned without the terminators.
        /// </summary>
        public List<string> PlainLines()
        {
            var lines = new List<string>();
            foreach (var write in Writer.Writes)
            {
                Assert.DoesNotContain("\u001b", write);
                Assert.EndsWith(Environment.NewLine, write);

                var line = write.Substring(0, write.Length - Environment.NewLine.Length);
                Assert.DoesNotContain("\n", line);
                Assert.DoesNotContain("\r", line);
                lines.Add(line);
            }

            return lines;
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

        /// <summary>The summary was written exactly once, after the last terminal write — the plain lines' final line.</summary>
        public void AssertSummaryFollowsLastTerminalWrite()
        {
            List<string> events;
            lock (Events)
                events = Events.ToList();

            Assert.AreEqual(1, events.Count(eventName => eventName == SummaryEvent));
            var lastTerminalWrite = events.FindLastIndex(eventName => eventName.StartsWith(TerminalEventPrefix, StringComparison.Ordinal));
            Assert.IsGreaterThanOrEqualTo(0, lastTerminalWrite);
            Assert.IsGreaterThan(lastTerminalWrite, events.IndexOf(SummaryEvent));
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
}
