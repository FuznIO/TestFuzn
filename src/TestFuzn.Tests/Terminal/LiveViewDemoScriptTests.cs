using Fuzn.TestFuzn.Internals;
using Fuzn.TestFuzn.Internals.Execution.Producers.Simulations;
using Fuzn.TestFuzn.Internals.Results.Load;
using Fuzn.TestFuzn.Internals.State;
using Fuzn.TestFuzn.Internals.Terminal;
using Fuzn.TestFuzn.StandaloneRunner;

namespace Fuzn.TestFuzn.Tests.Terminal;

/// <summary>
/// Pins the <c>run --demo</c> script hermetically: the whole scripted run plays in milliseconds
/// under a fake clock — a host whose delays advance the clock instead of waiting — into a real
/// <see cref="TestExecutionState"/> and its <see cref="ScenarioLoadCollector"/>. Pinned: the
/// phases marked in pipeline order at the scripted times, the simulations filling the plan at
/// init, the iteration totals exactly what the simulations promise, the failure schedule (only
/// under load, three distinct errors on the steps they belong to, later steps skipped), the live
/// metrics model walking the phase labels in order when sampled at 1 Hz as the console manager
/// samples and ending completed at full progress, the run's determinism, and a stop — Ctrl+C or
/// the quit key — ending the execution at the next tick with the measurement finalized and
/// cleanup still run in full.
/// </summary>
[TestClass]
public class LiveViewDemoScriptTests : Test
{
    private const int WarmupIterationCount = 30;
    private const int MeasurementIterationCount = 840;

    private static readonly TimeSpan PlannedDuration = TimeSpan.FromSeconds(17);

    private static DateTime At(double seconds)
    {
        return SyntheticLoadSnapshots.At(seconds);
    }

