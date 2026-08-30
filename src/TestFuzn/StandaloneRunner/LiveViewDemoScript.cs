using Fuzn.TestFuzn.Contracts;
using Fuzn.TestFuzn.Contracts.Results.Standard;
using Fuzn.TestFuzn.Internals;
using Fuzn.TestFuzn.Internals.Execution;
using Fuzn.TestFuzn.Internals.Execution.Producers.Simulations;
using Fuzn.TestFuzn.Internals.Results.Load;
using Fuzn.TestFuzn.Internals.State;
using Fuzn.TestFuzn.Internals.Terminal;
using Fuzn.TestFuzn.Internals.Thresholds;

namespace Fuzn.TestFuzn.StandaloneRunner;

/// <summary>
/// The scripted load run behind <c>run --demo</c>: plays a synthetic load test — an init phase,
/// a warmup, a ramp-up, a steady state with a scripted incident in it, a cleanup phase — into a
/// real <see cref="TestExecutionState"/>, so that the console manager's live view runs through
/// exactly the code a real run drives it through, with no Startup, plugins, sinks, configuration
/// or target system. The run takes the <see cref="Duration"/> it is built with (<c>--demo-duration</c>,
/// <see cref="DefaultDuration"/> when none is given): init and cleanup take their fixed second
/// each and what is left is split between the three simulations — <see cref="WarmupPercent"/>
/// warmup, <see cref="RampPercent"/> ramp, the rest steady — so the same shape plays out at any
/// duration from <see cref="MinimumDuration"/> up.
///
/// The script marks the phases on the scenario's <see cref="ScenarioLoadCollector"/> and on the
/// state's <see cref="TestResult"/> where the pipeline's managers mark them — init as the init
/// manager, warmup and measurement as the producer and consumer managers, cleanup as the cleanup
/// manager — and records warmup and measurement iterations into the collector as the scenario
/// message handler records them, each with its per-step results, so the dashboard, the plain
/// stats lines, the quit key and Ctrl+C, the final Record and the summary all come out as they
/// do on a real run. The scenario's typed simulations are the script: a warmup
/// <see cref="FixedLoadConfiguration"/>, a <see cref="GradualLoadIncreaseConfiguration"/> ramp
/// and a steady <see cref="FixedLoadConfiguration"/>, added at init the way <c>SetupSimulations</c>
/// adds a real scenario's, each then played for its configured duration at the rate it promises —
/// the iterations due by any point in a simulation are computed from the elapsed time, so a late
/// tick catches up and every simulation ends with exactly the count its rate and duration promise —
/// which keeps the plan, progress, ETA and phase labels the dashboard derives from the same
/// configs honest. One caveat: the script implements the documented per-second semantics of a
/// gradual load, but the real <c>GradualLoadIncreaseHandler</c> enqueues <c>currentRate</c>
/// iterations per step and sleeps <c>duration / (endRate − startRate)</c> between steps, so today
/// it delivers about <c>(endRate − startRate) / duration.TotalSeconds</c> times the labeled rate
/// (a pre-existing framework bug on the run's report list) — rps-scale calibration against the
/// demo's ramp segment does not transfer to a real run until the handler is fixed.
///
/// The steady state carries a scripted incident, so that the charts, the heatmap, the error
/// ticker and the threshold tiles all show a shape rather than a flat line: at
/// <see cref="IncidentStartShare"/> of the steady phase the "Place order" step starts taking
/// <see cref="IncidentLatencyFactor"/> times as long for one iteration in
/// <see cref="LatencyIncidentEveryIterations"/> — a partly degraded dependency — for
/// <see cref="LatencyIncidentShare"/> of the steady phase, and one iteration in
/// <see cref="ErrorBurstEveryIterations"/> fails it with an HTTP 503 for the
/// <see cref="ErrorBurstShare"/> of it the burst lasts; then both recover. The scenario declares
/// the two thresholds the incident is measured against (<see cref="ResponseTimePercentile95Limit"/>
/// and <see cref="ErrorRateLimit"/>), and the degraded share is what makes the demo's point:
/// while the incident lasts, the breached share of each one-second interval is far past the
/// limit and the live gauges turn red, yet over the whole measurement the same statistics stay
/// inside it, so the completion verdict — evaluated here as the execution manager evaluates a
/// real run's — passes and <c>run --demo</c> still exits 0.
///
/// The counts are exact on every run — every simulation's is its rate times its duration, and
/// the failures and degraded iterations are the fixed fractions of the windows they fall in —
/// and the response times come from the script's own generator (the current rate, a slow wobble,
/// per-request jitter), so the timings and latencies are exact too under a fake clock; under the
/// real host they follow the wall clock's tick times, and a real run's p95 and per-second counts
/// vary slightly (about a request per second) from one run to the next. The clock and the delays
/// come from the <see cref="ILiveViewHost"/> the console manager samples on, so the script and
/// the live view share one clock and a test drives the whole script in milliseconds under a fake
/// one. A stop — Ctrl+C or the quit key, both landing on the state's cancellation token — ends
/// the execution phase at the next tick with the measurement finalized as the consumer manager
/// finalizes a stopped run and no verdict evaluated, as none is on a stopped run; cleanup then
/// runs in full, as a real run's cleanup hooks do.
/// </summary>
internal sealed class LiveViewDemoScript
{
    /// <summary>The demo scenario's name, as the dashboard titles its section.</summary>
    public const string ScenarioName = "Checkout flow (demo)";

