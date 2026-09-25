using Fuzn.TestFuzn.Contracts.Adapters;
using Fuzn.TestFuzn.Internals.Terminal;
using System.Reflection;
using System.Text;

namespace Fuzn.TestFuzn.Runner;

internal class TestFuznRunnerCore
{
    internal const string TestNameArgument = "test-name";

    internal const string TestNameUsage = "--test-name requires a value: --test-name=<FullyQualifiedName>";

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

        if (parsedArgs.TryGetValue(TestNameArgument, out var testNameValue) && testNameValue == ArgumentsParser.FlagValue)
            return WriteInvocationError(testFrameworkInstanceCreator, TestNameUsage);

        var tests = _discoverTests.GetTests(testAssembly);

        var testName = argumentsParser.GetValueFromArgsOrEnvironmentVariable(parsedArgs, TestNameArgument, "TESTFUZN_TEST_NAME");

        if (string.IsNullOrEmpty(testName))
        {
            return await RunWithAdapter(testFrameworkInstanceCreator, async adapter =>
            {
                var selectedTest = await new TestSelectionMenu(_liveViewHost).SelectTest(tests, adapter.CancellationToken);
                if (selectedTest == null)
                    return;

                await new TestFuznTestRunner(_liveViewHost).RunTest<TStartup>(args, adapter, selectedTest);
            });
        }

        var testInfo = tests.SingleOrDefault(t => t.Name == testName);
        if (testInfo == null)
        {
            Console.WriteLine($"Test '{testName}' not found.");
            return 1;
        }

        return await RunWithAdapter(testFrameworkInstanceCreator, adapter => new TestFuznTestRunner(_liveViewHost).RunTest<TStartup>(args, adapter, testInfo));
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
