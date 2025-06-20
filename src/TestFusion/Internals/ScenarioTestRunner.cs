using System.Runtime.ExceptionServices;
using TestFusion.Internals.Producers;
using TestFusion.Internals.Logger;
using TestFusion.Internals.Init;
using TestFusion.Internals.Cleanup;
using TestFusion.Internals.Results.Load;
using TestFusion.Internals.Consumers;
using TestFusion.Internals.InputData;
using TestFusion.Internals.State;
using TestFusion.Cli.Internals;
using TestFusion.Internals.Results.Feature;
using TestFusion.Contracts.Adapters;
using TestFusion.Internals.ConsoleOutput;

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
        var resultsManager = new ResultsManager();
        var sharedExecutionState = new SharedExecutionState(resultsManager);
        var loadResultsManager = new LoadResultsManager(sharedExecutionState);
        var producerManager = new ProducerManager(sharedExecutionState);
        var initStepsRunner = new InitStepsRunner(_testFramework, sharedExecutionState);
        var inputDataFeeder = new InputDataFeeder(sharedExecutionState);
        var scenarioSimulationsSetup = new ScenarioSimulationsSetup(_testFramework, sharedExecutionState);
        var consoleWriter = new ConsoleWriter(_testFramework, sharedExecutionState, loadResultsManager);
        var scenarioExecutor = new ScenarioExecutor(_testFramework, sharedExecutionState, loadResultsManager, inputDataFeeder, consoleWriter);
        var consumerManager = new ConsumerManager(sharedExecutionState, scenarioExecutor, loadResultsManager);
        var consoleManager = new ConsoleManager(_testFramework, sharedExecutionState, consoleWriter, loadResultsManager);
        var scenarioFinalizer = new ScenarioFinalizer(_testFramework, sharedExecutionState, loadResultsManager);

        sharedExecutionState.Init(_featureTest, scenarios);

        try
        {
            loadResultsManager.Init(scenarios);

            await initStepsRunner.Run();

            inputDataFeeder.Init();

            await scenarioSimulationsSetup.Setup();

            producerManager.StartProducers();

            consumerManager.StartConsumers();

            consoleManager.StartRealtimeConsoleOutputIfEnabled();

            await producerManager.WaitForProducersToComplete();

            await consumerManager.WaitForConsumersToFinish();

            scenarioFinalizer.Complete();

            loadResultsManager.WriteReports();

            await consoleManager.Complete();

            if (sharedExecutionState.FirstException != null)
                ExceptionDispatchInfo.Capture(sharedExecutionState.FirstException).Throw();
        }
        catch (Exception)
        {
            if (sharedExecutionState.ExecutionStoppedReason != null)
            {
                throw sharedExecutionState.ExecutionStoppedReason;
            }

            throw;
        }
        finally
        {
            await new CleanupRunner(_testFramework, sharedExecutionState).Cleanup();
        }
    }
}
