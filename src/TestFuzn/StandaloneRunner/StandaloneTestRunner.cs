using Fuzn.TestFuzn.Contracts.Adapters;
using Fuzn.TestFuzn.Internals;
using Fuzn.TestFuzn.Internals.Terminal;

namespace Fuzn.TestFuzn.StandaloneRunner;

/// <summary>
/// Runs one discovered test in the standalone runner over a framework adapter: the startup
/// banner first, then the test class instance, the session's init, the class hooks, the test
/// method and the session's cleanup. The banner (<see cref="StartupBanner"/>) is the first
/// thing written and goes to the normal screen buffer through the live view host's terminal —
/// before the session initializes, and so before the live view enters the alternate screen
/// (the console manager enters it once the run's init has started) — so the test's full name
/// and its context stay in the scrollback above the live view and the summary, on the
/// direct-run path and after the selection menu has restored the terminal alike. It is written
/// on every output — styled on an ANSI terminal, as plain lines with no escape byte on a
/// redirected or non-ANSI one — and never reads the terminal's size or creates a key reader.
/// The terminal comes from an <see cref="ILiveViewHost"/> and the environment from an
/// <see cref="IEnvironmentWrapper"/>: the real console and process environment in production,
/// fakes in tests.
/// </summary>
internal class StandaloneTestRunner
{
    /// <summary>The banner's label on its title line, ahead of the test's full name.</summary>
    internal const string RunningTestLabel = "Running test:";

    /// <summary>What the banner's detail line shows for a value that is not set — the target environment, or an assembly without a name — as the reports show an unset value.</summary>
    internal const string UnsetValueText = "-";

    private readonly ILiveViewHost _liveViewHost;
    private readonly IEnvironmentWrapper _environmentWrapper;

    /// <param name="liveViewHost">The host the startup banner is written through: the real console in production, a fake in tests.</param>
    internal StandaloneTestRunner(ILiveViewHost liveViewHost)
        : this(liveViewHost, new EnvironmentWrapper())
    {
    }

    /// <param name="liveViewHost">The host the startup banner is written through: the real console in production, a fake in tests.</param>
    /// <param name="environmentWrapper">The environment the banner's target environment is read from.</param>
    internal StandaloneTestRunner(ILiveViewHost liveViewHost, IEnvironmentWrapper environmentWrapper)
    {
        if (liveViewHost == null)
            throw new ArgumentNullException(nameof(liveViewHost), "Live view host cannot be null.");
        if (environmentWrapper == null)
            throw new ArgumentNullException(nameof(environmentWrapper), "Environment wrapper cannot be null.");

        _liveViewHost = liveViewHost;
        _environmentWrapper = environmentWrapper;
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

        // A class that could not be instantiated is caught here too: the instance is then not
        // an ITest either.
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

    // The banner needs no terminal size — its layout is fixed — so nothing is gated here: the
    // capabilities only pick the color mode, and a host without a terminal fails loud before
    // anything of the run has started.
    private void WriteStartupBanner(DiscoveredTest testInfo)
    {
        var capabilities = _liveViewHost.DetectCapabilities();

        var terminalWriter = _liveViewHost.CreateTerminalWriter();
        if (terminalWriter == null)
            throw new InvalidOperationException("The live view host returned no terminal writer.");

        StartupBanner.Write(terminalWriter, RunningTestLabel, testInfo.Name, FormatDetailLine(testInfo.Class.Assembly.GetName().Name, ReadTargetEnvironment()), capabilities.ColorMode);
    }

    // The target environment the session is about to initialize with. The session is given no
    // arguments here (Init reads the environment variable alone), so the banner makes the same
    // lookup over the same inputs: announcing a --target-environment value from the command
    // line would name an environment the run does not target.
    private string ReadTargetEnvironment()
    {
        return new ArgumentsParser(_environmentWrapper).GetValueFromArgsOrEnvironmentVariable(null, TestSession.TargetEnvironmentArgument, TestSession.TargetEnvironmentVariable);
    }

    /// <summary>
    /// The banner's detail line, "Assembly: {name} · Target environment: {name}": the assembly
    /// the test lives in and the target environment the run is about to initialize with, either
    /// <see cref="UnsetValueText"/> when it is not set. Deliberately labeled as the assembly and
    /// not as the suite: the suite's name is the Startup's assembly, assigned by the session's
    /// init and open to the Startup to rename — neither is known before the run, and a banner
    /// that claimed the suite name could contradict the reports. The banner is decoration, so
    /// a missing value never aborts the run.
    /// </summary>
    internal static string FormatDetailLine(string? assemblyName, string? targetEnvironment)
    {
        if (string.IsNullOrEmpty(assemblyName))
            assemblyName = UnsetValueText;

        if (string.IsNullOrEmpty(targetEnvironment))
            targetEnvironment = UnsetValueText;

        return "Assembly: " + assemblyName + " · Target environment: " + targetEnvironment;
    }
}
