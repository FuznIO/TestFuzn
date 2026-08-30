using Fuzn.TestFuzn.Internals.State;
using Fuzn.TestFuzn.Internals.Terminal;
using Fuzn.TestFuzn.StandaloneRunner;
using Fuzn.TestFuzn.Tests.StandaloneRunner;

namespace Fuzn.TestFuzn.Tests.Terminal;

/// <summary>
/// Pins <see cref="LiveViewDemo.Run"/> — the demo's run over the real console manager and
/// execution state — hermetically, on virtual time: one host shared by the console manager's
/// loop and the demo script whose clock advances only when both are waiting, to the earliest
/// wake time, so exactly one of them runs at a time and the whole demo — the shortest one the
/// runner accepts, since what is pinned here holds at every duration — plays deterministically
/// in milliseconds. Pinned: on a terminal with live view support the
/// alternate screen is entered and left exactly once with the cursor restored and the summary
/// written once after the restore; on a redirected output the plain lines carry no escape byte
/// and end in the completed line with the exact totals, the summary after it; the state is
/// disposed and nothing is left waiting once Run returns; the quit key stops the run through
/// the Ctrl+C path, the terminal restored and the cancellation reported after the summary; and
/// the runner core maps a completed demo to exit code 0 and a stopped one to 1. On both output
/// paths the startup banner — the same three lines a test run opens with, the demo scenario as
/// the subject and its duration on the detail line — is the first terminal output, written
/// through the host before the live view starts; nothing goes through the adapter's markup. And
/// the runner core's <c>--demo-duration</c>: the run lasts what it names, the script's default
/// when it is absent, and an unreadable one is an invocation error with nothing run.
/// </summary>
[TestClass]
public class LiveViewDemoTests : Test
{
    private const string EnterSequence = AnsiCodes.EnterAlternateScreen + AnsiCodes.HideCursor + AnsiCodes.DisableAutoWrap;
    private const string RestoreSequence = AnsiCodes.Reset + AnsiCodes.EnableAutoWrap + AnsiCodes.ShowCursor + AnsiCodes.ExitAlternateScreen;
    private const string TerminalEventPrefix = "terminal:";
    private const string ScenarioLinePrefix = "[" + LiveViewDemoScript.ScenarioName + "] ";

    // The demo runs at its shortest here: what these tests pin — the frames, the plain lines, the
    // stop and the exit codes — is the same at every duration, and the shortest run keeps the
    // virtual-time replay small. The totals are the shortest run's, pinned by the script's tests.
    private static readonly TimeSpan DemoDuration = LiveViewDemoScript.MinimumDuration;
    private const int MeasurementIterationCount = 1453;
    private const int FailureCount = 21;

    private static readonly TimeSpan RunTimeout = TimeSpan.FromSeconds(30);

    // The demo's banner as written on a terminal without color (both harness hosts render plain),
    // its detail line naming the duration the run was asked for.
    private static string[] BannerWrites(TimeSpan duration)
    {
        return new[]
        {
            "⚡ TestFuzn" + Environment.NewLine,
            LiveViewDemo.RunningDemoLabel + " " + LiveViewDemoScript.ScenarioName + Environment.NewLine,
            LiveViewDemo.DemoDetail(duration) + Environment.NewLine
        };
    }

    private static DateTime At(double seconds)
    {
        return SyntheticLoadSnapshots.At(seconds);
    }

