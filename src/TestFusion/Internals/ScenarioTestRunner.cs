using System.Runtime.ExceptionServices;
using TestFusion.Internals.Execution.Producers;
using TestFusion.Internals.Logger;
using TestFusion.Internals.Init;
using TestFusion.Internals.Cleanup;
using TestFusion.Internals.Execution.Consumers;
using TestFusion.Internals.InputData;
using TestFusion.Internals.State;
using TestFusion.Cli.Internals;
using TestFusion.Internals.Results.Feature;
using TestFusion.Contracts.Adapters;
using TestFusion.Internals.ConsoleOutput;
using TestFusion.Internals.Execution;
using TestFusion.Internals.Reports;

namespace TestFusion.Internals;

internal class ScenarioTestRunner
{
    private readonly ITestFrameworkAdapter _testFramework;
    private readonly IFeatureTest _featureTest;

    public ScenarioTestRunner(ITestFrameworkAdapter testFramework, IFeatureTest featureTest)
    {
        if (GlobalState.CustomTestRunner)
            _testFramework = new TestFusionProvider();
        else
        {
            if (testFramework == null)
                throw new ArgumentNullException(nameof(testFramework));
            _testFramework = testFramework;
        }

        _featureTest = featureTest;
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
            featureResultManager.AddScenarioResults(sharedExecutionState.FeatureName, sharedExecutionState.ResultState.FeatureCollectors);
            await reportManager.WriteLoadReports(sharedExecutionState);
            await consoleManager.Complete();

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