    [Test]
    public async Task Verify_script_plays_the_phases_in_pipeline_order_with_the_promised_counts()
    {
        await Scenario()
            .Step("The phases land on the collector and the test result at the scripted times, and the simulations fill the plan at init", async context =>
            {
                var harness = new Harness();
                await harness.RunScript();

                var result = harness.Collector.GetCurrentResult(true);
                Assert.AreEqual(At(0), result.InitStartTime);
                Assert.AreEqual(At(1), result.InitEndTime);
                Assert.AreEqual(At(1), result.WarmupStartTime);
                Assert.AreEqual(At(4), result.WarmupEndTime);
                Assert.AreEqual(At(4), result.MeasurementStartTime);
                Assert.IsTrue(result.IsCompleted);
                Assert.AreNotEqual(default, result.MeasurementEndTime);
                Assert.AreEqual(At(18), result.CleanupStartTime);
                Assert.AreEqual(At(19), result.CleanupEndTime);
                Assert.AreEqual(TestStatus.Passed, result.Status);

                var testResult = harness.State.TestResult;
                Assert.AreEqual(At(1), testResult.InitEndTime);
                Assert.AreEqual(At(4), testResult.ExecuteStartTime);
                Assert.AreEqual(At(18), testResult.ExecuteEndTime);
                Assert.AreEqual(At(18), testResult.CleanupStartTime);
                Assert.AreEqual(At(19), testResult.CleanupEndTime);

                var simulations = harness.Scenario.SimulationsInternal;
                Assert.HasCount(3, simulations);
                var warmup = Assert.IsInstanceOfType<FixedLoadConfiguration>(simulations[0]);
                Assert.IsTrue(warmup.IsWarmup);
                Assert.AreEqual(LiveViewDemoScript.WarmupRate, warmup.Rate);
                Assert.AreEqual(LiveViewDemoScript.WarmupDuration, warmup.Duration);
                var ramp = Assert.IsInstanceOfType<GradualLoadIncreaseConfiguration>(simulations[1]);
                Assert.IsFalse(ramp.IsWarmup);
                Assert.AreEqual(LiveViewDemoScript.RampStartRate, ramp.StartRate);
                Assert.AreEqual(LiveViewDemoScript.RampEndRate, ramp.EndRate);
                Assert.AreEqual(LiveViewDemoScript.RampDuration, ramp.Duration);
                var steady = Assert.IsInstanceOfType<FixedLoadConfiguration>(simulations[2]);
                Assert.IsFalse(steady.IsWarmup);
                Assert.AreEqual(LiveViewDemoScript.SteadyRate, steady.Rate);
                Assert.AreEqual(LiveViewDemoScript.SteadyDuration, steady.Duration);
                Assert.AreEqual(PlannedDuration, new SimulationPlan(simulations).PlannedDuration);
                Assert.HasCount(3, result.Simulations);

                Assert.AreEqual(ExecutionStatus.Completed, harness.State.ExecutionStatus);
                Assert.IsTrue(harness.State.IsConsumingCompleted);
                Assert.IsTrue(harness.State.IsScenarioExecutionComplete(harness.Scenario.Name));
                Assert.IsFalse(harness.State.CancellationToken.IsCancellationRequested);
            })
            .Step("The iteration totals are exactly what the simulations promise: 10 rps × 3 s of warmup, then (10 + 80) / 2 × 8 s + 80 × 6 s of measurement, one result per step per iteration", async context =>
            {
                var harness = new Harness();
                await harness.RunScript();

                var result = harness.Collector.GetCurrentResult(true);
                Assert.AreEqual(WarmupIterationCount, result.WarmupRequestCountOk);
                Assert.AreEqual(0, result.WarmupRequestCountFailed);
                Assert.AreEqual(MeasurementIterationCount, result.RequestCount);
                Assert.AreEqual(MeasurementIterationCount, result.Ok.RequestCount + result.Failed.RequestCount);

                Assert.HasCount(3, result.Steps);
                CollectionAssert.AreEqual(new[] { "Browse products", "Add to cart", "Place order" }, result.Steps.Keys.ToList());
                foreach (var step in result.Steps.Values)
                    Assert.AreEqual(MeasurementIterationCount, step.Ok.RequestCount + step.Failed.RequestCount + step.SkippedCount, step.Name);

                // Every delay the script asked for was a positive slice no longer than a phase hook.
                Assert.IsNotEmpty(harness.Host.Delays);
                foreach (var delay in harness.Host.Delays)
                    Assert.IsInRange(TimeSpan.FromTicks(1), TimeSpan.FromSeconds(1), delay);
            })
            .Step("Two runs of the script record identical numbers: the schedule and the generator, not the wall clock, decide them", async context =>
            {
                var first = new Harness();
                await first.RunScript();
                var second = new Harness();
                await second.RunScript();

                var firstResult = first.Collector.GetCurrentResult(true);
                var secondResult = second.Collector.GetCurrentResult(true);
                Assert.AreEqual(firstResult.Ok.RequestCount, secondResult.Ok.RequestCount);
                Assert.AreEqual(firstResult.Failed.RequestCount, secondResult.Failed.RequestCount);
                Assert.AreEqual(firstResult.Ok.ResponseTimePercentile95, secondResult.Ok.ResponseTimePercentile95);
                Assert.AreEqual(firstResult.Ok.ResponseTimeMax, secondResult.Ok.ResponseTimeMax);
                Assert.AreEqual(firstResult.Failed.ResponseTimeMean, secondResult.Failed.ResponseTimeMean);
                foreach (var stepName in firstResult.Steps.Keys)
                {
                    Assert.AreEqual(firstResult.Steps[stepName].Failed.RequestCount, secondResult.Steps[stepName].Failed.RequestCount, stepName);
                    Assert.AreEqual(firstResult.Steps[stepName].Ok.ResponseTimePercentile95, secondResult.Steps[stepName].Ok.ResponseTimePercentile95, stepName);
                }
            })
            .Run();
    }

