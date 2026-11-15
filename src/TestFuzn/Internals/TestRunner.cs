using Fuzn.TestFuzn.Contracts;
using Fuzn.TestFuzn.Contracts.Adapters;
using Fuzn.TestFuzn.Internals.Cleanup;
using Fuzn.TestFuzn.Internals.Cluster;
using Fuzn.TestFuzn.Internals.Execution;
using Fuzn.TestFuzn.Internals.Init;
using Fuzn.TestFuzn.Internals.Logger;
using Fuzn.TestFuzn.Internals.Reports.Load;
using Fuzn.TestFuzn.Internals.State;
using System.Runtime.ExceptionServices;

namespace Fuzn.TestFuzn.Internals;

internal class TestRunner
{

    private readonly IServiceProvider _serviceProvider;
    private readonly TestExecutionState _testExecutionState;
    private readonly ConsoleManager _consoleManager;
    private readonly InitManager _initManager;
    private readonly ExecutionManager _executionManager;
    private readonly CleanupManager _cleanupManager;
    private readonly LoadReportManager _loadReportManager;

    public TestRunner(
        IServiceProvider serviceProvider,
        TestExecutionState testExecutionState,
        ConsoleManager consoleManager,
        InitManager initManager,
        ExecutionManager executionManager,
        CleanupManager cleanupManager,
        LoadReportManager loadReportManager)
    {
        _serviceProvider = serviceProvider;
        _testExecutionState = testExecutionState;
        _consoleManager = consoleManager;
        _initManager = initManager;
        _executionManager = executionManager;
        _cleanupManager = cleanupManager;
        _loadReportManager = loadReportManager;
    }

    public async Task Run(
        ITestFrameworkAdapter testFramework,
        ITest test,
        Action<AssertInternalState>? assertInternalState,
        params Scenario[] scenarios)
    {
        ArgumentNullException.ThrowIfNull(testFramework);
        ArgumentNullException.ThrowIfNull(test);
        ArgumentNullException.ThrowIfNull(scenarios);

        Exception? failure = null;
        try
        {
            _testExecutionState.Init(testFramework, test, scenarios);

            var cluster = _testExecutionState.TestSession.Cluster;
            var isLoadTest = _testExecutionState.TestType == TestType.Load;

            if (isLoadTest && cluster.Role == ClusterRole.Controller)
            {
                await _testExecutionState.TestSession.Configuration.ClusterRuntime.RunController(_testExecutionState, _serviceProvider);
            }
            else if (isLoadTest && cluster.RunLoadTestsOnCluster)
            {
                if (!cluster.TryStartRemoteRun())
                    throw new InvalidOperationException($"Cluster mode runs one load test per session. '{test.TestInfo.FullName}' was not run: select one load test only.");

                var isAttached = await _testExecutionState.TestSession.Configuration.ClusterRuntime.RunRemote(_testExecutionState, _serviceProvider);
                if (!isAttached)
                    return;
            }
            else
            {
                if (cluster.RunLoadTestsOnCluster || (cluster.SkipSuiteHooks && cluster.Role == ClusterRole.None))
                    throw new InvalidOperationException($"Cluster mode runs one load test per session, and '{test.TestInfo.FullName}' is a standard test: run it without the cluster.");

                await RunLocally(cluster);
            }

            if (_testExecutionState.TestResult.Status == TestStatus.Failed && _testExecutionState.FirstException != null)
            {
                _testExecutionState.TestResult.Failure = _testExecutionState.FirstException;
                _testExecutionState.TestResult.FailedIn = _testExecutionState.FailedIn;
            }
            _testExecutionState.TestSession.ResultManager.AddTestResults(_testExecutionState.TestResult);
            await _loadReportManager.WriteLoadReports(_testExecutionState);
            await _consoleManager.Complete();

            if (assertInternalState != null)
                assertInternalState(new AssertInternalState(_testExecutionState));

            if (_testExecutionState.FirstException != null)
                ExceptionDispatchInfo.Capture(_testExecutionState.FirstException).Throw();

            if (_testExecutionState.CancellationToken.IsCancellationRequested)
                throw new OperationCanceledException(_testExecutionState.CancellationToken);
        }
        catch (OperationCanceledException ex) when (_testExecutionState.CancellationToken.IsCancellationRequested)
        {
            failure = ex;
            throw;
        }
        catch (Exception ex)
        {
            failure = ex;
            if (_testExecutionState.ExecutionStoppedReason != null)
            {
                throw _testExecutionState.ExecutionStoppedReason;
            }

            throw;
        }
        finally
        {
            await _consoleManager.StopRealtimeConsoleOutput();
            var loadChild = _testExecutionState.TestSession.Cluster.LoadChild;
            if (loadChild != null)
                loadChild.Finish(failure);
            _testExecutionState.Dispose();
        }
    }

    private async Task RunLocally(ClusterSessionInfo cluster)
    {
        var loadChild = cluster.LoadChild;
        if (loadChild != null)
            loadChild.Attach(_testExecutionState);

        try
        {
            _consoleManager.StartRealtimeConsoleOutputIfEnabled();
            await _initManager.Run();

            if (loadChild != null)
                await loadChild.ReadyAndWaitForStart();

            await _executionManager.Run();
        }
        catch (OperationCanceledException) when (_testExecutionState.CancellationToken.IsCancellationRequested)
        {
            // Cancellation was requested — fall through to run cleanup
        }

        await _cleanupManager.Run();
    }
}