    /// <summary>The whole run's duration when <c>--demo-duration</c> names none.</summary>
    public static readonly TimeSpan DefaultDuration = TimeSpan.FromSeconds(60);

    /// <summary>The shortest run the script plays: below it the phases and the incident inside them are too short to show a shape.</summary>
    public static readonly TimeSpan MinimumDuration = TimeSpan.FromSeconds(20);

    /// <summary>What the init phase takes — the before-test and before-scenario hooks of a real run.</summary>
    public static readonly TimeSpan InitDuration = TimeSpan.FromSeconds(1);

    /// <summary>What the cleanup phase takes — the after-scenario and after-test hooks of a real run.</summary>
    public static readonly TimeSpan CleanupDuration = TimeSpan.FromSeconds(1);

    /// <summary>The interval every fixed rate is per: one second, so the rates read as requests per second.</summary>
    public static readonly TimeSpan RateInterval = TimeSpan.FromSeconds(1);

    /// <summary>The share of the load time — the run's duration less init and cleanup — the warmup simulation takes.</summary>
    public const int WarmupPercent = 8;

    /// <summary>The share of the load time the ramp-up simulation takes.</summary>
    public const int RampPercent = 25;

    /// <summary>The share of the load time the steady simulation takes: what the warmup and the ramp leave, to the tick.</summary>
    public const int SteadyPercent = 100 - WarmupPercent - RampPercent;

    /// <summary>The warmup simulation: a fixed load at this rate per second.</summary>
    public const int WarmupRate = 10;

    /// <summary>The ramp-up simulation: a gradual load increase from this rate to <see cref="RampEndRate"/>.</summary>
    public const int RampStartRate = 10;

    public const int RampEndRate = 100;

    /// <summary>The steady-state simulation: a fixed load at this rate per second.</summary>
    public const int SteadyRate = 100;

    /// <summary>
    /// The cadence the script records at: every tick records the iterations that have come due
    /// since the previous one, so the collector fills smoothly under the console manager's 1 Hz
    /// sampling instead of in once-a-second bursts. A simulation's last tick lands on its end
    /// instead of past it, so a phase whose duration is not a whole number of ticks still ends
    /// exactly when its configuration says.
    /// </summary>
    public static readonly TimeSpan TickInterval = TimeSpan.FromMilliseconds(100);

    /// <summary>The 95th-percentile response time the demo scenario declares a threshold on.</summary>
    public static readonly TimeSpan ResponseTimePercentile95Limit = TimeSpan.FromMilliseconds(500);

    /// <summary>The error rate the demo scenario declares a threshold on, as a fraction of all requests.</summary>
    public const double ErrorRateLimit = 0.02;

    /// <summary>How far into the steady phase the scripted incident begins, as a share of it.</summary>
    public const double IncidentStartShare = 0.40;

    /// <summary>How long "Place order" stays degraded, as a share of the steady phase, counted from the incident's start.</summary>
    public const double LatencyIncidentShare = 0.15;

