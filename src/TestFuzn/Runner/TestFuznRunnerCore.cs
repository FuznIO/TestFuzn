using Fuzn.TestFuzn.Contracts.Adapters;
using Fuzn.TestFuzn.Internals.Cluster;
using Fuzn.TestFuzn.Internals.Cluster.LoadChild;
using Fuzn.TestFuzn.Internals.Terminal;
using System.Reflection;
using System.Text;

namespace Fuzn.TestFuzn.Runner;

internal class TestFuznRunnerCore
{
    internal const string TestNameArgument = "test-name";

    internal const string TestNameUsage = "--test-name requires a value: --test-name=<FullyQualifiedName>";

    internal static readonly string[] HelpArguments = ["--help", "-h", "-?"];

    internal static readonly string[] HelpLines =
    [
        "TestFuzn runner",
        "",
        "Usage:",
        "  dotnet run --project <test project> -- --runner=testfuzn <options>",
        "  <test exe> --runner=testfuzn <options>",
        "",
        "Options:",
        "  --test-name=<name>              Run one test by its full name. Without it, a picker opens.",
        "                                  Env: TESTFUZN_TEST_NAME",
        "  --target-environment=<name>     The system under test, e.g. dev, staging.",
        "                                  Env: TESTFUZN_TARGET_ENVIRONMENT",
        "  --execution-environment=<name>  Where the tests run, e.g. local, ci.",
        "                                  Env: TESTFUZN_EXECUTION_ENVIRONMENT",
        "  --keep-last-n-runs=<n>          How many old result folders to keep.",
        "                                  Env: TESTFUZN_KEEP_LAST_N_RUNS",
        "  --cluster                       Run the test on a cluster through Cockpit. Selects this runner.",
        "                                  Env: TESTFUZN_CLUSTER=true",
        "  --run-name=<name>               A name for the cluster run on Cockpit's cluster page.",
        "  --cockpit-source=<name>         Cockpit's name for the kind of run, e.g. nightly-load. Sends the",
        "                                  report to Cockpit. A cluster run needs one.",
        "                                  Env: TESTFUZN_COCKPIT_SOURCE",
        "  --runner=<testfuzn|mstest>      The runner to use. The default is mstest.",
        "  --help, -h, -?                  Show this help.",
        "",
        "Values take '=': --test-name=<name>, not --test-name <name>.",
        "For the MSTest runner's options: --runner=mstest --help"
    ];

    internal const string TestSkippedMessage = "Test skipped.";

    internal const string RunStoppedMessage = "Run stopped (OperationCanceledException): the run was cancelled by Ctrl+C or the quit key before it completed.";

    private const string TestSkippedMarkup = "[" + LiveDashboardLayout.WarningStyle + "]" + TestSkippedMessage + "[/]";
    private const string RunStoppedMarkup = "[" + LiveDashboardLayout.WarningStyle + "]" + RunStoppedMessage + "[/]";

    private readonly ILiveViewHost _liveViewHost;
    private readonly DiscoverTests _discoverTests;

    public TestFuznRunnerCore()
        : this(new ConsoleLiveViewHost())
    {
    }

    internal TestFuznRunnerCore(ILiveViewHost liveViewHost)
        : this(liveViewHost, new DiscoverTests())
    {
    }

    internal TestFuznRunnerCore(ILiveViewHost liveViewHost, DiscoverTests discoverTests)
    {
        if (liveViewHost == null)
            throw new ArgumentNullException(nameof(liveViewHost), "Live view host cannot be null.");
        if (discoverTests == null)
            throw new ArgumentNullException(nameof(discoverTests), "Test discovery cannot be null.");

        _liveViewHost = liveViewHost;
        _discoverTests = discoverTests;
    }

    public async Task<int> Run<TStartup>(Assembly testAssembly,
        string[] args, Func<ITestFrameworkAdapter> testFrameworkInstanceCreator)
        where TStartup : IStartup, new()
    {
        var argumentsParser = new ArgumentsParser(new EnvironmentWrapper());
        var parsedArgs = argumentsParser.Parse(args);

        Console.OutputEncoding = Encoding.UTF8;

        if (IsHelpRequested(args))
            return WriteThroughAdapter(testFrameworkInstanceCreator, HelpLines.Select(MarkupParser.Escape), 0);

        if (parsedArgs.TryGetValue(TestNameArgument, out var testNameValue) && testNameValue == ArgumentsParser.FlagValue)
            return WriteThroughAdapter(testFrameworkInstanceCreator, ["[red]" + TestNameUsage + "[/]"], 1);

        var tests = _discoverTests.GetTests(testAssembly);

        if (parsedArgs.TryGetValue(LoadChildProtocol.RunArgument, out var runFilePath))
            return await RunLoadChild<TStartup>(runFilePath, args, tests, testFrameworkInstanceCreator);

        if (parsedArgs.TryGetValue(LoadChildProtocol.ControllerArgument, out var controllerFilePath))
            return await RunController<TStartup>(controllerFilePath, args, tests, testFrameworkInstanceCreator);

        // The remote client runs no suite or class hooks: the workers run them.
        ClusterSessionInfo? cluster = null;
        var remote = argumentsParser.GetValueFromArgsOrEnvironmentVariable(parsedArgs, ClusterSessionInfo.ClusterArgument, ClusterSessionInfo.ClusterVariable);
        if (string.Equals(remote, ArgumentsParser.FlagValue, StringComparison.OrdinalIgnoreCase))
            cluster = new ClusterSessionInfo { SkipSuiteHooks = true };

        var testName = argumentsParser.GetValueFromArgsOrEnvironmentVariable(parsedArgs, TestNameArgument, "TESTFUZN_TEST_NAME");

        if (string.IsNullOrEmpty(testName))
        {
            return await RunWithAdapter(testFrameworkInstanceCreator, async adapter =>
            {
                var selectedTest = await new TestSelectionMenu(_liveViewHost).SelectTest(tests, adapter.CancellationToken);
                if (selectedTest == null)
                    return;

                await new TestFuznTestRunner(_liveViewHost).RunTest<TStartup>(args, adapter, selectedTest, cluster);
            });
        }

        var testInfo = tests.SingleOrDefault(t => t.Name == testName);
        if (testInfo == null)
        {
            Console.WriteLine($"Test '{testName}' not found.");
            return 1;
        }

        return await RunWithAdapter(testFrameworkInstanceCreator, adapter => new TestFuznTestRunner(_liveViewHost).RunTest<TStartup>(args, adapter, testInfo, cluster));
    }

