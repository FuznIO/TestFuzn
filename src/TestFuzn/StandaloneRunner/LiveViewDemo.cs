using System.Globalization;
using System.Reflection;
using Fuzn.TestFuzn.Contracts.Adapters;
using Fuzn.TestFuzn.Internals;
using Fuzn.TestFuzn.Internals.ConsoleOutput;
using Fuzn.TestFuzn.Internals.Logger;
using Fuzn.TestFuzn.Internals.State;
using Fuzn.TestFuzn.Internals.Terminal;

namespace Fuzn.TestFuzn.StandaloneRunner;

/// <summary>
/// The standalone runner's live view demo, behind <c>run --demo</c>: the dev loop for iterating
/// on the live view's looks and the reviewable artifact for a human, runnable from any test
/// project that hosts the runner, with no Startup, plugins, sinks, configuration files or target
/// system. It runs the real <see cref="ConsoleManager"/> — the dashboard on a terminal that
/// supports the live view, the plain stats lines on a redirected or non-ANSI one, chosen by the
/// production host's real capability detection — over a real, initialized
/// <see cref="TestExecutionState"/> whose scenario's collector a <see cref="LiveViewDemoScript"/>
/// fills with a scripted synthetic load run of the duration the runner core was asked for
/// (<c>--demo-duration</c>, <see cref="LiveViewDemoScript.DefaultDuration"/> when none was
/// given), incident and declared thresholds and all. <see cref="Run"/> mirrors
/// <see cref="TestRunner"/>'s lifecycle step for step — init, execute, cleanup, then the console
/// manager's Complete with the summary in the normal buffer, the live view stopped in a finally
/// on every exit path — so the quit key and Ctrl+C stop the demo through the same cancellation
/// path a real run stops through, and end it the same way: the run completes its cleanup and
/// summary and then reports its cancellation, which the runner core prints and exits 1 on, as
/// it does for a stopped test. Only the reports are skipped: the demo has no results directory.
/// The demo opens with the same <see cref="StartupBanner"/> a test run opens with — its
/// scenario name as the subject and its duration on the detail line — written through the host's
/// terminal before the live view starts, so the two entry paths look alike in the scrollback.
/// </summary>
internal sealed class LiveViewDemo
{
    /// <summary>The banner's label on its title line, ahead of the demo scenario's name.</summary>
    internal const string RunningDemoLabel = "Running demo:";

    /// <summary>What the banner's detail line says the demo runs against, ahead of its duration.</summary>
    internal const string DemoDetailPrefix = "synthetic load, no target system · ";

    private readonly ILiveViewHost _liveViewHost;
    private readonly TimeSpan _duration;

    /// <summary>
    /// The execution state of the run in progress — or, once <see cref="Run"/> has returned, the
    /// disposed state it left behind, for tests to inspect what the run ended as; null before Run.
    /// </summary>
    internal TestExecutionState? TestExecutionState { get; private set; }

    /// <param name="liveViewHost">The clock, the terminal and the delays: the real console in production, a fake in tests.</param>
    /// <param name="duration">What the scripted run takes, init and cleanup included; at least <see cref="LiveViewDemoScript.MinimumDuration"/>.</param>
    internal LiveViewDemo(ILiveViewHost liveViewHost, TimeSpan duration)
    {
        if (liveViewHost == null)
            throw new ArgumentNullException(nameof(liveViewHost), "Live view host cannot be null.");
        if (duration < LiveViewDemoScript.MinimumDuration)
            throw new ArgumentOutOfRangeException(nameof(duration), duration, "The demo runs for at least " + LiveViewDemoScript.MinimumDuration.TotalSeconds + " seconds.");

        _liveViewHost = liveViewHost;
        _duration = duration;
    }

    /// <summary>
    /// The banner's detail line for a run of the given duration: what the demo runs against and
    /// how long its scripted load runs, so the scrollback records which demo was watched.
    /// </summary>
    internal static string DemoDetail(TimeSpan duration)
    {
        return DemoDetailPrefix + duration.TotalSeconds.ToString("0.#", CultureInfo.InvariantCulture) + " s";
    }

    /// <summary>
    /// Runs the demo to its end over the given framework adapter — the standalone adapter, whose
    /// Ctrl+C handler cancels the token the state links — and returns when the summary has been
    /// written. Throws <see cref="OperationCanceledException"/> after the summary when the run
    /// was stopped, as <see cref="TestRunner"/> does.
    /// </summary>
    public async Task Run(ITestFrameworkAdapter testFramework)
    {
        if (testFramework == null)
            throw new ArgumentNullException(nameof(testFramework), "Test framework adapter cannot be null.");

        WriteStartupBanner();

        var testSession = new TestSession("demo");
        var testExecutionState = new TestExecutionState(testSession);
        TestExecutionState = testExecutionState;
        var consoleManager = new ConsoleManager(testExecutionState, new ConsoleWriter(), _liveViewHost);

        try
        {
            testExecutionState.Init(testFramework, new DemoTest(), LiveViewDemoScript.CreateScenario());
            var script = new LiveViewDemoScript(testExecutionState, _liveViewHost, _duration);

            try
            {
                consoleManager.StartRealtimeConsoleOutputIfEnabled();
                await script.Init();
                await script.Execute();
            }
            catch (OperationCanceledException) when (testExecutionState.CancellationToken.IsCancellationRequested)
            {
                // A stop during init — fall through to cleanup, as the test runner does.
            }

            await script.Cleanup();
            await consoleManager.Complete();

            if (testExecutionState.CancellationToken.IsCancellationRequested)
                throw new OperationCanceledException(testExecutionState.CancellationToken);
        }
        finally
        {
            // Every exit path restores the terminal; a no-op when Complete already stopped the
            // live view or none was started.
            await consoleManager.StopRealtimeConsoleOutput();
            testExecutionState.Dispose();
        }
    }

    // As the test runner writes its banner: no size read (the layout is fixed), the capabilities
    // pick the color mode only, and a host without a terminal fails loud before the run starts.
    private void WriteStartupBanner()
    {
        var capabilities = _liveViewHost.DetectCapabilities();

        var terminalWriter = _liveViewHost.CreateTerminalWriter();
        if (terminalWriter == null)
            throw new InvalidOperationException("The live view host returned no terminal writer.");

        StartupBanner.Write(terminalWriter, RunningDemoLabel, LiveViewDemoScript.ScenarioName, DemoDetail(_duration), capabilities.ColorMode);
    }

    /// <summary>The test the demo runs as — the state's test result and the summary need one; no test method is ever invoked.</summary>
    private sealed class DemoTest : ITest
    {
        public object TestFramework { get; set; } = null!;

        public MethodInfo TestMethodInfo { get; set; } = null!;

        public TestInfo TestInfo { get; set; } = new TestInfo
        {
            Name = "Live view demo",
            FullName = "Fuzn.TestFuzn.StandaloneRunner.LiveViewDemo",
            Id = "live-view-demo"
        };
    }
}