    [Test]
    public async Task Verify_failures_are_occasional_under_load_with_three_distinct_errors_on_their_steps()
    {
        await Scenario()
            .Step("Only measurement iterations at or above the threshold rate fail, every 23rd: 33 of 840, well under a tenth; the steps after a failed one are skipped", async context =>
            {
                var harness = new Harness();
                await harness.RunScript();

                // The ramp reaches 40 rps 3.43 s in, when 88 iterations are due; of the multiples
                // of 23 up to 840, the three before that cannot fail: 36 - 3.
                var result = harness.Collector.GetCurrentResult(true);
                Assert.AreEqual(33, result.Failed.RequestCount);
                Assert.IsLessThan(MeasurementIterationCount / 10, result.Failed.RequestCount);
                Assert.AreEqual(MeasurementIterationCount - 33, result.Ok.RequestCount);

                var browse = result.Steps["Browse products"];
                var addToCart = result.Steps["Add to cart"];
                var placeOrder = result.Steps["Place order"];
                Assert.AreEqual(0, browse.SkippedCount);
                Assert.AreEqual(browse.Failed.RequestCount, addToCart.SkippedCount);
                Assert.AreEqual(browse.Failed.RequestCount + addToCart.Failed.RequestCount, placeOrder.SkippedCount);
                Assert.AreEqual(result.Failed.RequestCount, browse.Failed.RequestCount + addToCart.Failed.RequestCount + placeOrder.Failed.RequestCount);
            })
            .Step("Each step carries its one distinct error with the count of its failures: the 503 on Place order most often, the timeout on Add to cart, the 500 on Browse products", async context =>
            {
                var harness = new Harness();
                await harness.RunScript();

                var result = harness.Collector.GetCurrentResult(true);
                var browseError = Assert.ContainsSingle(result.Steps["Browse products"].Errors);
                Assert.AreEqual("HTTP 500 Internal Server Error", browseError.Key);
                Assert.AreEqual(result.Steps["Browse products"].Failed.RequestCount, browseError.Value.Count);
                var addToCartError = Assert.ContainsSingle(result.Steps["Add to cart"].Errors);
                Assert.AreEqual("Request timed out after 2000 ms", addToCartError.Key);
                Assert.AreEqual(result.Steps["Add to cart"].Failed.RequestCount, addToCartError.Value.Count);
                var placeOrderError = Assert.ContainsSingle(result.Steps["Place order"].Errors);
                Assert.AreEqual("HTTP 503 Service Unavailable", placeOrderError.Key);
                Assert.AreEqual(result.Steps["Place order"].Failed.RequestCount, placeOrderError.Value.Count);
                Assert.IsGreaterThan(addToCartError.Value.Count, placeOrderError.Value.Count);
                Assert.IsGreaterThan(browseError.Value.Count, placeOrderError.Value.Count);
                Assert.AreEqual(33, browseError.Value.Count + addToCartError.Value.Count + placeOrderError.Value.Count);

                // A timed-out iteration is the slowest failure: its 2 s step plus the step before it.
                Assert.IsGreaterThan(TimeSpan.FromMilliseconds(2000), result.Failed.ResponseTimeMax);
                Assert.IsLessThan(TimeSpan.FromMilliseconds(2000), result.Ok.ResponseTimeMax);
            })
            .Run();
    }