    [Test]
    public async Task Verify_demo_runs_to_completion_on_the_dashboard_and_restores_the_terminal_before_the_summary()
    {
        await Scenario()
            .Step("The banner precedes the alternate screen, which is entered once and left once as the last terminal write, then the summary; the state is disposed and nothing is left waiting", async context =>
            {
                var harness = new Harness(supportsLiveView: true);
                await harness.RunDemo();

                harness.AssertEnteredAndRestoredOnce();
                harness.AssertSummaryFollowsRestore();
                harness.AssertRunLeftNothingBehind();

                // The banner is the first terminal output, on the normal screen buffer before
                // the alternate screen is entered; the adapter's markup carries nothing.
                harness.AssertBannerFirst();
                Assert.IsEmpty(harness.MarkupEvents());

                // The banner's three writes, then the script's 20 s at the 250 ms render cadence:
                // 80 frames between entering and leaving, every one repainted (the spinner
                // advances per frame); the loop's tick due when the run ends is cancelled before
                // it renders.
                Assert.HasCount(StartupBanner.Height + 82, harness.Writer.Writes);
                Assert.AreEqual(At(DemoDuration.TotalSeconds), harness.Host.UtcNow);
                Assert.AreEqual(ExecutionStatus.Completed, harness.State.ExecutionStatus);
                Assert.IsGreaterThan(0, harness.Host.Reader.TryReadKeyCallCount);
            })
            .Step("The runner core maps the completed demo to exit code 0", async context =>
            {
                var harness = new Harness(supportsLiveView: true);
                var exitCode = await harness.RunDemoThroughRunnerCore();

                Assert.AreEqual(0, exitCode);
                harness.AssertEnteredAndRestoredOnce();
                harness.AssertSummaryFollowsRestore();
                Assert.AreEqual(0, harness.Host.PendingWaiterCount);
            })
            .Run();
    }

    [Test]
    public async Task Verify_demo_writes_plain_lines_on_a_redirected_output_ending_in_the_completed_line_before_the_summary()
    {
        await Scenario()
            .Step("No escape byte anywhere: the plain banner first, then the init placeholder, the phase transitions in order, the completed line with the exact totals last, then the summary; no key or size ever read", async context =>
            {
                var harness = new Harness(supportsLiveView: false);
                await harness.RunDemo();

                harness.AssertBannerFirst();
                var lines = harness.PlainLines();
                Assert.AreEqual(ScenarioLinePrefix + "phase: init", lines[StartupBanner.Height]);
                Assert.AreEqual(ScenarioLinePrefix + "elapsed 00:00:00  total 0  ok 0  failed 0  rps N/A  p95 N/A", lines[StartupBanner.Height + 1]);
                CollectionAssert.AreEqual(new[]
                {
                    "init",
                    "warmup: Fixed Load 10 rps",
                    "sim 1/2: Gradual Load 10→100 rps",
                    "sim 2/2: Fixed Load 100 rps"
                }, PhaseLabels(lines).Take(4).ToList());
                // The final line ends in the completion verdict: the demo declares two thresholds
                // and its cumulative statistics hold both, so it reads ok.
                Assert.MatchesRegex(@"^\[Checkout flow \(demo\)\] completed  elapsed 00:00:20  total " + MeasurementIterationCount + "  ok " + (MeasurementIterationCount - FailureCount) + "  failed " + FailureCount + @"  p95 .+  thresholds: ok$", lines[lines.Count - 1]);
                Assert.AreEqual(1, lines.Count(line => line.StartsWith(ScenarioLinePrefix + "completed", StringComparison.Ordinal)));

                harness.AssertSummaryFollowsLastTerminalWrite();
                harness.AssertRunLeftNothingBehind();
                Assert.AreEqual(0, harness.Host.Reader.TryReadKeyCallCount);
                Assert.AreEqual(0, harness.Writer.WindowWidthReadCount);
                Assert.AreEqual(0, harness.Writer.WindowHeightReadCount);
                Assert.AreEqual(ExecutionStatus.Completed, harness.State.ExecutionStatus);
                Assert.IsEmpty(harness.MarkupEvents());
            })
            .Run();
    }

