using Fuzn.TestFuzn.Contracts;
using Fuzn.TestFuzn.Contracts.Results.Standard;
using Fuzn.TestFuzn.Internals;
using Fuzn.TestFuzn.Internals.Execution;
using Fuzn.TestFuzn.Internals.Execution.Producers.Simulations;
using Fuzn.TestFuzn.Internals.Results.Load;
using Fuzn.TestFuzn.Internals.State;
using Fuzn.TestFuzn.Internals.Terminal;

namespace Fuzn.TestFuzn.StandaloneRunner;

/// <summary>
/// The scripted load run behind <c>run --demo</c>: plays a synthetic load test — an init phase,
/// a warmup, a ramp-up, a steady state with occasional failures, a cleanup phase; about twenty
/// seconds in all — into a real <see cref="TestExecutionState"/>, so that the console manager's
/// live view runs through exactly the code a real run drives it through, with no Startup,
/// plugins, sinks, configuration or target system. The script marks the phases on the scenario's
/// <see cref="ScenarioLoadCollector"/> and on the state's <see cref="TestResult"/> where the
/// pipeline's managers mark them — init as the init manager, warmup and measurement as the
/// producer and consumer managers, cleanup as the cleanup manager — and records warmup and
/// measurement iterations into the collector as the scenario message handler records them,
/// each with its per-step results, so the dashboard, the plain stats lines, the quit key and
/// Ctrl+C, the final Record and the summary all come out as they do on a real run.
/// The scenario's typed simulations are the script: a warmup <see cref="FixedLoadConfiguration"/>,
/// a <see cref="GradualLoadIncreaseConfiguration"/> ramp and a steady <see cref="FixedLoadConfiguration"/>,
/// added at init the way <c>SetupSimulations</c> adds a real scenario's, each then played for
/// its configured duration at the rate it promises — the iterations due by any point in a
/// simulation are computed from the elapsed time, so a late tick catches up and every
/// simulation ends with exactly the count its rate and duration promise — which keeps the plan,
/// progress, ETA and phase labels the dashboard derives from the same configs honest. One
/// caveat: the script implements the documented per-second semantics of a gradual load, but the
/// real <c>GradualLoadIncreaseHandler</c> enqueues <c>currentRate</c> iterations per step and
/// sleeps <c>duration / (endRate − startRate)</c> between steps, so today it delivers about
/// <c>(endRate − startRate) / duration.TotalSeconds</c> times the labeled rate (~9× for this
/// ramp; a pre-existing framework bug on the run's report list) — rps-scale calibration against
/// the demo's ramp segment does not transfer to a real run until the handler is fixed. The
/// counts are exact on every run — 30 warmup and 840 measurement iterations, of which 33 fail
/// on the fixed failure schedule — and the response times come from the script's own generator
/// (the current rate, a slow wobble, per-request jitter), so the timings and latencies are exact
/// too under a fake clock; under the real host they follow the wall clock's tick times, and a
/// real run's p95 and per-second counts vary slightly (about a request per second) from one run
/// to the next. The clock and the delays come from the <see cref="ILiveViewHost"/> the console
/// manager samples on, so the script and the live view share one clock and a test drives the
/// whole script in milliseconds under a fake one. A stop — Ctrl+C or the quit key, both landing on the
/// state's cancellation token — ends the execution phase at the next tick with the measurement
/// finalized as the consumer manager finalizes a stopped run; cleanup then runs in full, as a
/// real run's cleanup hooks do.
/// </summary>
internal sealed class LiveViewDemoScript
{
    /// <summary>The demo scenario's name, as the dashboard titles its section.</summary>
    public const string ScenarioName = "Checkout flow (demo)";

    /// <summary>What the init phase takes — the before-test and before-scenario hooks of a real run.</summary>
    public static readonly TimeSpan InitDuration = TimeSpan.FromSeconds(1);

    /// <summary>What the cleanup phase takes — the after-scenario and after-test hooks of a real run.</summary>
    public static readonly TimeSpan CleanupDuration = TimeSpan.FromSeconds(1);

    /// <summary>The interval every fixed rate is per: one second, so the rates read as requests per second.</summary>
    public static readonly TimeSpan RateInterval = TimeSpan.FromSeconds(1);

