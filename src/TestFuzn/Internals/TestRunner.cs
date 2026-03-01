using Fuzn.TestFuzn.Contracts.Adapters;
using Fuzn.TestFuzn.Internals.Cleanup;
using Fuzn.TestFuzn.Internals.Execution;
using Fuzn.TestFuzn.Internals.Init;
using Fuzn.TestFuzn.Internals.Logger;
using Fuzn.TestFuzn.Internals.Reports.Load;
using Fuzn.TestFuzn.Internals.State;
using System.Runtime.ExceptionServices;

namespace Fuzn.TestFuzn.Internals;

internal class TestRunner
{
    private readonly TestExecutionState _testExecutionState;
    private readonly ConsoleManager _consoleManager;
    private readonly InitManager _initManager;
    private readonly ExecutionManager _executionManager;
    private readonly CleanupManager _cleanupManager;
    private readonly LoadReportManager _loadReportManager;

    public TestRunner(
        TestExecutionState testExecutionState,
        ConsoleManager consoleManager,
        InitManager initManager,
        ExecutionManager executionManager,
        CleanupManager cleanupManager,
        LoadReportManager loadReportManager)
    {
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

        try
        {
            _testExecutionState.Init(testFramework, test, scenarios);

            _consoleManager.StartRealtimeConsoleOutputIfEnabled();
            await _initManager.Run();
            await _executionManager.Run();
            await _cleanupManager.Run();
            TestSession.Current.ResultManager.AddTestResults(_testExecutionState.TestResult);
            await _loadReportManager.WriteLoadReports(_testExecutionState);
            await _consoleManager.Complete();

            if (assertInternalState != null)
                assertInternalState(new AssertInternalState(_testExecutionState));

            if (_testExecutionState.FirstException != null)
                ExceptionDispatchInfo.Capture(_testExecutionState.FirstException).Throw();
        }
        catch (Exception)
        {
            if (_testExecutionState.ExecutionStoppedReason != null)
            {
                throw _testExecutionState.ExecutionStoppedReason;
            }

            throw;
        }
    }
}