    /// <summary>How long the HTTP 503 burst lasts, as a share of the steady phase, counted from the incident's start.</summary>
    public const double ErrorBurstShare = 0.05;

    /// <summary>What the incident multiplies the degraded step's response time by.</summary>
    public const double IncidentLatencyFactor = 4.0;

    /// <summary>One iteration in this many is degraded while the latency incident lasts: a fifth of them, well past the p95 threshold's five percent, so every interval of the incident breaches it.</summary>
    public const int LatencyIncidentEveryIterations = 5;

    /// <summary>One iteration in this many fails while the error burst lasts: a quarter of them, more than ten times the declared error rate.</summary>
    public const int ErrorBurstEveryIterations = 4;

    /// <summary>The step the incident degrades and the burst fails, by index into <see cref="Steps"/>: "Place order", the last one.</summary>
    public const int IncidentStepIndex = 2;

    /// <summary>
    /// The failure schedule outside the incident: once the run has reached the steady state,
    /// every <see cref="FailureEveryIterations"/>-th measurement iteration fails at one of its
    /// steps — the failures cycle through <see cref="Failures"/>, so all three distinct errors
    /// show up in the error ticker while the run is healthy. Sparse enough that the trickle alone
    /// is well inside <see cref="ErrorRateLimit"/>: the burst is what breaches it, live. Warmup
    /// and ramp-up iterations never fail, and waiting for the steady state is what keeps that
    /// true of the live gauge as well as of the verdict: a lone failure reads one over its
    /// interval's request count, so it stays inside <see cref="ErrorRateLimit"/> only while the
    /// interval holds 1 / 0.02 = 50 requests, and a second of the steady state holds
    /// <see cref="SteadyRate"/> of them — twice that, at every duration — where a ramp second
    /// holds only as many as the rate it has climbed to. Sparse enough, too, that no two of the
    /// trickle's failures share one interval: <see cref="FailureEveryIterations"/> iterations at
    /// <see cref="SteadyRate"/> are two seconds apart.
    /// </summary>
    public const int FailureEveryIterations = 200;

    /// <summary>The steps every iteration runs, in order, with their unloaded response times.</summary>
    public static readonly IReadOnlyList<DemoStep> Steps = new[]
    {
        new DemoStep("Browse products", "browse-products", TimeSpan.FromMilliseconds(25)),
        new DemoStep("Add to cart", "add-to-cart", TimeSpan.FromMilliseconds(40)),
        new DemoStep("Place order", "place-order", TimeSpan.FromMilliseconds(85))
    };

    /// <summary>The failure the error burst fails "Place order" with: the incident's HTTP 503, fast as a rejection is.</summary>
    public static readonly DemoFailure ErrorBurstFailure = new DemoFailure(IncidentStepIndex, "HTTP 503 Service Unavailable", TimeSpan.FromMilliseconds(12), message => new InvalidOperationException(message));

    /// <summary>
    /// The failures the trickle cycles through, one per step: the step that fails, its error and
    /// how long the failed step took. The burst's 503 is the first of them, so the healthy run's
    /// trickle and the incident's burst are one error on one step in the ticker, as a repeated
    /// failure of the same dependency is.
    /// </summary>
    public static readonly IReadOnlyList<DemoFailure> Failures = new[]
    {
        ErrorBurstFailure,
        new DemoFailure(1, "Request timed out after 2000 ms", TimeSpan.FromMilliseconds(2000), message => new TimeoutException(message)),
        new DemoFailure(0, "HTTP 500 Internal Server Error", TimeSpan.FromMilliseconds(80), message => new InvalidOperationException(message))
    };

    // How response times grow with load: at SteadyRate a step takes (1 + LoadLatencyFactor) times
    // its unloaded time, so the p95 sparkline climbs through the ramp.
    private const double LoadLatencyFactor = 0.9;

    // A slow triangle-wave wobble on top of the load factor, so the steady state's latency moves.
    private const double WobbleAmplitude = 0.12;
    private static readonly TimeSpan WobblePeriod = TimeSpan.FromSeconds(5);

