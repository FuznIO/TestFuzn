using System.Runtime.ExceptionServices;
using TestFusion.Internals.Producers;
using TestFusion.Internals.Logger;
using TestFusion.Internals.Init;
using TestFusion.Internals.Cleanup;
using TestFusion.Internals.Producers.Simulations;
using TestFusion.Internals.Results.Load;
using TestFusion.Internals.Consumers;
using TestFusion.Internals.InputData;
using TestFusion.Internals.State;
using TestFusion.Cli.Internals;
using TestFusion.Internals.Results.Feature;
using TestFusion.Plugins.TestFrameworkProviders;
using TestFusion.Internals.ConsoleOutput;

namespace TestFusion.Internals;

internal class ScenarioTestRunner
{
    private readonly ITestFrameworkProvider _testFramework;
    private readonly IFeatureTest _featureTest;

    public ScenarioTestRunner(ITestFrameworkProvider testFramework, IFeatureTest featureTest)
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
        var inputFeeder = new InputDataFeeder(sharedExecutionState);
        var consoleWriter = new ConsoleWriter(_testFramework, sharedExecutionState, loadResultsManager);
        var scenarioExecutor = new ScenarioExecutor(_testFramework, sharedExecutionState, loadResultsManager, inputFeeder, consoleWriter);
        var consumerManager = new ConsumerManager(sharedExecutionState, scenarioExecutor, loadResultsManager);
        var consoleManager = new ConsoleManager(_testFramework, sharedExecutionState, consoleWriter, loadResultsManager);

        sharedExecutionState.Init(_featureTest, scenarios);

        try
        {
            loadResultsManager.Init(scenarios);

            await initStepsRunner.Run();

            inputFeeder.Init();

            foreach (var scenario in scenarios)
            {
                if (sharedExecutionState.TestType == TestType.Feature)
                {
                    var totalExecutions = 1;
                    if (scenario.InputDataInfo.HasInputData)
                        totalExecutions = scenario.InputDataInfo.InputDataList.Count;

                    scenario.SimulationsInternal.Add(new FixedConcurrentLoadConfiguration(1, totalExecutions));
                }
            }

            consoleWriter.ScenarioStart();

            producerManager.StartProducers();

            consumerManager.StartConsumers();

            consoleManager.StartRealtimeConsoleOutputIfEnabled();

            await producerManager.WaitForProducersToComplete();

            await consumerManager.WaitForConsumersToFinish();

            sharedExecutionState.ScenarioResult.MarkAsCompleted();

            if (sharedExecutionState.TestType == TestType.Load)
            {
                if (sharedExecutionState.ExecutionStatus != ExecutionStatus.Stopped)
                {
                    foreach (var scenario in scenarios)
                    {
                        var scenarioCollector = loadResultsManager.GetScenarioCollector(scenario.Name);
                        scenarioCollector.MarkAsCompleted();
                        var scenarioResult = scenarioCollector.GetCurrentResult();
                        if (scenario.AssertWhenDoneAction != null)
                        {
                            try
                            {
                                scenario.AssertWhenDoneAction(new AssertScenarioStats(scenarioResult));
                            }
                            catch (Exception e)
                            {
                                sharedExecutionState.FirstException = e;
                                scenarioCollector.AssertWhenDoneException(e);
                                scenarioCollector.SetStatus(ScenarioStatus.Failed);
                            }
                        }
                    }
                }
                loadResultsManager.Complete();
            }

            await consoleManager.Complete();

            loadResultsManager.WriteReports();

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
