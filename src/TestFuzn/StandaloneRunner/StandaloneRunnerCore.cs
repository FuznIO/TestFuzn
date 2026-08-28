using Fuzn.TestFuzn.Contracts.Adapters;
using Fuzn.TestFuzn.Internals.Terminal;
using Spectre.Console;
using System.Reflection;
using System.Text;

namespace Fuzn.TestFuzn.StandaloneRunner;

internal class StandaloneRunnerCore
{
    /// <summary>The bare flag that runs the live view demo instead of a test: <c>run --demo</c>.</summary>
    internal const string DemoFlag = "demo";

    /// <summary>The argument that selects the test to run: <c>--test-name=&lt;FullyQualifiedName&gt;</c>.</summary>
    internal const string TestNameArgument = "test-name";

    /// <summary>The invocation error for a <c>--test-name</c> given without a value — bare, or with a space instead of <c>=</c>.</summary>
    internal const string TestNameUsage = "--test-name requires a value: --test-name=<FullyQualifiedName>";

    private readonly ILiveViewHost _liveViewHost;

    public StandaloneRunnerCore()
        : this(new ConsoleLiveViewHost())
    {
    }

    /// <param name="liveViewHost">The host the test selection menu and the live view demo run over: the real console and clock in production, a fake in tests.</param>
    internal StandaloneRunnerCore(ILiveViewHost liveViewHost)
    {
        if (liveViewHost == null)
            throw new ArgumentNullException(nameof(liveViewHost), "Live view host cannot be null.");

        _liveViewHost = liveViewHost;
    }

    public async Task<int> Run<TStartup>(Assembly testAssembly,
        string[] args, Func<ITestFrameworkAdapter> testFrameworkInstanceCreator)
        where TStartup : IStartup, new()
    {
        var argumentsParser = new ArgumentsParser(new EnvironmentWrapper());
        var parsedArgs = argumentsParser.Parse(args);

        Console.OutputEncoding = Encoding.UTF8;

        // The live view demo needs no test, Startup or target system: it plays a scripted load
        // run through the same adapter and exit-code path a test takes.
        if (ArgumentsParser.HasFlag(parsedArgs, DemoFlag))
            return await RunWithAdapter(testFrameworkInstanceCreator, adapter => new LiveViewDemo(_liveViewHost).Run(adapter));

        // A --test-name without a value — bare, or with a space instead of = — parses as a bare
        // flag: an invocation error, reported before any test is discovered or the menu is
        // shown, never a test named "true".
        if (parsedArgs.TryGetValue(TestNameArgument, out var testNameValue) && testNameValue == ArgumentsParser.FlagValue)
            return WriteInvocationError(testFrameworkInstanceCreator, TestNameUsage);

        var tests = new DiscoverTests().GetTests(testAssembly);

        var testName = argumentsParser.GetValueFromArgsOrEnvironmentVariable(parsedArgs, TestNameArgument, "TESTFUZN_TEST_NAME");

        if (string.IsNullOrEmpty(testName))
        {
            // No test named: the selection menu picks one. The menu runs over the same adapter
            // the picked test then runs on — the adapter's Ctrl+C handler cancels the token the
            // menu polls, and a second adapter would register a second handler. A quit, by
            // Escape, an empty line or Ctrl+C, completes with nothing run: exit code 0.
            return await RunWithAdapter(testFrameworkInstanceCreator, async adapter =>
            {
                var selectedTest = await new TestSelectionMenu(_liveViewHost).SelectTest(tests, adapter.CancellationToken);
                if (selectedTest == null)
                    return;

                await new StandaloneTestRunner().RunTest<TStartup>(args, adapter, selectedTest);
            });
        }

        var testInfo = tests.SingleOrDefault(t => t.Name == testName);
        if (testInfo == null)
        {
            Console.WriteLine($"Test '{testName}' not found.");
            return 1;
        }

        return await RunWithAdapter(testFrameworkInstanceCreator, adapter => new StandaloneTestRunner().RunTest<TStartup>(args, adapter, testInfo));
    }

    /// <summary>
    /// Runs the given work over a fresh framework adapter and turns its outcome into the process
    /// exit code: 0 when it completes or the test is skipped, 1 when it throws — the exception
    /// printed. The adapter is disposed afterwards.
    /// </summary>
    private static async Task<int> RunWithAdapter(Func<ITestFrameworkAdapter> testFrameworkInstanceCreator, Func<ITestFrameworkAdapter, Task> run)
    {
        var adapter = testFrameworkInstanceCreator();
        try
        {
            await run(adapter);
            return 0;
        }
        catch (Exception ex) when (IsScenarioRunModeIgnore(ex))
        {
            AnsiConsole.MarkupLine("[yellow]Test skipped.[/]");
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex);
            return 1;
        }
        finally
        {
            if (adapter is IDisposable disposable)
                disposable.Dispose();
        }
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