    // Per-request jitter: a uniform factor in [1 - JitterAmplitude, 1 + JitterAmplitude], and a
    // spike by SpikeFactor on a SpikeProbability share of requests, which is what lifts p95 and
    // p99 clear of the mean. Both are small enough that the healthy steady state's intervals stay
    // well inside ResponseTimePercentile95Limit — the incident is what breaches it.
    private const double JitterAmplitude = 0.25;
    private const double SpikeProbability = 0.01;
    private const double SpikeFactor = 2.0;

    // Absorbs floating-point noise below an integer when flooring an expected count, so a count
    // that is mathematically whole is never one short.
    private const double CountEpsilon = 1e-9;

    private readonly TestExecutionState _testExecutionState;
    private readonly ILiveViewHost _liveViewHost;
    private readonly Scenario _scenario;
    private readonly ScenarioLoadCollector _collector;
    private DateTime _scriptStartTime;
    private ILoadConfiguration? _steadySimulation;
    private DateTime _incidentStartTime = DateTime.MaxValue;
    private DateTime _latencyIncidentEndTime = DateTime.MaxValue;
    private DateTime _errorBurstEndTime = DateTime.MaxValue;
    private long _measurementIterationCount;
    private long _latencyIncidentIterationCount;
    private long _errorBurstIterationCount;
    private long _failureCount;
    private ulong _generatorState = 0x9E3779B97F4A7C15UL;

    /// <param name="testExecutionState">An initialized state whose single scenario is the one <see cref="CreateScenario"/> built.</param>
    /// <param name="liveViewHost">The clock and the delays — the host the console manager samples on, so the script shares its clock.</param>
    /// <param name="duration">What the whole run takes, init and cleanup included; at least <see cref="MinimumDuration"/>.</param>
    public LiveViewDemoScript(TestExecutionState testExecutionState, ILiveViewHost liveViewHost, TimeSpan duration)
    {
        if (testExecutionState == null)
            throw new ArgumentNullException(nameof(testExecutionState), "Test execution state cannot be null.");
        if (liveViewHost == null)
            throw new ArgumentNullException(nameof(liveViewHost), "Live view host cannot be null.");
        if (testExecutionState.Scenarios.Count != 1)
            throw new ArgumentException("The demo script plays exactly one scenario; the state has " + testExecutionState.Scenarios.Count + ".", nameof(testExecutionState));
        if (duration < MinimumDuration)
            throw new ArgumentOutOfRangeException(nameof(duration), duration, "The demo runs for at least " + MinimumDuration.TotalSeconds + " seconds.");

        _testExecutionState = testExecutionState;
        _liveViewHost = liveViewHost;
        _scenario = testExecutionState.Scenarios[0];
        _collector = testExecutionState.LoadCollectors[_scenario.Name];

        Duration = duration;
        var loadDuration = duration - InitDuration - CleanupDuration;
        WarmupDuration = TimeSpan.FromTicks(loadDuration.Ticks * WarmupPercent / 100);
        RampDuration = TimeSpan.FromTicks(loadDuration.Ticks * RampPercent / 100);
        SteadyDuration = loadDuration - WarmupDuration - RampDuration;
        IncidentStartOffset = TimeSpan.FromTicks((long)(SteadyDuration.Ticks * IncidentStartShare));
        LatencyIncidentDuration = TimeSpan.FromTicks((long)(SteadyDuration.Ticks * LatencyIncidentShare));
        ErrorBurstDuration = TimeSpan.FromTicks((long)(SteadyDuration.Ticks * ErrorBurstShare));
    }

    /// <summary>What the whole run takes: init, the three simulations and cleanup.</summary>
    public TimeSpan Duration { get; }

    /// <summary>What the warmup simulation runs for.</summary>
    public TimeSpan WarmupDuration { get; }

    /// <summary>What the ramp-up simulation runs for.</summary>
    public TimeSpan RampDuration { get; }

    /// <summary>What the steady simulation runs for: the load time the warmup and the ramp leave, which is its <see cref="SteadyPercent"/> per cent of it to the tick.</summary>
    public TimeSpan SteadyDuration { get; }

    /// <summary>How far into the steady phase the scripted incident begins.</summary>
    public TimeSpan IncidentStartOffset { get; }

    /// <summary>How long "Place order" stays degraded from the incident's start.</summary>
    public TimeSpan LatencyIncidentDuration { get; }

    /// <summary>How long the HTTP 503 burst lasts from the incident's start.</summary>
    public TimeSpan ErrorBurstDuration { get; }

