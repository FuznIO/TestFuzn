using Fuzn.TestFuzn.Contracts.Adapters;
using Fuzn.TestFuzn.Internals.Terminal;
using System.Reflection;
using System.Text;

namespace Fuzn.TestFuzn.StandaloneRunner;

internal class StandaloneRunnerCore
{
    /// <summary>The bare flag that runs the live view demo instead of a test: <c>run --demo</c>.</summary>
    internal const string DemoFlag = "demo";

    /// <summary>The argument that sets how long the demo runs: <c>--demo-duration=&lt;seconds&gt;</c>.</summary>
    internal const string DemoDurationArgument = "demo-duration";

    /// <summary>The invocation error for a <c>--demo-duration</c> that is not a whole number of seconds of at least the demo's minimum.</summary>
    internal static readonly string DemoDurationUsage = "--demo-duration takes whole seconds, at least " + (int)LiveViewDemoScript.MinimumDuration.TotalSeconds + ": --demo-duration=<seconds>";

    /// <summary>The argument that selects the test to run: <c>--test-name=&lt;FullyQualifiedName&gt;</c>.</summary>
    internal const string TestNameArgument = "test-name";

    /// <summary>The invocation error for a <c>--test-name</c> given without a value — bare, or with a space instead of <c>=</c>.</summary>
    internal const string TestNameUsage = "--test-name requires a value: --test-name=<FullyQualifiedName>";

    /// <summary>The line written when the test asked to be skipped; exit code 0.</summary>
    internal const string TestSkippedMessage = "Test skipped.";

    /// <summary>
    /// The line written, in place of an exception trace, when a run ended because it was
    /// stopped — the cancellation the test runner reports after a Ctrl+C or the quit key, once
    /// cleanup and the summary are done; exit code 1, as for any run that did not complete.
    /// </summary>
    internal const string RunStoppedMessage = "Run stopped (OperationCanceledException): the run was cancelled by Ctrl+C or the quit key before it completed.";

    private const string TestSkippedMarkup = "[" + TerminalPalette.WarningStyle + "]" + TestSkippedMessage + "[/]";
    private const string RunStoppedMarkup = "[" + TerminalPalette.WarningStyle + "]" + RunStoppedMessage + "[/]";

    private readonly ILiveViewHost _liveViewHost;
    private readonly DiscoverTests _discoverTests;
    private readonly Func<TimeSpan, LiveViewDemo> _demoFactory;

    public StandaloneRunnerCore()
        : this(new ConsoleLiveViewHost())
    {
    }

    /// <param name="liveViewHost">The host the test selection menu, the startup banner, the live view demo and a failed run's exception run over: the real console and clock in production, a fake in tests.</param>
    internal StandaloneRunnerCore(ILiveViewHost liveViewHost)
        : this(liveViewHost, new DiscoverTests())
    {
    }

    /// <param name="liveViewHost">The host the test selection menu, the startup banner, the live view demo and a failed run's exception run over: the real console and clock in production, a fake in tests.</param>
    /// <param name="discoverTests">The discovery of the assembly's tests: reflection in production, hand-made tests in unit tests.</param>
    internal StandaloneRunnerCore(ILiveViewHost liveViewHost, DiscoverTests discoverTests)
        : this(liveViewHost, discoverTests, null)
    {
    }

    /// <param name="liveViewHost">The host the test selection menu, the startup banner, the live view demo and a failed run's exception run over: the real console and clock in production, a fake in tests.</param>
    /// <param name="discoverTests">The discovery of the assembly's tests: reflection in production, hand-made tests in unit tests.</param>
    /// <param name="demoFactory">
    /// Builds the demo the <see cref="DemoFlag"/> runs, from the duration the command line asked
    /// for; null for the production one, over this core's host. A test hands in a demo whose
    /// thresholds the scripted run cannot hold, to drive the exit code a violated verdict earns.
    /// </param>
    internal StandaloneRunnerCore(ILiveViewHost liveViewHost, DiscoverTests discoverTests, Func<TimeSpan, LiveViewDemo>? demoFactory)
    {
        if (liveViewHost == null)
            throw new ArgumentNullException(nameof(liveViewHost), "Live view host cannot be null.");
        if (discoverTests == null)
            throw new ArgumentNullException(nameof(discoverTests), "Test discovery cannot be null.");

        _liveViewHost = liveViewHost;
        _discoverTests = discoverTests;

        if (demoFactory == null)
            demoFactory = duration => new LiveViewDemo(liveViewHost, duration);

        _demoFactory = demoFactory;
    }

