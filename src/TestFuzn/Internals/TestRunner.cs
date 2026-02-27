using Fuzn.TestFuzn.Contracts.Adapters;
using Fuzn.TestFuzn.Internals.Cleanup;
using Fuzn.TestFuzn.Internals.ConsoleOutput;
using Fuzn.TestFuzn.Internals.Execution;
using Fuzn.TestFuzn.Internals.Execution.Consumers;
using Fuzn.TestFuzn.Internals.Execution.Producers;
using Fuzn.TestFuzn.Internals.Init;
using Fuzn.TestFuzn.Internals.InputData;
using Fuzn.TestFuzn.Internals.Logger;
using Fuzn.TestFuzn.Internals.Reports;
using Fuzn.TestFuzn.Internals.Results.Standard;
using Fuzn.TestFuzn.Internals.State;
using System.Runtime.ExceptionServices;

namespace Fuzn.TestFuzn.Internals;

internal class TestRunner
{
    private readonly ITestFrameworkAdapter _testFramework;
    private readonly ITest _test;
    internal Action<AssertInternalState> _assertInternalState;

    public TestRunner(ITestFrameworkAdapter testFramework, 
        ITest test,
        Action<AssertInternalState> assertInternalState)
    {
        if (testFramework == null)
                throw new ArgumentNullException(nameof(testFramework));
            _testFramework = testFramework;

        _test = test;
        _assertInternalState = assertInternalState;
    }

    public async Task Run(params Scenario[] scenarios)
    {
        var testExecutionState = new TestExecutionState(_test, scenarios);
        var consoleWriter = new ConsoleWriter(_testFramework, testExecutionState);
        var consoleManager = new ConsoleManager(_testFramework, testExecutionState, consoleWriter);
        var inputDataFeeder = new InputDataFeeder(testExecutionState);
        var initManager = new InitManager(_testFramework, testExecutionState, inputDataFeeder);
        var producerManager = new ProducerManager(testExecutionState);
        var executeScenarioMessageHandler = new ExecuteScenarioMessageHandler(_testFramework, testExecutionState, inputDataFeeder);
        var consumerManager = new ConsumerManager(testExecutionState, executeScenarioMessageHandler);
        var executionManager = new ExecutionManager(_testFramework, testExecutionState, producerManager, consumerManager);
        var cleanupManager = new CleanupManager(_testFramework, testExecutionState);
        var standardResultManager = new StandardResultManager();
        var reportManager = new ReportManager();

        try
        {
            consoleManager.StartRealtimeConsoleOutputIfEnabled();
            await initManager.Run();
            await executionManager.Run();
            await cleanupManager.Run();
            standardResultManager.AddTestResults(testExecutionState.TestResult);
            await reportManager.WriteLoadReports(testExecutionState);
            await consoleManager.Complete();

            if (_assertInternalState != null)
                _assertInternalState(new AssertInternalState(testExecutionState));

            if (testExecutionState.FirstException != null)
                ExceptionDispatchInfo.Capture(testExecutionState.FirstException).Throw();
        }
        catch (Exception)
        {
            if (testExecutionState.ExecutionStoppedReason != null)
            {
                throw testExecutionState.ExecutionStoppedReason;
            }

            throw;
        }
    }
}