    /// <summary>The warmup simulation: a fixed load at this rate per second for <see cref="WarmupDuration"/>.</summary>
    public const int WarmupRate = 10;

    public static readonly TimeSpan WarmupDuration = TimeSpan.FromSeconds(3);

    /// <summary>The ramp-up simulation: a gradual load increase from this rate to <see cref="RampEndRate"/> over <see cref="RampDuration"/>.</summary>
    public const int RampStartRate = 10;

    public const int RampEndRate = 80;

    public static readonly TimeSpan RampDuration = TimeSpan.FromSeconds(8);

    /// <summary>The steady-state simulation: a fixed load at this rate per second for <see cref="SteadyDuration"/>.</summary>
    public const int SteadyRate = 80;

    public static readonly TimeSpan SteadyDuration = TimeSpan.FromSeconds(6);

    /// <summary>
    /// The cadence the script records at: every tick records the iterations that have come due
    /// since the previous one, so the collector fills smoothly under the console manager's 1 Hz
    /// sampling instead of in once-a-second bursts.
    /// </summary>
    public static readonly TimeSpan TickInterval = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// The failure schedule: once the current rate has reached <see cref="FailureRateThreshold"/>
    /// requests per second, every <see cref="FailureEveryIterations"/>-th measurement iteration
    /// fails at one of its steps — the failures cycle through <see cref="Failures"/>, so all three
    /// distinct errors show up, the 503 most often. Warmup iterations never fail.
    /// </summary>
    public const int FailureEveryIterations = 23;

    public const int FailureRateThreshold = 40;

    /// <summary>The steps every iteration runs, in order, with their unloaded response times.</summary>
    public static readonly IReadOnlyList<DemoStep> Steps = new[]
    {
        new DemoStep("Browse products", "browse-products", TimeSpan.FromMilliseconds(35)),
        new DemoStep("Add to cart", "add-to-cart", TimeSpan.FromMilliseconds(55)),
        new DemoStep("Place order", "place-order", TimeSpan.FromMilliseconds(120))
    };

    /// <summary>The failures the schedule cycles through: the step that fails, its error and how long the failed step took.</summary>
    public static readonly IReadOnlyList<DemoFailure> Failures = new[]
    {
        new DemoFailure(2, "HTTP 503 Service Unavailable", TimeSpan.FromMilliseconds(12), message => new InvalidOperationException(message)),
        new DemoFailure(2, "HTTP 503 Service Unavailable", TimeSpan.FromMilliseconds(12), message => new InvalidOperationException(message)),
        new DemoFailure(2, "HTTP 503 Service Unavailable", TimeSpan.FromMilliseconds(12), message => new InvalidOperationException(message)),
        new DemoFailure(1, "Request timed out after 2000 ms", TimeSpan.FromMilliseconds(2000), message => new TimeoutException(message)),
        new DemoFailure(0, "HTTP 500 Internal Server Error", TimeSpan.FromMilliseconds(80), message => new InvalidOperationException(message))
    };

    // How response times grow with load: at SteadyRate a step takes (1 + LoadLatencyFactor) times
    // its unloaded time, so the p95 sparkline climbs through the ramp.
    private const double LoadLatencyFactor = 0.8;

    // A slow triangle-wave wobble on top of the load factor, so the steady state's latency moves.
    private const double WobbleAmplitude = 0.15;
    private static readonly TimeSpan WobblePeriod = TimeSpan.FromSeconds(5);

    // Per-request jitter: a uniform factor in [1 - JitterAmplitude, 1 + JitterAmplitude], and a
    // spike by SpikeFactor on a SpikeProbability share of requests, which is what lifts p95 and
    // p99 clear of the mean.
    private const double JitterAmplitude = 0.3;
    private const double SpikeProbability = 0.04;
    private const double SpikeFactor = 2.5;

    // Absorbs floating-point noise below an integer when flooring an expected count, so a count
    // that is mathematically whole is never one short.
    private const double CountEpsilon = 1e-9;

    private readonly TestExecutionState _testExecutionState;
    private readonly ILiveViewHost _liveViewHost;
    private readonly Scenario _scenario;
    private readonly ScenarioLoadCollector _collector;
    private DateTime _scriptStartTime;
    private long _measurementIterationCount;
    private long _failureCount;
    private ulong _generatorState = 0x9E3779B97F4A7C15UL;