    // Started by a worker app: runs this worker's part of the load and reports through status.json.
    private async Task<int> RunLoadChild<TStartup>(string runFilePath, string[] args, List<DiscoveredTest> tests,
        Func<ITestFrameworkAdapter> testFrameworkInstanceCreator)
        where TStartup : IStartup, new()
    {
        using var coordinator = LoadChildCoordinator.FromRunFile(runFilePath, Console.In);
        coordinator.Start();

        var settings = coordinator.Settings;
        var cluster = new ClusterSessionInfo
        {
            Role = ClusterRole.LoadChild,
            RunId = settings.RunId,
            WorkerIndex = settings.WorkerIndex,
            WorkerCount = settings.WorkerCount,
            Seed = settings.Seed,
            LoadChild = coordinator
        };
        if (settings.OnceData != null)
        {
            foreach (var onceData in settings.OnceData)
                cluster.OnceData[onceData.Key] = onceData.Value.GetRawText();
        }

        Exception? failure = null;
        var exitCode = await RunWithAdapter(testFrameworkInstanceCreator, async adapter =>
        {
            try
            {
                var testInfo = FindTest(tests, settings.TestName);
                await new TestFuznTestRunner(_liveViewHost).RunTest<TStartup>(WithTargetEnvironment(args, settings.TargetEnvironment), adapter, testInfo, cluster);
            }
            catch (Exception ex)
            {
                failure = ex;
                throw;
            }
        });

        coordinator.Finish(failure);
        return exitCode;
    }

    // Started by a worker app: coordinates the load workers. The cluster package reads the rest of controller.json.
    private async Task<int> RunController<TStartup>(string controllerFilePath, string[] args, List<DiscoveredTest> tests,
        Func<ITestFrameworkAdapter> testFrameworkInstanceCreator)
        where TStartup : IStartup, new()
    {
        if (!File.Exists(controllerFilePath))
        {
            Console.WriteLine($"The controller file '{controllerFilePath}' does not exist.");
            return 1;
        }

        var settings = ClusterJson.Deserialize<ControllerLaunchSettings>(File.ReadAllText(controllerFilePath));
        var cluster = new ClusterSessionInfo
        {
            Role = ClusterRole.Controller,
            SkipSuiteHooks = true,
            RunId = settings.RunId,
            WorkerCount = Math.Max(1, settings.Workers)
        };

        return await RunWithAdapter(testFrameworkInstanceCreator, adapter =>
            new TestFuznTestRunner(_liveViewHost).RunTest<TStartup>(WithTargetEnvironment(args, settings.TargetEnvironment), adapter, FindTest(tests, settings.TestName), cluster));
    }

    private static DiscoveredTest FindTest(List<DiscoveredTest> tests, string testName)
    {
        var testInfo = tests.SingleOrDefault(t => t.Name == testName);
        if (testInfo == null)
            throw new InvalidOperationException($"Test '{testName}' was not found in the uploaded build.");

        return testInfo;
    }

    // The target environment belongs to the run, so it comes from the run's settings, not the worker machine.
    private static string[] WithTargetEnvironment(string[] args, string? targetEnvironment)
    {
        if (string.IsNullOrEmpty(targetEnvironment))
            return args;

        return args.Append("--" + Internals.TestSession.TargetEnvironmentArgument + "=" + targetEnvironment).ToArray();
    }

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

    internal static bool IsRunCancellation(Exception exception)
    {
        if (exception == null)
            throw new ArgumentNullException(nameof(exception), "Exception cannot be null.");

        return exception.GetType() == typeof(OperationCanceledException)
            && ((OperationCanceledException)exception).CancellationToken.IsCancellationRequested;
    }

    internal static bool IsHelpRequested(string[] args)
    {
        return args.Any(arg => HelpArguments.Contains(arg, StringComparer.OrdinalIgnoreCase));
    }

    private static int WriteThroughAdapter(Func<ITestFrameworkAdapter> testFrameworkInstanceCreator, IEnumerable<string> markupLines, int exitCode)
    {
        var adapter = testFrameworkInstanceCreator();
        try
        {
            foreach (var line in markupLines)
                adapter.WriteMarkup(line);
            return exitCode;
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
