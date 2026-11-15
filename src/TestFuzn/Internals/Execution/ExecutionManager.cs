using Fuzn.TestFuzn.Internals.State;
using Fuzn.TestFuzn.Internals.Execution.Consumers;
using Fuzn.TestFuzn.Internals.Execution.Producers;
using Fuzn.TestFuzn.Contracts;
using Fuzn.TestFuzn.Internals.Cluster;

namespace Fuzn.TestFuzn.Internals.Execution;

internal class ExecutionManager
{
    private readonly IServiceProvider _serviceProvider;
    private readonly TestExecutionState _testExecutionState;
    private readonly ProducerManager _producerManager;
    private readonly ConsumerManager _consumerManager;

    public ExecutionManager(
        IServiceProvider serviceProvider,
        TestExecutionState testExecutionState,
        ProducerManager producerManager,
        ConsumerManager consumerManager)
    {
        _serviceProvider = serviceProvider;
        _testExecutionState = testExecutionState;
        _producerManager = producerManager;
        _consumerManager = consumerManager;
    }

    public async Task Run()
    {
        _producerManager.StartProducers();

        _consumerManager.StartConsumers();

        try
        {
            await _producerManager.WaitForProducersToComplete();
        }
        catch (OperationCanceledException)
        {
            _testExecutionState.MessageQueue.CompleteAdding();
        }

        try
        {
            await _consumerManager.WaitForConsumersToFinish();
        }
        catch (OperationCanceledException)
        {
            // Consumer was cancelled — expected during test cancellation
        }

        ExecuteAssertWhenDone();

        _testExecutionState.TestResult.MarkPhaseAsCompleted(StandardTestPhase.Execute, DateTime.UtcNow);
    }

    private void ExecuteAssertWhenDone()
    {
        if (_testExecutionState.TestResult.TestType == TestType.Standard)
            return;

        if (_testExecutionState.ExecutionStatus == ExecutionStatus.Stopped)
            return;

        if (_testExecutionState.TestSession.Cluster.Role == ClusterRole.LoadChild)
            return;

        AssertWhenDone(_testExecutionState, _serviceProvider);
    }

    internal static void AssertWhenDone(TestExecutionState state, IServiceProvider serviceProvider)
    {
        foreach (var scenario in state.Scenarios)
        {
            var scenarioCollector = state.LoadCollectors[scenario.Name];
            var scenarioResult = scenarioCollector.GetCurrentResult();
            if (scenario.AssertWhenDoneAction != null)
            {
                try
                {
                    var context = ContextFactory.CreateScenarioContext(state.TestSession, serviceProvider, state.TestFramework, "AssertWhenDoneAction", state.CancellationToken, scenario);
                    scenario.AssertWhenDoneAction(context, new AssertScenarioStats(scenarioResult));
                }
                catch (Exception e)
                {
                    state.FirstException = e;
                    state.TestResult.Status = TestStatus.Failed;
                    scenarioCollector.SetAssertWhenDoneException(e);
                    scenarioCollector.SetStatus(TestStatus.Failed);
                }
            }
        }
    }
}