    /// <param name="testExecutionState">An initialized state whose single scenario is the one <see cref="CreateScenario"/> built.</param>
    /// <param name="liveViewHost">The clock and the delays — the host the console manager samples on, so the script shares its clock.</param>
    public LiveViewDemoScript(TestExecutionState testExecutionState, ILiveViewHost liveViewHost)
    {
        if (testExecutionState == null)
            throw new ArgumentNullException(nameof(testExecutionState), "Test execution state cannot be null.");
        if (liveViewHost == null)
            throw new ArgumentNullException(nameof(liveViewHost), "Live view host cannot be null.");
        if (testExecutionState.Scenarios.Count != 1)
            throw new ArgumentException("The demo script plays exactly one scenario; the state has " + testExecutionState.Scenarios.Count + ".", nameof(testExecutionState));

        _testExecutionState = testExecutionState;
        _liveViewHost = liveViewHost;
        _scenario = testExecutionState.Scenarios[0];
        _collector = testExecutionState.LoadCollectors[_scenario.Name];
    }

    /// <summary>
    /// The demo scenario: named, with the three steps and a simulations action — what makes a
    /// scenario a load test. The simulations themselves are added when init completes, where
    /// <c>SetupSimulations</c> adds a real scenario's, so the live view shows the init placeholder
    /// until then, as on a real run.
    /// </summary>
    public static Scenario CreateScenario()
    {
        var scenario = new Scenario(ScenarioName);
        scenario.Id = "checkout-flow-demo";
        scenario.Description = "A scripted synthetic load run for the standalone runner's live view; no target system is called.";
        foreach (var step in Steps)
            scenario.Steps.Add(new Step { Name = step.Name, Id = step.Id });

        scenario.SimulationsAction = (scenarioContext, simulations) => Task.CompletedTask;
        return scenario;
    }

    /// <summary>
    /// The init phase, as the init manager runs it: marks init started, spends
    /// <see cref="InitDuration"/> where the hooks would run — a stop during it ends init with an
    /// <see cref="OperationCanceledException"/>, as a hook observing the run's token would —
    /// then adds the simulations, caches their descriptions and marks init complete on the
    /// collector and the test result.
    /// </summary>
    public async Task Init()
    {
        _scriptStartTime = _liveViewHost.UtcNow;
        _collector.MarkPhaseAsStarted(LoadTestPhase.Init, _scriptStartTime);

        await _liveViewHost.Delay(InitDuration, _testExecutionState.CancellationToken);
        _testExecutionState.CancellationToken.ThrowIfCancellationRequested();

        // The rates are per second: the two-argument FixedLoad makes the interval the duration.
        new SimulationsBuilder(_scenario, isWarmup: true)
            .FixedLoad(WarmupRate, RateInterval, WarmupDuration);
        new SimulationsBuilder(_scenario, isWarmup: false)
            .GradualLoadIncrease(RampStartRate, RampEndRate, RampDuration)
            .FixedLoad(SteadyRate, RateInterval, SteadyDuration);
        _collector.CacheSimulationDescriptions(_scenario);

        var timestamp = _liveViewHost.UtcNow;
        _collector.MarkPhaseAsCompleted(LoadTestPhase.Init, timestamp);
        _testExecutionState.TestResult.MarkPhaseAsCompleted(StandardTestPhase.Init, timestamp);
    }

