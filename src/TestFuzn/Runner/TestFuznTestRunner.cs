using Fuzn.TestFuzn.Contracts.Adapters;
using Fuzn.TestFuzn.Internals;
using Fuzn.TestFuzn.Internals.Terminal;

namespace Fuzn.TestFuzn.Runner;

internal class TestFuznTestRunner
{
    internal const string RunningTestLabel = "Running test:";

    private readonly ILiveViewHost _liveViewHost;

    internal TestFuznTestRunner(ILiveViewHost liveViewHost)
    {
        if (liveViewHost == null)
            throw new ArgumentNullException(nameof(liveViewHost), "Live view host cannot be null.");

        _liveViewHost = liveViewHost;
    }

    internal async Task RunTest<TStartup>(string[] args, ITestFrameworkAdapter testFramework,
        DiscoveredTest testInfo)
        where TStartup : IStartup, new()
    {
        if (testFramework == null)
            throw new ArgumentNullException(nameof(testFramework), "Test framework adapter cannot be null.");
        if (testInfo == null)
            throw new ArgumentNullException(nameof(testInfo), "Test info cannot be null.");
        if (testInfo.Name == null || testInfo.Class == null)
            throw new ArgumentException("The discovered test must carry its name and class.", nameof(testInfo));

        WriteStartupBanner(testInfo);

        var testClassInstance = Activator.CreateInstance(testInfo.Class);
        var iTestClassInstance = testClassInstance as ITest;
        if (iTestClassInstance == null)
            throw new Exception($"Test class '{testInfo.Class.Name}' must implement {nameof(ITest)} interface.");

        var testSession = new TestSession("default");
        TestSession.Default = testSession;
        try
        {
            await testSession.Init<TStartup>(testFramework);

            if (iTestClassInstance is IBeforeClass beforeClass)
            {
                var context = ContextFactory.CreateContext(testSession, testSession.ServiceProvider, testFramework, "BeforeClass");
                await beforeClass.BeforeClass(context);
            }

            iTestClassInstance.TestFramework = testFramework;
            iTestClassInstance.TestMethodInfo = testInfo.Method;

            var invocationResult = testFramework.ExecuteTestMethod(iTestClassInstance, testInfo.Method);

            if (invocationResult is Task task)
                await task;
        }
        finally
        {
            if (iTestClassInstance is IAfterClass afterClass)
            {
                var context = ContextFactory.CreateContext(testSession, testSession.ServiceProvider, testFramework, "AfterClass");
                await afterClass.AfterClass(context);
            }

            await testSession.Cleanup(testFramework);
        }
    }

    private void WriteStartupBanner(DiscoveredTest testInfo)
    {
        var capabilities = _liveViewHost.DetectCapabilities();

        var terminalWriter = _liveViewHost.CreateTerminalWriter();
        if (terminalWriter == null)
            throw new InvalidOperationException("The live view host returned no terminal writer.");

        StartupBanner.Write(terminalWriter, RunningTestLabel, testInfo.Name, capabilities.ColorMode);
    }
}