    public async Task<int> Run<TStartup>(Assembly testAssembly,
        string[] args, Func<ITestFrameworkAdapter> testFrameworkInstanceCreator)
        where TStartup : IStartup, new()
    {
        var argumentsParser = new ArgumentsParser(new EnvironmentWrapper());
        var parsedArgs = argumentsParser.Parse(args);

        Console.OutputEncoding = Encoding.UTF8;

        // The live view demo needs no test, Startup or target system: it plays a scripted load
        // run of --demo-duration seconds (the script's default when none is given) through the
        // same adapter and exit-code path a test takes. A duration that is not whole seconds of
        // at least the script's minimum is an invocation error, reported as a bare --test-name is.
        if (ArgumentsParser.HasFlag(parsedArgs, DemoFlag))
        {
            if (!ArgumentsParser.TryGetDuration(parsedArgs, DemoDurationArgument, LiveViewDemoScript.MinimumDuration, LiveViewDemoScript.DefaultDuration, out var demoDuration))
                return WriteInvocationError(testFrameworkInstanceCreator, DemoDurationUsage);

            return await RunWithAdapter(testFrameworkInstanceCreator, adapter => _demoFactory(demoDuration).Run(adapter));
        }

        // A --test-name without a value — bare, or with a space instead of = — parses as a bare
        // flag: an invocation error, reported before any test is discovered or the menu is
        // shown, never a test named "true".
        if (parsedArgs.TryGetValue(TestNameArgument, out var testNameValue) && testNameValue == ArgumentsParser.FlagValue)
            return WriteInvocationError(testFrameworkInstanceCreator, TestNameUsage);

        var tests = _discoverTests.GetTests(testAssembly);

        var testName = argumentsParser.GetValueFromArgsOrEnvironmentVariable(parsedArgs, TestNameArgument, "TESTFUZN_TEST_NAME");

        if (string.IsNullOrEmpty(testName))
        {
            // No test named: the selection menu picks one. The menu runs over the same adapter
            // the picked test then runs on — the adapter's Ctrl+C handler cancels the token the
            // menu polls, and a second adapter would register a second handler. A quit, by
            // Escape, an empty line or Ctrl+C, completes with nothing run: exit code 0. The
            // picked test's startup banner goes through the same host, after the menu has
            // restored the terminal, so it lands on the normal screen buffer.
            return await RunWithAdapter(testFrameworkInstanceCreator, async adapter =>
            {
                var selectedTest = await new TestSelectionMenu(_liveViewHost).SelectTest(tests, adapter.CancellationToken);
                if (selectedTest == null)
                    return;

                await new StandaloneTestRunner(_liveViewHost).RunTest<TStartup>(args, adapter, selectedTest);
            });
        }

        // A name that names no test is reported before any adapter or banner: the banner is
        // written only once the test is resolved.
        var testInfo = tests.SingleOrDefault(t => t.Name == testName);
        if (testInfo == null)
        {
            Console.WriteLine($"Test '{testName}' not found.");
            return 1;
        }

        return await RunWithAdapter(testFrameworkInstanceCreator, adapter => new StandaloneTestRunner(_liveViewHost).RunTest<TStartup>(args, adapter, testInfo));
    }

    /// <summary>
    /// Runs the given work over a fresh framework adapter and turns its outcome into the process
    /// exit code: 0 when it completes or the test is skipped (the skip written through the
    /// adapter), 1 when it throws — the exception written through the host's terminal by
    /// <see cref="ExceptionRenderer"/>, or, for the cancellation a stopped run reports, as the
    /// single <see cref="RunStoppedMessage"/> line: a stop is not a failure to trace. The
    /// adapter is disposed afterwards.
    /// </summary>
    private async Task<int> RunWithAdapter(Func<ITestFrameworkAdapter> testFrameworkInstanceCreator, Func<ITestFrameworkAdapter, Task> run)
    {
        var adapter = testFrameworkInstanceCreator();
        try
        {
            await run(adapter);
            return 0;
        }
        catch (Exception ex) when (IsScenarioRunModeIgnore(ex))
        {
            adapter.WriteMarkup(TestSkippedMarkup);
            return 0;
        }
        catch (Exception ex)
        {
            WriteRunFailure(ex);
            return 1;
        }
        finally
        {
            if (adapter is IDisposable disposable)
                disposable.Dispose();
        }
    }

    // The failure goes to the normal screen buffer through the host's terminal — the live view
    // has been left by now — styled on an ANSI terminal, plain with no escape byte otherwise,
    // and never truncated: the trace is what the reader copies from the scrollback. The
    // terminal's size is never read and no key reader is created, so a redirected output is fine.
    private void WriteRunFailure(Exception exception)
    {
        var capabilities = _liveViewHost.DetectCapabilities();

        var terminalWriter = _liveViewHost.CreateTerminalWriter();
        if (terminalWriter == null)
            throw new InvalidOperationException("The live view host returned no terminal writer.");

        if (IsRunCancellation(exception))
        {
            terminalWriter.Write(MarkupText.RenderTruncated(RunStoppedMarkup, int.MaxValue, capabilities.ColorMode).Text + Environment.NewLine);
            return;
        }

        ExceptionRenderer.Write(terminalWriter, exception, capabilities.ColorMode);
    }

    /// <summary>
    /// Whether the exception is the cancellation a stopped run reports: exactly an
    /// <see cref="OperationCanceledException"/> — the test runner and the demo throw the base
    /// type once cleanup and the summary are done — carrying a cancelled token. A derived
    /// cancellation from inside a test (an HTTP timeout's <see cref="TaskCanceledException"/>,
    /// say) is a failure of the run and gets the full trace.
    /// </summary>
    internal static bool IsRunCancellation(Exception exception)
    {
        if (exception == null)
            throw new ArgumentNullException(nameof(exception), "Exception cannot be null.");

        return exception.GetType() == typeof(OperationCanceledException)
            && ((OperationCanceledException)exception).CancellationToken.IsCancellationRequested;
    }

    /// <summary>
    /// Reports an invocation error through a fresh framework adapter — the way the run's own
    /// messages are written — and yields the failure exit code 1. The adapter is disposed afterwards.
    /// </summary>
    private static int WriteInvocationError(Func<ITestFrameworkAdapter> testFrameworkInstanceCreator, string message)
    {
        var adapter = testFrameworkInstanceCreator();
        try
        {
            adapter.WriteMarkup("[red]" + message + "[/]");
            return 1;
        }
        finally
        {
            if (adapter is IDisposable disposable)
                disposable.Dispose();
        }
    }

    private static bool IsScenarioRunModeIgnore(Exception ex)
    {
        if (ex is ScenarioRunModeIgnoreException)
            return true;

        if (ex is TargetInvocationException invocationException && invocationException.InnerException is ScenarioRunModeIgnoreException)
            return true;

        return false;
    }
}