    /// <summary>
    /// The execution phase, as the producer, consumer and execution managers run it: the
    /// simulations in order, warmup marked started at the first warmup simulation and complete
    /// before the first measurement one, measurement (and the test result's execute phase)
    /// marked started there, each simulation played at its rate for its duration; then, whether
    /// the simulations ran out or a stop ended them, the measurement marked complete, the
    /// producers and consuming marked complete and the status set to Completed on an unstopped
    /// run, and the execute phase marked complete. Returns normally on a stop — the producer's
    /// cancellation is absorbed inside the execution manager on a real run, too.
    /// </summary>
    public async Task Execute()
    {
        var hasWarmupPhase = false;
        var isWarmupCompleted = false;
        var isMeasurementStarted = false;

        foreach (var simulation in _scenario.SimulationsInternal)
        {
            if (IsStopRequested())
                break;

            if (simulation.IsWarmup && !hasWarmupPhase)
            {
                hasWarmupPhase = true;
                _collector.MarkPhaseAsStarted(LoadTestPhase.Warmup, _liveViewHost.UtcNow);
            }

            if (hasWarmupPhase && !simulation.IsWarmup && !isWarmupCompleted)
            {
                isWarmupCompleted = true;
                _collector.MarkPhaseAsCompleted(LoadTestPhase.Warmup, _liveViewHost.UtcNow);
            }

            if (!simulation.IsWarmup && !isMeasurementStarted)
            {
                isMeasurementStarted = true;
                var timestamp = _liveViewHost.UtcNow;
                _testExecutionState.TestResult.MarkPhaseAsStarted(StandardTestPhase.Execute, timestamp);
                _collector.MarkPhaseAsStarted(LoadTestPhase.Measurement, timestamp);
            }

            await PlaySimulation(simulation);
        }

        _testExecutionState.MarkScenarioProducersCompleted(_scenario.Name);

        // The consumer manager's finalization: the scenario's measurement completes when its
        // last iteration has been consumed, or is finalized as it stands when the run was
        // stopped; consuming is marked complete, and the status Completed, only on an unstopped
        // run. Stopped is judged on the token as well as the status: the quit key's stop request
        // cancels the token at once but sets the status from a callback off this thread, and the
        // script gets here right after the token turned.
        var isStopped = IsStopRequested();
        _collector.MarkPhaseAsCompleted(LoadTestPhase.Measurement, _liveViewHost.UtcNow);
        if (!isStopped)
            _testExecutionState.MarkConsumingCompleted();
        if (!isStopped && _testExecutionState.ExecutionStatus == ExecutionStatus.Running)
            _testExecutionState.ExecutionStatus = ExecutionStatus.Completed;

        _testExecutionState.TestResult.MarkPhaseAsCompleted(StandardTestPhase.Execute, _liveViewHost.UtcNow);
    }

    /// <summary>
    /// The cleanup phase, as the cleanup manager runs it: cleanup marked started on the test
    /// result and the collector, <see cref="CleanupDuration"/> spent where the hooks would run —
    /// not cut short by a stop, since a real run's cleanup hooks run in full after one — then
    /// cleanup marked complete on both, which is what lands the live view's "completed".
    /// </summary>
    public async Task Cleanup()
    {
        var startedTimestamp = _liveViewHost.UtcNow;
        _testExecutionState.TestResult.MarkPhaseAsStarted(StandardTestPhase.Cleanup, startedTimestamp);
        _collector.MarkPhaseAsStarted(LoadTestPhase.Cleanup, startedTimestamp);

        await _liveViewHost.Delay(CleanupDuration, CancellationToken.None);

        var completedTimestamp = _liveViewHost.UtcNow;
        _collector.MarkPhaseAsCompleted(LoadTestPhase.Cleanup, completedTimestamp);
        _testExecutionState.TestResult.MarkPhaseAsCompleted(StandardTestPhase.Cleanup, completedTimestamp);
    }

    /// <summary>
    /// Plays one simulation: on every tick, records the iterations due by the elapsed time —
    /// the count the simulation's rate promises for that much of its duration, so the total at
    /// the end is exact whatever the ticks' lateness — at the rate current then, and ends when
    /// the duration has elapsed or a stop has been requested. Ticks are scheduled on an absolute
    /// cadence so a slow tick does not drift the ones after it.
    /// </summary>
    private async Task PlaySimulation(ILoadConfiguration simulation)
    {
        var duration = DurationOf(simulation);
        var startTime = _liveViewHost.UtcNow;
        var nextTickTime = startTime;
        var recordedCount = 0L;

        while (!IsStopRequested())
        {
            var now = _liveViewHost.UtcNow;
            var elapsed = now - startTime;
            if (elapsed > duration)
                elapsed = duration;

            var rate = RateAt(simulation, elapsed);
            var dueCount = (long)Math.Floor(ExpectedIterationCount(simulation, elapsed) + CountEpsilon);
            for (; recordedCount < dueCount; recordedCount++)
                RecordIteration(simulation.IsWarmup, rate, now);

            if (elapsed >= duration)
                return;

            nextTickTime += TickInterval;
            var delay = nextTickTime - now;
            if (delay > TimeSpan.Zero)
                await _liveViewHost.Delay(delay, _testExecutionState.CancellationToken);
        }
    }

