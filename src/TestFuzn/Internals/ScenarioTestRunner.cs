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
using Fuzn.TestFuzn.Internals.Results.Feature;
using Fuzn.TestFuzn.Internals.State;
using System.Runtime.ExceptionServices;

namespace Fuzn.TestFuzn.Internals;

internal class ScenarioTestRunner
{
    private readonly ITestFrameworkAdapter _testFramework;
    private readonly IFeatureTest _featureTest;
    internal Action<AssertInternalState> _assertInternalState;

    public ScenarioTestRunner(ITestFrameworkAdapter testFramework, 
        IFeatureTest featureTest,
        Action<AssertInternalState> assertInternalState)
    {
        if (testFramework == null)
                throw new ArgumentNullException(nameof(testFramework));
            _testFramework = testFramework;

        _featureTest = featureTest;
        _assertInternalState = assertInternalState;
    }

    public async Task Run(params Scenario[] scenarios)
    {
        var sharedExecutionState = new SharedExecutionState(_featureTest, scenarios);
        var consoleWriter = new ConsoleWriter(_testFramework, sharedExecutionState);
        var consoleManager = new ConsoleManager(_testFramework, sharedExecutionState, consoleWriter);
        var inputDataFeeder = new InputDataFeeder(sharedExecutionState);
        var initManager = new InitManager(_testFramework, sharedExecutionState, inputDataFeeder);
        var producerManager = new ProducerManager(sharedExecutionState);
        var executeScenarioMessageHandler = new ExecuteScenarioMessageHandler(_testFramework, sharedExecutionState, inputDataFeeder);
        var consumerManager = new ConsumerManager(sharedExecutionState, executeScenarioMessageHandler);
        var executionManager = new ExecutionManager(_testFramework, sharedExecutionState, producerManager, consumerManager);
        var cleanupManager = new CleanupManager(_testFramework, sharedExecutionState);
        var featureResultManager = new FeatureResultManager();
        var reportManager = new ReportManager();

        try
        {
            consoleManager.StartRealtimeConsoleOutputIfEnabled();
            await initManager.Run();
            await executionManager.Run();
            await cleanupManager.Run();
            featureResultManager.AddScenarioResults(sharedExecutionState);
            await reportManager.WriteLoadReports(sharedExecutionState);
            await consoleManager.Complete();

            if (_assertInternalState != null)
                _assertInternalState(new AssertInternalState(sharedExecutionState));

            if (sharedExecutionState.TestRunState.FirstException != null)
                ExceptionDispatchInfo.Capture(sharedExecutionState.TestRunState.FirstException).Throw();
        }
        catch (Exception)
        {
            if (sharedExecutionState.TestRunState.ExecutionStoppedReason != null)
            {
                throw sharedExecutionState.TestRunState.ExecutionStoppedReason;
            }

            throw;
        }
    }
}
