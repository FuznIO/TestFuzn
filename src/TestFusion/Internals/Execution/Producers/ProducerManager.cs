using TestFusion.Internals.Execution.Producers.Simulations;
using TestFusion.Internals.Results.Load;
using TestFusion.Internals.State;

namespace TestFusion.Internals.Execution.Producers;

internal class ProducerManager
{
    private readonly SharedExecutionState _sharedExecutionState;
    private List<Task> _producerTasks = new();

    public ProducerManager(SharedExecutionState sharedExecutionState)
    {
        _sharedExecutionState = sharedExecutionState;
    }

    public void StartProducers()
    {
        foreach (var scenario in _sharedExecutionState.Scenarios)
        {
            var producerTask = Task.Run(async () => await Produce(scenario));
            _producerTasks.Add(producerTask);
        }
    }

    private async Task Produce(Scenario scenario)
    {
        var scenarioCollector = _sharedExecutionState.ResultState.LoadCollectors[scenario.Name];
        var hasWarmupPhase = false;
        var measurementPhaseStarted = false;

        foreach (var loadSimulation in scenario.SimulationsInternal)
        {
            if (_sharedExecutionState.TestRunState.ExecutionStatus == ExecutionStatus.Stopped)
                break;

            ILoadHandler handler = loadSimulation switch
            {
                OneTimeLoadConfiguration oneTimeLoad => new OneTimeLoadHandler(oneTimeLoad, scenario.Name, _sharedExecutionState),
                FixedConcurrentLoadConfiguration fixedConcurrent => new FixedConcurrentLoadHandler(fixedConcurrent, scenario.Name, _sharedExecutionState),
                FixedLoadConfiguration fixedLoad => new FixedLoadHandler(fixedLoad, scenario.Name, _sharedExecutionState),
                RandomLoadPerSecondConfiguration randomLoadPerSecond => new RandomLoadPerSecondHandler(randomLoadPerSecond, scenario.Name, _sharedExecutionState),
                GradualLoadIncreaseConfiguration gradualLoadIncrease => new GradualLoadIncreaseHandler(gradualLoadIncrease, scenario.Name, _sharedExecutionState),
                PauseLoadConfiguration gradualLoadIncrease => new PauseLoadHandler(gradualLoadIncrease),
                _ => throw new NotSupportedException($"Load simulation type {loadSimulation.GetType().Name} is not supported."),
            };

            if (loadSimulation.IsWarmup && !hasWarmupPhase)
            {
                hasWarmupPhase = true;
                scenarioCollector.MarkPhaseAsStarted(LoadTestPhase.Warmup);
            }

            if (hasWarmupPhase && !loadSimulation.IsWarmup)
            {
                while (_sharedExecutionState.IsExecutionQueueEmpty(scenario.Name) == false)
                    await Task.Delay(TimeSpan.FromMilliseconds(100));

                scenarioCollector.MarkPhaseAsCompleted(LoadTestPhase.Warmup);
            }

            if (!loadSimulation.IsWarmup && !measurementPhaseStarted)
            {
                measurementPhaseStarted = true;
                scenarioCollector.MarkPhaseAsStarted(LoadTestPhase.Measurement);
            }

            await handler.Execute();
        }

        _sharedExecutionState.MarkScenarioProducersCompleted(scenario.Name);
    }

    public async Task WaitForProducersToComplete()
    {
        await Task.WhenAll(_producerTasks);
        _sharedExecutionState.ExecutionState.MessageQueue.CompleteAdding();
    }
}