    /// <summary>
    /// A stop as the producer sees one: the run's token cancelled (Ctrl+C, the quit key) or the
    /// status set to Stopped.
    /// </summary>
    private bool IsStopRequested()
    {
        return _testExecutionState.CancellationToken.IsCancellationRequested
            || _testExecutionState.ExecutionStatus == ExecutionStatus.Stopped;
    }

    private static TimeSpan DurationOf(ILoadConfiguration simulation)
    {
        switch (simulation)
        {
            case FixedLoadConfiguration fixedLoad:
                return fixedLoad.Duration;
            case GradualLoadIncreaseConfiguration gradual:
                return gradual.Duration;
            default:
                throw new NotSupportedException("The demo script cannot play a " + simulation.GetType().Name + ".");
        }
    }

    /// <summary>The requests per second the simulation promises at the given point into it.</summary>
    private static double RateAt(ILoadConfiguration simulation, TimeSpan elapsed)
    {
        switch (simulation)
        {
            case FixedLoadConfiguration fixedLoad:
                return fixedLoad.Rate / fixedLoad.Interval.TotalSeconds;
            case GradualLoadIncreaseConfiguration gradual:
            {
                if (gradual.Duration <= TimeSpan.Zero)
                    return gradual.EndRate;

                return gradual.StartRate + (gradual.EndRate - gradual.StartRate) * (elapsed.TotalSeconds / gradual.Duration.TotalSeconds);
            }
            default:
                throw new NotSupportedException("The demo script cannot play a " + simulation.GetType().Name + ".");
        }
    }

    /// <summary>
    /// The iterations the simulation promises by the given point into it: the rate integrated
    /// over the elapsed time — rate × t for a fixed load, start × t + (end − start) × t² / (2 × duration)
    /// for a gradual increase.
    /// </summary>
    private static double ExpectedIterationCount(ILoadConfiguration simulation, TimeSpan elapsed)
    {
        var seconds = elapsed.TotalSeconds;
        switch (simulation)
        {
            case FixedLoadConfiguration fixedLoad:
                return fixedLoad.Rate * seconds / fixedLoad.Interval.TotalSeconds;
            case GradualLoadIncreaseConfiguration gradual:
            {
                var durationSeconds = gradual.Duration.TotalSeconds;
                if (durationSeconds <= 0)
                    return gradual.EndRate * seconds;

                return gradual.StartRate * seconds + (gradual.EndRate - gradual.StartRate) * seconds * seconds / (2 * durationSeconds);
            }
            default:
                throw new NotSupportedException("The demo script cannot play a " + simulation.GetType().Name + ".");
        }
    }

    /// <summary>
    /// Records one iteration into the collector as the scenario message handler does: a warmup
    /// iteration as its status only; a measurement iteration with its per-step results — every
    /// step passed with a response time for the current load, or, on a scheduled failure, the
    /// failing step failed with its error and duration and the steps after it skipped, as the
    /// step handler leaves them — and the iteration's execute time as the sum of its steps.
    /// </summary>
    private void RecordIteration(bool isWarmup, double rate, DateTime now)
    {
        DemoFailure? failure = null;
        if (!isWarmup)
            failure = NextScheduledFailure(rate);

        var iterationResult = new IterationResult();
        var executeDuration = TimeSpan.Zero;
        for (var index = 0; index < Steps.Count; index++)
        {
            var step = Steps[index];
            var stepResult = new StepStandardResult();
            stepResult.Name = step.Name;
            stepResult.Id = step.Id;

            if (failure != null && index > failure.StepIndex)
            {
                stepResult.Status = StepStatus.Skipped;
                stepResult.Duration = TimeSpan.Zero;
            }
            else if (failure != null && index == failure.StepIndex)
            {
                stepResult.Status = StepStatus.Failed;
                stepResult.Exception = failure.CreateException();
                stepResult.Duration = failure.Duration;
            }
            else
            {
                stepResult.Status = StepStatus.Passed;
                stepResult.Duration = ResponseTime(step, rate, now);
            }

            executeDuration += stepResult.Duration;
            iterationResult.StepResults.Add(stepResult.Name, stepResult);
        }

        iterationResult.ExecuteEndTime = now;
        iterationResult.ExecuteStartTime = now - executeDuration;

        var status = TestStatus.Passed;
        if (failure != null)
            status = TestStatus.Failed;

        if (isWarmup)
            _collector.RecordWarmup(status);
        else
            _collector.RecordMeasurement(status, iterationResult);
    }