    [Test]
    public async Task Verify_quit_key_stops_the_demo_through_the_Ctrl_C_path_with_the_terminal_restored_and_the_stop_reported_after_the_summary()
    {
        await Scenario()
            .Step("q mid-ramp: the run stops as after Ctrl+C, cleanup still runs, the terminal is restored once, the summary follows, and Run ends with the cancellation", async context =>
            {
                var harness = new Harness(supportsLiveView: true);
                harness.PressQuitKeyAt(At(6));

                await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () => await harness.RunDemo());

                harness.AssertEnteredAndRestoredOnce();
                harness.AssertSummaryFollowsRestore();
                harness.AssertRunLeftNothingBehind();
                Assert.AreEqual(ExecutionStatus.Stopped, harness.State.ExecutionStatus);
                Assert.IsNull(harness.State.ExecutionStoppedReason);
                Assert.IsNull(harness.State.FirstException);
                Assert.IsFalse(harness.State.IsConsumingCompleted);
                // Stopped on the loop's first tick after the key, then the full cleanup second.
                Assert.AreEqual(At(7.25), harness.Host.UtcNow);
                Assert.IsEmpty(harness.MarkupEvents());
            })
            .Step("The runner core maps the stopped demo to exit code 1 and writes the single stopped line after the restore, in place of a trace", async context =>
            {
                var harness = new Harness(supportsLiveView: true);
                harness.PressQuitKeyAt(At(6));

                var exitCode = await harness.RunDemoThroughRunnerCore();

                Assert.AreEqual(1, exitCode);
                harness.AssertEnteredAndRestoredOnceThenStoppedLine();
                harness.AssertSummaryFollowsRestore();
                Assert.AreEqual(0, harness.Host.PendingWaiterCount);
            })
            .Run();
    }

    [Test]
    public async Task Verify_a_violated_threshold_verdict_fails_the_run_with_exit_code_1_as_an_assert_does()
    {
        await Scenario()
            .Step("A threshold no run can hold makes the verdict violated: the runner core traces the violation and exits 1, where the same demo with the script's own thresholds exits 0", async context =>
            {
                var harness = new Harness(supportsLiveView: false, DemoDuration, ViolatingScenario);

                var exitCode = await harness.RunDemoThroughRunnerCore();

                Assert.AreEqual(1, exitCode);

                // The assert-when-done pathway, unchanged: the exception is the run's first, the
                // scenario's assert-when-done failure, and both the scenario and the test result
                // are Failed — exactly what an AssertWhenDone failure leaves behind.
                var exception = Assert.IsInstanceOfType<ThresholdViolationException>(harness.State.FirstException);
                var result = harness.State.LoadCollectors[LiveViewDemoScript.ScenarioName].GetCurrentResult(true);
                Assert.AreSame(exception, result.AssertWhenDoneException);
                Assert.AreEqual(TestStatus.Failed, result.Status);
                Assert.AreEqual(TestStatus.Failed, harness.State.TestResult.Status);
                Assert.ContainsSingle(exception.Violations);

                // The run completed — a verdict never stops a run — so the stopped line is not
                // what was written: the violation is traced, as any run failure is.
                Assert.AreEqual(ExecutionStatus.Completed, harness.State.ExecutionStatus);
                var lines = harness.PlainLines();
                Assert.DoesNotContain(StandaloneRunnerCore.RunStoppedMessage, lines);
                Assert.Contains(nameof(ThresholdViolationException) + ": " + exception.Message, lines);
            })
            .Step("The plain final line reports the same verdict the exit code came from: failed, breached, and the violation as the reason", async context =>
            {
                var harness = new Harness(supportsLiveView: false, DemoDuration, ViolatingScenario);

                await Assert.ThrowsExactlyAsync<ThresholdViolationException>(async () => await harness.RunDemo());

                var finalLine = harness.PlainLines().Last(line => line.StartsWith(ScenarioLinePrefix, StringComparison.Ordinal));
                Assert.StartsWith(ScenarioLinePrefix + "failed  ", finalLine);
                Assert.Contains("  thresholds: breached (1)  reason: Threshold violated: mean ", finalLine);
            })
            .Run();
    }

    /// <summary>
    /// A fresh demo scenario with a mean response time of one millisecond added to its thresholds
    /// — a limit no run of the scripted load can hold, so the completion verdict is violated
    /// where the script's own two thresholds always hold.
    /// </summary>
    private static Scenario ViolatingScenario()
    {
        var scenario = LiveViewDemoScript.CreateScenario();
        new ThresholdsBuilder(scenario.Thresholds).ResponseTimeMean(TimeSpan.FromMilliseconds(1));
        return scenario;
    }

    [Test]
    public async Task Verify_the_demo_duration_argument_decides_how_long_the_demo_runs_and_an_invalid_one_is_an_invocation_error()
    {
        await Scenario()
            .Step("--demo-duration=25 plays the scripted load for 25 seconds, the banner naming the duration", async context =>
            {
                var harness = new Harness(supportsLiveView: false, TimeSpan.FromSeconds(25));

                var exitCode = await harness.RunDemoThroughRunnerCore();

                Assert.AreEqual(0, exitCode);
                harness.AssertBannerFirst();
                Assert.AreEqual(At(25), harness.Host.UtcNow);
                Assert.IsEmpty(harness.MarkupEvents());
            })
            .Step("Without the argument the demo runs for its default duration", async context =>
            {
                var harness = new Harness(supportsLiveView: false, LiveViewDemoScript.DefaultDuration);

                var exitCode = await harness.RunThroughRunnerCore(new[] { "run", "--" + StandaloneRunnerCore.DemoFlag });

                Assert.AreEqual(0, exitCode);
                harness.AssertBannerFirst();
                Assert.AreEqual(At(LiveViewDemoScript.DefaultDuration.TotalSeconds), harness.Host.UtcNow);
            })
            .Step("A duration below the minimum, a fractional one, text, an empty value or a bare flag is an invocation error: the usage message through the adapter and exit code 1, with no banner and no run", async context =>
            {
                foreach (var argument in new[] { "--demo-duration=19", "--demo-duration=0", "--demo-duration=-5", "--demo-duration=20.5", "--demo-duration=abc", "--demo-duration=", "--demo-duration" })
                {
                    var harness = new Harness(supportsLiveView: false);

                    var exitCode = await harness.RunThroughRunnerCore(new[] { "run", "--" + StandaloneRunnerCore.DemoFlag, argument });

                    Assert.AreEqual(1, exitCode, argument);
                    var usage = Assert.ContainsSingle(harness.MarkupEvents());
                    Assert.AreEqual("[red]" + StandaloneRunnerCore.DemoDurationUsage + "[/]", usage, argument);
                    Assert.IsEmpty(harness.Writer.Writes, argument);
                    Assert.AreEqual(At(0), harness.Host.UtcNow, argument);
                }
            })
            .Run();
    }

    /// <summary>The phase labels from the plain lines, in order of appearance.</summary>
    private static List<string> PhaseLabels(List<string> lines)
    {
        var phasePrefix = ScenarioLinePrefix + "phase: ";
        return lines.Where(line => line.StartsWith(phasePrefix, StringComparison.Ordinal)).Select(line => line.Substring(phasePrefix.Length)).ToList();
    }

    /// <summary>
    /// The demo over the virtual-time host, a fake adapter and the shared event log both write
    /// to — terminal writes prefixed <c>terminal:</c>, the adapter's events as it records them —
    /// so ordering across the two can be asserted.
    /// </summary>
    private sealed class Harness
    {
        public List<string> Events { get; } = new List<string>();

        public VirtualTimeHost Host { get; }

        public FakeTestFrameworkAdapter TestFramework { get; }

        /// <summary>
        /// The demo the harness runs: the one built for <see cref="Duration"/>, replaced by the
        /// one the runner core asked the harness's factory for — from the same scenario source,
        /// for the duration the command line named — once the core has run.
        /// </summary>
        public LiveViewDemo Demo { get; private set; }

        /// <summary>
        /// Where every demo this harness builds gets its scenario. A factory, not one scenario:
        /// the demo's script adds the simulations to the scenario it runs, so two demos over one
        /// instance would run different load profiles — the second one's twice over.
        /// </summary>
        private readonly Func<Scenario> _createScenario;

        public FakeTerminalWriter Writer => Host.Writer;

        /// <summary>The state the demo ran over; throws before a run.</summary>
        public TestExecutionState State
        {
            get
            {
                var state = Demo.TestExecutionState;
                if (state == null)
                    throw new InvalidOperationException("The demo has not run.");

                return state;
            }
        }

        /// <summary>What the demo was asked to run for — the banner names it and the runner core is given it.</summary>
        public TimeSpan Duration { get; }

        /// <param name="supportsLiveView">Live view capabilities (interactive, ANSI, no color so frames stay plain) or a redirected output and input.</param>
        public Harness(bool supportsLiveView)
            : this(supportsLiveView, DemoDuration)
        {
        }

        /// <param name="supportsLiveView">Live view capabilities (interactive, ANSI, no color so frames stay plain) or a redirected output and input.</param>
        /// <param name="duration">What the demo runs for.</param>
        public Harness(bool supportsLiveView, TimeSpan duration)
            : this(supportsLiveView, duration, LiveViewDemoScript.CreateScenario)
        {
        }

        /// <param name="supportsLiveView">Live view capabilities (interactive, ANSI, no color so frames stay plain) or a redirected output and input.</param>
        /// <param name="duration">What the demo runs for.</param>
        /// <param name="createScenario">Builds the scenario a demo records against — the script's own, or one whose thresholds the run cannot hold — once per demo.</param>
        public Harness(bool supportsLiveView, TimeSpan duration, Func<Scenario> createScenario)
        {
            Duration = duration;
            TerminalCapabilities capabilities;
            if (supportsLiveView)
                capabilities = new TerminalCapabilities(isInteractive: true, supportsAnsi: true, colorMode: ColorMode.None);
            else
                capabilities = TerminalCapabilities.Resolve(isOutputRedirected: true, isInputRedirected: true, isVirtualTerminalEnabled: true, term: "xterm-256color", colorTerm: null, noColor: null);

            // Two participants share the clock: the console manager's loop and the demo script.
            Host = new VirtualTimeHost(participantCount: 2, capabilities);
            Host.Writer.WriteObserver = text =>
            {
                lock (Events)
                    Events.Add(TerminalEventPrefix + text);
            };
            TestFramework = new FakeTestFrameworkAdapter(Events);
            _createScenario = createScenario;
            Demo = new LiveViewDemo(Host, duration, createScenario());
        }

        /// <summary>Runs the demo as the runner core does; a scheduling deadlock fails the test by timeout instead of hanging it.</summary>
        public async Task RunDemo()
        {
            await Demo.Run(TestFramework).WaitAsync(RunTimeout);
        }

        /// <summary>Runs <c>run --demo</c> through the runner core over this harness's host, asking for its duration, for the exit code.</summary>
        public async Task<int> RunDemoThroughRunnerCore()
        {
            return await RunThroughRunnerCore(new[] { "run", "--" + StandaloneRunnerCore.DemoFlag, "--" + StandaloneRunnerCore.DemoDurationArgument + "=" + (int)Duration.TotalSeconds });
        }

        /// <summary>
        /// Runs the runner core over this harness's host and adapter with the given command line,
        /// for the exit code. The core runs a demo of this harness's own — a fresh scenario from
        /// the source it was built with, not the script's default, as the production factory
        /// builds a fresh one — for the duration the command line asks for.
        /// </summary>
        public async Task<int> RunThroughRunnerCore(string[] args)
        {
            var runnerCore = new StandaloneRunnerCore(Host, new DiscoverTests(), duration =>
            {
                Demo = new LiveViewDemo(Host, duration, _createScenario());
                return Demo;
            });
            return await runnerCore.Run<FakeStartup>(typeof(LiveViewDemoTests).Assembly, args, () => TestFramework).WaitAsync(RunTimeout);
        }

        /// <summary>Presses the quit key once the virtual clock has reached the given time; the loop reads it on its next tick.</summary>
        public void PressQuitKeyAt(DateTime time)
        {
            var isPressed = false;
            Host.OnDelay = now =>
            {
                if (isPressed || now < time)
                    return;

                isPressed = true;
                Host.Reader.Press(LiveDashboardLayout.QuitKey);
            };
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
                Assert.DoesNotContain("", write);
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
                return Events.Where(eventName => eventName.StartsWith(FakeTestFrameworkAdapter.MarkupEventPrefix, StringComparison.Ordinal)).Select(eventName => eventName.Substring(FakeTestFrameworkAdapter.MarkupEventPrefix.Length)).ToList();
        }

        /// <summary>The startup banner's three plain lines, its detail line naming the run's duration, are the first terminal writes.</summary>
        public void AssertBannerFirst()
        {
            Assert.IsGreaterThanOrEqualTo(StartupBanner.Height, Writer.Writes.Count);
            CollectionAssert.AreEqual(BannerWrites(Duration), Writer.Writes.Take(StartupBanner.Height).ToList());
        }

        /// <summary>The alternate screen was entered once (the first write after the banner) and restored once (the last write); nothing follows the restore.</summary>
        public void AssertEnteredAndRestoredOnce()
        {
            AssertBannerFirst();
            Assert.AreEqual(EnterSequence, Writer.Writes[StartupBanner.Height]);
            Assert.AreEqual(1, Writer.Writes.Count(write => write == EnterSequence));
            Assert.AreEqual(1, Writer.Writes.Count(write => write == RestoreSequence));
            Assert.AreEqual(RestoreSequence, Writer.Writes[Writer.Writes.Count - 1]);
        }

        /// <summary>
        /// As <see cref="AssertEnteredAndRestoredOnce"/>, except that the runner core's stopped
        /// line — plain, since the harness host renders without color — is the one write after
        /// the restore: the stop is reported as a line, not traced.
        /// </summary>
        public void AssertEnteredAndRestoredOnceThenStoppedLine()
        {
            AssertBannerFirst();
            Assert.AreEqual(EnterSequence, Writer.Writes[StartupBanner.Height]);
            Assert.AreEqual(1, Writer.Writes.Count(write => write == EnterSequence));
            Assert.AreEqual(1, Writer.Writes.Count(write => write == RestoreSequence));
            Assert.AreEqual(RestoreSequence, Writer.Writes[Writer.Writes.Count - 2]);
            Assert.AreEqual(StandaloneRunnerCore.RunStoppedMessage + Environment.NewLine, Writer.Writes[Writer.Writes.Count - 1]);
        }

        /// <summary>The summary was written exactly once, after the terminal was restored.</summary>
        public void AssertSummaryFollowsRestore()
        {
            List<string> events;
            lock (Events)
                events = Events.ToList();

            Assert.AreEqual(1, events.Count(eventName => eventName == FakeTestFrameworkAdapter.SummaryEvent));
            Assert.IsGreaterThan(events.IndexOf(TerminalEventPrefix + RestoreSequence), events.IndexOf(FakeTestFrameworkAdapter.SummaryEvent));
        }

        /// <summary>The summary was written exactly once, after the last terminal write — the plain lines' final line.</summary>
        public void AssertSummaryFollowsLastTerminalWrite()
        {
            List<string> events;
            lock (Events)
                events = Events.ToList();

            Assert.AreEqual(1, events.Count(eventName => eventName == FakeTestFrameworkAdapter.SummaryEvent));
            var lastTerminalWrite = events.FindLastIndex(eventName => eventName.StartsWith(TerminalEventPrefix, StringComparison.Ordinal));
            Assert.IsGreaterThanOrEqualTo(0, lastTerminalWrite);
            Assert.IsGreaterThan(lastTerminalWrite, events.IndexOf(FakeTestFrameworkAdapter.SummaryEvent));
        }

        /// <summary>Nothing waits on the host any more — the loop has ended — and the state's token source has been disposed.</summary>
        public void AssertRunLeftNothingBehind()
        {
            Assert.AreEqual(0, Host.PendingWaiterCount);
            var state = State;
            Assert.ThrowsExactly<ObjectDisposedException>(() => _ = state.CancellationToken);
        }
    }

    /// <summary>
    /// An <see cref="ILiveViewHost"/> on virtual time shared by the console manager's loop and
    /// the demo script. A delay registers a waiter with its wake time and completes only once
    /// every participant is waiting: then the earliest waiter (ties by registration order) is
    /// released with the clock set to its wake time, so exactly one participant runs at a time
    /// and the run is deterministic. A delay on a token already cancelled returns at once without
    /// waiting, and a pending waiter whose token is cancelled is due at once — released by the
    /// cancellation callback, or by the scheduler before that callback has run — as the
    /// production delay returns then (the loop's last delay on Stop, the script's on a stop
    /// request). The hook is invoked with the clock at the start of every delay, before it can
    /// advance, which is where a test presses a key at a virtual time.
    /// </summary>
    private sealed class VirtualTimeHost : ILiveViewHost
    {
        private readonly object _gate = new object();
        private readonly List<Waiter> _waiters = new List<Waiter>();
        private readonly int _participantCount;
        private long _sequence;
        private DateTime _utcNow = SyntheticLoadSnapshots.BaseTime;

        public VirtualTimeHost(int participantCount, TerminalCapabilities capabilities)
        {
            _participantCount = participantCount;
            Capabilities = capabilities;
        }

        public FakeTerminalWriter Writer { get; } = new FakeTerminalWriter { WindowWidth = 120, WindowHeight = 40 };

        public FakeTerminalReader Reader { get; } = new FakeTerminalReader();

        public TerminalCapabilities Capabilities { get; }

        /// <summary>Called with the clock's time at the start of every delay.</summary>
        public Action<DateTime>? OnDelay { get; set; }

        /// <summary>How many delays are waiting to be released.</summary>
        public int PendingWaiterCount
        {
            get
            {
                lock (_gate)
                    return _waiters.Count;
            }
        }

        public DateTime UtcNow
        {
            get
            {
                lock (_gate)
                    return _utcNow;
            }
        }

        public TerminalCapabilities DetectCapabilities()
        {
            return Capabilities;
        }

        public ITerminalWriter CreateTerminalWriter()
        {
            return Writer;
        }

        public ITerminalReader CreateTerminalReader()
        {
            return Reader;
        }

        public Task Delay(TimeSpan interval, CancellationToken cancellationToken)
        {
            Waiter waiter;
            lock (_gate)
            {
                if (OnDelay != null)
                    OnDelay(_utcNow);

                if (cancellationToken.IsCancellationRequested)
                    return Task.CompletedTask;

                _sequence++;
                waiter = new Waiter(_utcNow + interval, _sequence, cancellationToken);
                _waiters.Add(waiter);
                ReleaseNextIfAllWaiting();
            }

            if (cancellationToken.CanBeCanceled)
            {
                var registration = cancellationToken.Register(() => ReleaseCancelled(waiter));
                lock (_gate)
                {
                    if (waiter.IsReleased)
                        registration.Unregister();
                    else
                        waiter.Registration = registration;
                }
            }

            return waiter.Completion.Task;
        }

        // Under the lock: once every participant is waiting, release the earliest waiter with the
        // clock moved to its wake time (never backwards). A waiter whose token is already
        // cancelled is due now, whether or not its cancellation callback has run yet — the
        // token turns at once while the callback runs off-thread — so a stop lands at the same
        // virtual instant however the threads interleave.
        private void ReleaseNextIfAllWaiting()
        {
            if (_waiters.Count < _participantCount)
                return;

            Waiter? next = null;
            var nextWakeTime = DateTime.MaxValue;
            foreach (var waiter in _waiters)
            {
                var wakeTime = waiter.WakeTime;
                if (waiter.CancellationToken.IsCancellationRequested)
                    wakeTime = _utcNow;

                if (next == null || wakeTime < nextWakeTime || (wakeTime == nextWakeTime && waiter.Sequence < next.Sequence))
                {
                    next = waiter;
                    nextWakeTime = wakeTime;
                }
            }

            if (next == null)
                return;

            if (nextWakeTime > _utcNow)
                _utcNow = nextWakeTime;

            Release(next);
        }

        private void ReleaseCancelled(Waiter waiter)
        {
            lock (_gate)
            {
                if (waiter.IsReleased)
                    return;

                Release(waiter);
            }
        }

        // Under the lock. Unregister never waits for a running callback, so it is safe here;
        // the completion's continuations run asynchronously, never inside the lock.
        private void Release(Waiter waiter)
        {
            _waiters.Remove(waiter);
            waiter.IsReleased = true;
            waiter.Registration.Unregister();
            waiter.Completion.TrySetResult();
        }

        private sealed class Waiter
        {
            public Waiter(DateTime wakeTime, long sequence, CancellationToken cancellationToken)
            {
                WakeTime = wakeTime;
                Sequence = sequence;
                CancellationToken = cancellationToken;
            }

            public DateTime WakeTime { get; }

            public long Sequence { get; }

            public CancellationToken CancellationToken { get; }

            public TaskCompletionSource Completion { get; } = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            public CancellationTokenRegistration Registration { get; set; }

            public bool IsReleased { get; set; }
        }
    }
}