    [Test]
    public async Task Verify_live_model_walks_the_phase_labels_in_order_and_ends_completed_at_full_progress()
    {
        await Scenario()
            .Step("Sampled at 1 Hz as the console manager samples, the model shows init, the warmup, both measurement simulations and cleanup in order, and the final Record after cleanup lands completed", async context =>
            {
                var harness = new Harness();
                var sampler = new Sampler(harness.Collector, harness.Scenario);
                harness.Host.BeforeDelay = sampler.OnDelay;
                await harness.RunScript();

                // The one final force-refreshed Record the console manager makes after cleanup.
                sampler.Sample(harness.Host.UtcNow);

                CollectionAssert.AreEqual(new[]
                {
                    "init",
                    "warmup: Fixed Load 10 rps",
                    "sim 1/2: Gradual Load 10→80 rps",
                    "sim 2/2: Fixed Load 80 rps",
                    "cleanup",
                    "completed"
                }, sampler.PhaseLabels);

                var final = sampler.Snapshots[At(19)];
                Assert.IsTrue(final.IsCompleted);
                Assert.AreEqual("completed", final.PhaseLabel);
                Assert.AreEqual(1.0, final.ProgressFraction);
                Assert.AreEqual(TimeSpan.Zero, final.EstimatedTimeRemaining);
                Assert.AreEqual(PlannedDuration, final.PlannedDuration);
                Assert.AreEqual(TimeSpan.FromSeconds(19), final.Duration);
                Assert.AreEqual(TestStatus.Passed, final.Status);
                Assert.IsNull(final.StatusDetail);
                Assert.AreEqual(WarmupIterationCount, final.WarmupRequestCountOk);
                Assert.AreEqual(MeasurementIterationCount, final.RequestCountOk + final.RequestCountFailed);
                Assert.HasCount(3, final.Errors);
                CollectionAssert.AreEqual(new[] { "Browse products", "Add to cart", "Place order" }, final.Steps.Select(step => step.Name).ToList());
            })
            .Step("The per-second deltas follow the simulations: 10 through the warmup, climbing through the ramp, 80 through the steady state; progress tracks the planned time", async context =>
            {
                var harness = new Harness();
                var sampler = new Sampler(harness.Collector, harness.Scenario);
                harness.Host.BeforeDelay = sampler.OnDelay;
                await harness.RunScript();

                for (var second = 2; second <= 4; second++)
                    Assert.AreEqual(10, sampler.RequestDeltaAt(At(second)), "second " + second);
                for (var second = 5; second <= 12; second++)
                    Assert.IsGreaterThan(sampler.RequestDeltaAt(At(second - 1)), sampler.RequestDeltaAt(At(second)), "second " + second);
                for (var second = 13; second <= 18; second++)
                    Assert.AreEqual(80, sampler.RequestDeltaAt(At(second)), "second " + second);

                Assert.AreEqual(0.0, sampler.Snapshots[At(1)].ProgressFraction);
                Assert.AreEqual(3.0 / 17, sampler.Snapshots[At(4)].ProgressFraction.GetValueOrDefault(), 1e-9);
                Assert.AreEqual(11.0 / 17, sampler.Snapshots[At(12)].ProgressFraction.GetValueOrDefault(), 1e-9);
                Assert.AreEqual(16.0 / 17, sampler.Snapshots[At(17)].ProgressFraction.GetValueOrDefault(), 1e-9);
                Assert.AreEqual(1.0, sampler.Snapshots[At(18)].ProgressFraction);
                Assert.AreEqual(TimeSpan.FromSeconds(1), sampler.Snapshots[At(17)].EstimatedTimeRemaining);

                // Every measurement interval closed with successful requests, so the p95 sparkline
                // has a point for each — except the cleanup sample's: completing the measurement
                // force-refreshes the collector, which closes the interval first, as on a real
                // run. The step means keep the steps' order of unloaded times, since every
                // request's factors apply to all steps alike.
                for (var second = 5; second <= 17; second++)
                    Assert.IsGreaterThan(TimeSpan.Zero, sampler.Snapshots[At(second)].Samples[^1].ResponseTimePercentile95, "second " + second);
                Assert.AreEqual(TimeSpan.Zero, sampler.Snapshots[At(18)].Samples[^1].ResponseTimePercentile95);

                var steps = sampler.Snapshots[At(18)].Steps;
                Assert.IsGreaterThan(steps[0].ResponseTimeMean, steps[1].ResponseTimeMean);
                Assert.IsGreaterThan(steps[1].ResponseTimeMean, steps[2].ResponseTimeMean);
            })
            .Run();
    }