    /// <summary>The failure the schedule assigns to the next measurement iteration at the given rate, or null when it passes.</summary>
    private DemoFailure? NextScheduledFailure(double rate)
    {
        _measurementIterationCount++;
        if (rate < FailureRateThreshold || _measurementIterationCount % FailureEveryIterations != 0)
            return null;

        var failure = Failures[(int)(_failureCount % Failures.Count)];
        _failureCount++;
        return failure;
    }

    /// <summary>
    /// A passed step's response time: its unloaded time, grown by the load factor for the
    /// current rate, moved by the slow wobble, and jittered per request with the occasional
    /// spike.
    /// </summary>
    private TimeSpan ResponseTime(DemoStep step, double rate, DateTime now)
    {
        var loadFactor = 1.0 + LoadLatencyFactor * (rate / SteadyRate);
        var wobbleFactor = 1.0 + WobbleAmplitude * TriangleWave(now - _scriptStartTime);
        var jitterFactor = 1.0 - JitterAmplitude + 2 * JitterAmplitude * NextUnitInterval();
        var spikeFactor = 1.0;
        if (NextUnitInterval() < SpikeProbability)
            spikeFactor = SpikeFactor;

        var milliseconds = step.ResponseTime.TotalMilliseconds * loadFactor * wobbleFactor * jitterFactor * spikeFactor;
        return TimeSpan.FromMilliseconds(milliseconds);
    }

    /// <summary>A triangle wave in [-1, 1] with <see cref="WobblePeriod"/>, from the elapsed script time.</summary>
    private static double TriangleWave(TimeSpan elapsed)
    {
        var phase = elapsed.TotalSeconds / WobblePeriod.TotalSeconds;
        phase -= Math.Floor(phase);
        return 4 * Math.Abs(phase - 0.5) - 1;
    }

    /// <summary>
    /// The next value of the script's own uniform generator in [0, 1): a 64-bit linear
    /// congruential generator with a fixed seed, so the numbers are the same on every run and
    /// every runtime — unlike <see cref="Random"/>, whose seeded sequence may change between
    /// .NET versions.
    /// </summary>
    private double NextUnitInterval()
    {
        _generatorState = _generatorState * 6364136223846793005UL + 1442695040888963407UL;
        return (_generatorState >> 11) * (1.0 / (1UL << 53));
    }

    /// <summary>A step of the demo scenario: its name and id, and its response time when unloaded.</summary>
    public sealed class DemoStep
    {
        public string Name { get; }

        public string Id { get; }

        public TimeSpan ResponseTime { get; }

        public DemoStep(string name, string id, TimeSpan responseTime)
        {
            Name = name;
            Id = id;
            ResponseTime = responseTime;
        }
    }

    /// <summary>A scheduled failure: which step (by index into <see cref="Steps"/>) fails, with what error message, how long the failed step took, and which exception carries the message.</summary>
    public sealed class DemoFailure
    {
        private readonly Func<string, Exception> _exceptionFactory;

        public int StepIndex { get; }

        public string Message { get; }

        public TimeSpan Duration { get; }

        public DemoFailure(int stepIndex, string message, TimeSpan duration, Func<string, Exception> exceptionFactory)
        {
            if (exceptionFactory == null)
                throw new ArgumentNullException(nameof(exceptionFactory), "Exception factory cannot be null.");

            StepIndex = stepIndex;
            Message = message;
            Duration = duration;
            _exceptionFactory = exceptionFactory;
        }

        /// <summary>A fresh exception carrying the message, as a step's failure is stored by its exception (the collector keys errors by message).</summary>
        public Exception CreateException()
        {
            return _exceptionFactory(Message);
        }
    }
}