    /// <summary>
    /// The demo scenario: named, with the three steps, the two declared thresholds the incident
    /// plays against and a simulations action — what makes a scenario a load test. The
    /// thresholds are declared through the same <see cref="ThresholdsBuilder"/> a real scenario
    /// declares them through, so the live gauges and the completion verdict read them as they
    /// read any scenario's. The simulations themselves are added when init completes, where
    /// <c>SetupSimulations</c> adds a real scenario's, so the live view shows the init
    /// placeholder until then, as on a real run.
    /// </summary>
    public static Scenario CreateScenario()
    {
        var scenario = new Scenario(ScenarioName);
        scenario.Id = "checkout-flow-demo";
        scenario.Description = "A scripted synthetic load run for the standalone runner's live view; no target system is called.";
        foreach (var step in Steps)
            scenario.Steps.Add(new Step { Name = step.Name, Id = step.Id });

        new ThresholdsBuilder(scenario.Thresholds)
            .ResponseTimePercentile95(ResponseTimePercentile95Limit)
            .ErrorRate(ErrorRateLimit);

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

        // The steady load, added last, is the one the incident plays inside.
        _steadySimulation = _scenario.SimulationsInternal[_scenario.SimulationsInternal.Count - 1];

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
    /// run, the declared thresholds judged where the execution manager judges them, and the
    /// execute phase marked complete. Returns normally on a stop — the producer's cancellation
    /// is absorbed inside the execution manager on a real run, too.
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

        if (!isStopped)
            EvaluateThresholds();

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
    /// The completion verdict, as the execution manager evaluates one: the declared thresholds
    /// read once off the cumulative result where the AssertWhenDone callback would run, the
    /// results stored on the collector for the summary, and a violation failing the scenario the
    /// way an assert failure does. The demo's thresholds are chosen to pass — the incident
    /// breaches them live, never cumulatively — so the violation path is the mirror of a real
    /// run's, not a path the demo takes.
    /// </summary>
    private void EvaluateThresholds()
    {
        if (_scenario.Thresholds.Count == 0)
            return;

        var thresholdResults = ThresholdEvaluator.EvaluateVerdict(_scenario.Thresholds, _collector.GetCurrentResult());
        _collector.SetThresholdResults(thresholdResults);

        var violations = thresholdResults.Where(thresholdResult => !thresholdResult.Passed).ToList();
        if (violations.Count == 0)
            return;

        var exception = new ThresholdViolationException(violations);
        _testExecutionState.FirstException = exception;
        _testExecutionState.TestResult.Status = TestStatus.Failed;
        _collector.SetAssertWhenDoneException(exception);
        _collector.SetStatus(TestStatus.Failed);
    }

    /// <summary>
    /// Plays one simulation: on every tick, records the iterations due by the elapsed time —
    /// the count the simulation's rate promises for that much of its duration, so the total at
    /// the end is exact whatever the ticks' lateness — at the rate current then, and ends when
    /// the duration has elapsed or a stop has been requested. Ticks are scheduled on an absolute
    /// cadence so a slow tick does not drift the ones after it, and the last one is pulled back
    /// to the simulation's end so a duration that is not a whole number of ticks does not
    /// overshoot it. The steady simulation is where the scripted incident plays, scheduled
    /// against its start.
    /// </summary>
    private async Task PlaySimulation(ILoadConfiguration simulation)
    {
        var duration = DurationOf(simulation);
        var startTime = _liveViewHost.UtcNow;
        var endTime = startTime + duration;
        var nextTickTime = startTime;
        var recordedCount = 0L;

        if (ReferenceEquals(simulation, _steadySimulation))
            ScheduleIncident(startTime);

        while (!IsStopRequested())
        {
            var now = _liveViewHost.UtcNow;
            var elapsed = now - startTime;
            if (elapsed > duration)
                elapsed = duration;

            var rate = RateAt(simulation, elapsed);
            var dueCount = (long)Math.Floor(ExpectedIterationCount(simulation, elapsed) + CountEpsilon);
            for (; recordedCount < dueCount; recordedCount++)
                RecordIteration(simulation, rate, now);

            if (elapsed >= duration)
                return;

            nextTickTime += TickInterval;
            if (nextTickTime > endTime)
                nextTickTime = endTime;

            var delay = nextTickTime - now;
            if (delay > TimeSpan.Zero)
                await _liveViewHost.Delay(delay, _testExecutionState.CancellationToken);
        }
    }

    /// <summary>Fixes the incident's window against the steady phase's start, once that start is known.</summary>
    private void ScheduleIncident(DateTime steadyStartTime)
    {
        _incidentStartTime = steadyStartTime + IncidentStartOffset;
        _latencyIncidentEndTime = _incidentStartTime + LatencyIncidentDuration;
        _errorBurstEndTime = _incidentStartTime + ErrorBurstDuration;
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
    /// step passed with a response time for the current load, degraded when the incident has
    /// this iteration, or, on a scheduled failure, the failing step failed with its error and
    /// duration and the steps after it skipped, as the step handler leaves them — and the
    /// iteration's execute time as the sum of its steps.
    /// </summary>
    private void RecordIteration(ILoadConfiguration simulation, double rate, DateTime now)
    {
        var isWarmup = simulation.IsWarmup;
        DemoFailure? failure = null;
        var isDegraded = false;
        if (!isWarmup)
        {
            failure = NextScheduledFailure(ReferenceEquals(simulation, _steadySimulation), now);
            isDegraded = NextScheduledDegradation(now);
        }

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
                stepResult.Duration = ResponseTime(step, rate, now, isDegraded && index == IncidentStepIndex);
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

    /// <summary>
    /// The failure the schedule assigns to the next measurement iteration, or null when it
    /// passes: while the error burst lasts, every
    /// <see cref="ErrorBurstEveryIterations"/>-th iteration of it takes the burst's 503;
    /// otherwise the trickle's, once the run is in the steady state and the iteration is one of
    /// <see cref="FailureEveryIterations"/>. An iteration the burst has already failed does not
    /// advance the trickle's cycle: one failure, one error.
    /// </summary>
    private DemoFailure? NextScheduledFailure(bool isSteady, DateTime now)
    {
        _measurementIterationCount++;

        if (now >= _incidentStartTime && now < _errorBurstEndTime)
        {
            _errorBurstIterationCount++;
            if (_errorBurstIterationCount % ErrorBurstEveryIterations == 0)
                return ErrorBurstFailure;
        }

        if (!isSteady || _measurementIterationCount % FailureEveryIterations != 0)
            return null;

        var failure = Failures[(int)(_failureCount % Failures.Count)];
        _failureCount++;
        return failure;
    }

    /// <summary>
    /// Whether the incident degrades the next measurement iteration's "Place order": while the
    /// latency incident lasts, every <see cref="LatencyIncidentEveryIterations"/>-th iteration
    /// of it — the share of a partly degraded dependency, not all of the traffic.
    /// </summary>
    private bool NextScheduledDegradation(DateTime now)
    {
        if (now < _incidentStartTime || now >= _latencyIncidentEndTime)
            return false;

        _latencyIncidentIterationCount++;
        return _latencyIncidentIterationCount % LatencyIncidentEveryIterations == 0;
    }

    /// <summary>
    /// A passed step's response time: its unloaded time, grown by the load factor for the
    /// current rate, moved by the slow wobble, jittered per request with the occasional spike,
    /// and multiplied by <see cref="IncidentLatencyFactor"/> when the incident has this step.
    /// </summary>
    private TimeSpan ResponseTime(DemoStep step, double rate, DateTime now, bool isDegraded)
    {
        var loadFactor = 1.0 + LoadLatencyFactor * (rate / SteadyRate);
        var wobbleFactor = 1.0 + WobbleAmplitude * TriangleWave(now - _scriptStartTime);
        var jitterFactor = 1.0 - JitterAmplitude + 2 * JitterAmplitude * NextUnitInterval();
        var spikeFactor = 1.0;
        if (NextUnitInterval() < SpikeProbability)
            spikeFactor = SpikeFactor;

        var incidentFactor = 1.0;
        if (isDegraded)
            incidentFactor = IncidentLatencyFactor;

        var milliseconds = step.ResponseTime.TotalMilliseconds * loadFactor * wobbleFactor * jitterFactor * spikeFactor * incidentFactor;
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
