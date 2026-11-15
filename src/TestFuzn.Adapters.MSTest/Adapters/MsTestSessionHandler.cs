using Fuzn.TestFuzn.Contracts.Adapters;
using Fuzn.TestFuzn.Internals;
using Fuzn.TestFuzn.Runner;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Services;

namespace Fuzn.TestFuzn.Adapters;

/// <summary>
/// Runs the suite's Init before the first test and its Cleanup after the last test, under the MSTest runner.
/// <see cref="TestFuznHost"/> adds it to the test session.
/// </summary>
/// <remarks>
/// The Testing Platform calls session handlers only for test runs, so a test discovery (Test Explorer,
/// <c>--list-tests</c>) runs no suite hooks. A throw from a session handler crashes the test host, so
/// failures are reported here and fail the run through the exit code.
/// </remarks>
internal sealed class MsTestSessionHandler : ITestSessionLifetimeHandler
{
    internal const string NotHostedMessage =
        "TestFuzn is not initialized. TestFuznHost must run the tests. The Fuzn.TestFuzn.Adapters.MSTest package adds an " +
        "entry point that does this. If the project sets TestFuznGenerateEntryPoint to false, its Main must call " +
        "TestFuznHost.Run<Startup>(args).";

    private readonly Func<TestSession, ITestFrameworkAdapter, Task> _init;
    private TestSession? _session;
    private Exception? _initException;

    internal MsTestSessionHandler(Func<TestSession, ITestFrameworkAdapter, Task> init)
    {
        _init = init;
    }

    internal static MsTestSessionHandler? Active { get; private set; }

    internal bool Failed { get; private set; }

    internal static bool InitFailed => Active?._initException != null;

    public string Uid => "Fuzn.TestFuzn.Session";
    public string Version => typeof(MsTestSessionHandler).Assembly.GetName().Version?.ToString() ?? "1.0.0";
    public string DisplayName => "TestFuzn";
    public string Description => "Initializes TestFuzn before the first test and cleans it up after the last test.";

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public async Task OnTestSessionStartingAsync(ITestSessionContext testSessionContext)
    {
        Active = this;
        _session = null;
        _initException = null;

        var session = new TestSession("default");
        TestSession.Default = session;
        try
        {
            // MSTest sets the test exe's folder as the current folder, but only after this handler runs.
            // Init and the suite hooks read files relative to it, appsettings*.json for one.
            Directory.SetCurrentDirectory(BaseTestFuznRunnerAdapter.DefaultTestResultsDirectory);


            await _init(session, MsTestRunnerAdapter.ForSession(testSessionContext.CancellationToken));
            _session = session;
        }
        catch (Exception ex)
        {
            // The tests still run. Each test class then fails with this exception, as with a failed [AssemblyInitialize].
            _initException = ex;
            Failed = true;
            Console.Error.WriteLine($"TestFuzn failed to initialize: {ex}");
        }
    }

    /// <summary>
    /// Throws if the suite is not initialized. Test classes call this before their first test.
    /// </summary>
    internal static void EnsureInitialized()
    {
        var handler = Active;
        if (handler == null)
            throw new InvalidOperationException(NotHostedMessage);

        if (handler._initException != null)
            throw new InvalidOperationException("TestFuzn failed to initialize. See the inner exception.", handler._initException);
    }

    public async Task OnTestSessionFinishingAsync(ITestSessionContext testSessionContext)
    {
        Active = null;

        var session = _session;
        _session = null;
        if (session == null)
            return;

        try
        {
            await session.Cleanup(MsTestRunnerAdapter.ForSession(testSessionContext.CancellationToken));
        }
        catch (Exception ex)
        {
            Failed = true;
            Console.Error.WriteLine($"TestFuzn cleanup failed: {ex}");
        }
    }
}