    [Test]
    public async Task Verify_a_stop_ends_the_execution_at_the_next_tick_and_cleanup_still_runs_in_full()
    {
        await Scenario()
            .Step("Ctrl+C mid-ramp: nothing more is recorded, the measurement is finalized as stopped, consuming is not marked complete, and cleanup takes its full second", async context =>
            {
                var harness = new Harness();
                harness.Host.BeforeDelay = now =>
                {
                    if (now == At(6))
                        harness.TestFramework.Cancel();
                };

                await harness.Script.Init();
                await harness.Script.Execute();

                harness.AssertStoppedLikeCtrlC();
                Assert.AreEqual(At(6), harness.Host.UtcNow);
                var result = harness.Collector.GetCurrentResult(true);
                // Two seconds into the ramp: 10 × 2 + 70 × 2² / 16 = 37.5 iterations due.
                Assert.AreEqual(37, result.RequestCount);
                Assert.AreEqual(WarmupIterationCount, result.WarmupRequestCountOk);
                Assert.AreEqual(At(4), result.MeasurementStartTime);
                Assert.IsTrue(result.IsCompleted);
                Assert.AreNotEqual(default, result.MeasurementEndTime);
                Assert.AreEqual(At(6), harness.State.TestResult.ExecuteEndTime);
                Assert.IsFalse(harness.State.IsConsumingCompleted);

                await harness.Script.Cleanup();

                result = harness.Collector.GetCurrentResult(true);
                Assert.AreEqual(37, result.RequestCount);
                Assert.AreEqual(At(6), result.CleanupStartTime);
                Assert.AreEqual(At(7), result.CleanupEndTime);
                Assert.AreEqual(At(7), harness.Host.UtcNow);

                var metrics = new ScenarioLiveMetrics(harness.Scenario.Name, harness.Scenario.SimulationsInternal.ToArray());
                metrics.Record(result, At(7));
                Assert.AreEqual("completed", metrics.Current.PhaseLabel);
                Assert.IsTrue(metrics.Current.IsCompleted);
                Assert.AreEqual(TimeSpan.FromSeconds(7), metrics.Current.Duration);
            })
            .Step("The quit key's stop request lands the same way", async context =>
            {
                var harness = new Harness();
                Task? stopRequest = null;
                harness.Host.BeforeDelay = now =>
                {
                    if (now == At(6) && stopRequest == null)
                        stopRequest = harness.State.RequestStop();
                };

                await harness.Script.Init();
                await harness.Script.Execute();
                Assert.IsNotNull(stopRequest);
                await stopRequest;

                harness.AssertStoppedLikeCtrlC();
                Assert.AreEqual(At(6), harness.Host.UtcNow);
                Assert.AreEqual(37, harness.Collector.GetCurrentResult(true).RequestCount);
                Assert.IsTrue(harness.Collector.GetCurrentResult(true).IsCompleted);
                // The request's callback sets the status Stopped off the script's thread; the
                // finalization must not mark consuming completed in the window before it runs.
                Assert.IsFalse(harness.State.IsConsumingCompleted);

                await harness.Script.Cleanup();
                Assert.AreEqual(At(7), harness.Collector.GetCurrentResult(true).CleanupEndTime);
            })
            .Step("A stop during init ends init with the cancellation, as a hook observing the token would; no simulations are added, and cleanup still marks its phase", async context =>
            {
                var harness = new Harness();
                harness.Host.BeforeDelay = now =>
                {
                    if (now == At(0))
                        harness.TestFramework.Cancel();
                };

                await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () => await harness.Script.Init());

                harness.AssertStoppedLikeCtrlC();
                var result = harness.Collector.GetCurrentResult(true);
                Assert.AreEqual(At(0), result.InitStartTime);
                Assert.AreEqual(default, result.InitEndTime);
                Assert.IsEmpty(harness.Scenario.SimulationsInternal);
                Assert.AreEqual(At(0), harness.Host.UtcNow);

                await harness.Script.Cleanup();
                result = harness.Collector.GetCurrentResult(true);
                Assert.AreEqual(At(0), result.CleanupStartTime);
                Assert.AreEqual(At(1), result.CleanupEndTime);
                Assert.AreEqual(0, result.RequestCount);
            })
            .Run();
    }

    /// <summary>
    /// The demo script over a real execution state — the demo scenario, a fake standalone-style
    /// adapter whose token Ctrl+C would cancel, and the fake clock host — plus the state's
    /// collector for the scenario.
    /// </summary>
    private sealed class Harness
    {
        public FakeClockHost Host { get; } = new FakeClockHost();

        public FakeTestFrameworkAdapter TestFramework { get; } = new FakeTestFrameworkAdapter(new List<string>());

        public Scenario Scenario { get; }

        public TestExecutionState State { get; }

        public ScenarioLoadCollector Collector { get; }

        public LiveViewDemoScript Script { get; }

        public Harness()
        {
            Scenario = LiveViewDemoScript.CreateScenario();
            State = new TestExecutionState(new TestSession("live-view-demo-script-tests"));
            State.Init(TestFramework, new FakeTest(), Scenario);
            Collector = State.LoadCollectors[Scenario.Name];
            Script = new LiveViewDemoScript(State, Host);
        }

        /// <summary>The whole script, phase after phase, as the demo runs it.</summary>
        public async Task RunScript()
        {
            await Script.Init();
            await Script.Execute();
            await Script.Cleanup();
        }

        /// <summary>The state as Ctrl+C leaves it — the same shape the console manager tests pin for the quit key.</summary>
        public void AssertStoppedLikeCtrlC()
        {
            Assert.AreEqual(ExecutionStatus.Stopped, State.ExecutionStatus);
            Assert.IsTrue(State.CancellationToken.IsCancellationRequested);
            Assert.IsNull(State.ExecutionStoppedReason);
            Assert.IsNull(State.FirstException);
        }
    }

    /// <summary>
    /// Samples the collector the way the console manager does: once per second on the fake
    /// clock, force-refreshed, the model created at the first sample after init has completed
    /// and the init placeholder's label before that — and keeps every snapshot by its time.
    /// </summary>
    private sealed class Sampler
    {
        private static readonly TimeSpan SampleInterval = TimeSpan.FromSeconds(1);

        private readonly ScenarioLoadCollector _collector;
        private readonly Scenario _scenario;
        private ScenarioLiveMetrics? _metrics;
        private DateTime? _nextSampleTime;

        public Sampler(ScenarioLoadCollector collector, Scenario scenario)
        {
            _collector = collector;
            _scenario = scenario;
        }

        /// <summary>Every snapshot taken, by the time it was taken at.</summary>
        public Dictionary<DateTime, LiveMetricsSnapshot> Snapshots { get; } = new Dictionary<DateTime, LiveMetricsSnapshot>();

        /// <summary>The phase labels seen, each once, in the order they first appeared.</summary>
        public List<string> PhaseLabels { get; } = new List<string>();

        public void OnDelay(DateTime now)
        {
            if (_nextSampleTime == null)
                _nextSampleTime = now;
            if (now < _nextSampleTime.Value)
                return;

            Sample(now);
            _nextSampleTime = _nextSampleTime.Value + SampleInterval;
        }

        public void Sample(DateTime now)
        {
            var snapshot = _collector.GetCurrentResult(true);
            if (_metrics == null)
            {
                if (snapshot.InitEndTime == default)
                {
                    AddPhaseLabel("init");
                    return;
                }

                _metrics = new ScenarioLiveMetrics(_scenario.Name, _scenario.SimulationsInternal.ToArray());
            }

            _metrics.Record(snapshot, now);
            Snapshots[now] = _metrics.Current;
            AddPhaseLabel(_metrics.Current.PhaseLabel);
        }

        /// <summary>The requests (ok and failed) recorded in the interval that closed at the given sample time.</summary>
        public int RequestDeltaAt(DateTime sampleTime)
        {
            var sample = Snapshots[sampleTime].Samples[^1];
            return sample.OkDelta + sample.FailedDelta;
        }

        private void AddPhaseLabel(string phaseLabel)
        {
            if (PhaseLabels.Count == 0 || PhaseLabels[PhaseLabels.Count - 1] != phaseLabel)
                PhaseLabels.Add(phaseLabel);
        }
    }

    /// <summary>
    /// An <see cref="ILiveViewHost"/> with a fake clock: a delay advances the clock by its interval
    /// and returns at once — or returns without advancing when the token is already cancelled,
    /// as the production delay returns at once then — after reporting the time it starts at to
    /// the test's hook, which is where a test samples or stops the run. The script never touches
    /// the terminal, so the host has none.
    /// </summary>
    private sealed class FakeClockHost : ILiveViewHost
    {
        public DateTime UtcNow { get; set; } = SyntheticLoadSnapshots.BaseTime;

        /// <summary>Called with the clock's time at the start of every delay, before it advances.</summary>
        public Action<DateTime>? BeforeDelay { get; set; }

        /// <summary>Every interval delayed by, in order.</summary>
        public List<TimeSpan> Delays { get; } = new List<TimeSpan>();

        public TerminalCapabilities DetectCapabilities()
        {
            throw new InvalidOperationException("The demo script never detects capabilities.");
        }

        public ITerminalWriter CreateTerminalWriter()
        {
            throw new InvalidOperationException("The demo script never writes to the terminal.");
        }

        public ITerminalReader CreateTerminalReader()
        {
            throw new InvalidOperationException("The demo script never reads keys.");
        }

        public Task Delay(TimeSpan interval, CancellationToken cancellationToken)
        {
            if (BeforeDelay != null)
                BeforeDelay(UtcNow);

            if (cancellationToken.IsCancellationRequested)
                return Task.CompletedTask;

            Delays.Add(interval);
            UtcNow = UtcNow + interval;
            return Task.CompletedTask;
        }
    }
}
