using TestFusion.Internals.Producers.Simulations;
using TestFusion.Internals.State;

namespace TestFusion.Internals.Producers;

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
        foreach (var loadSimulation in scenario.SimulationsInternal)
        {
            if (_sharedExecutionState.ExecutionStatus == ExecutionStatus.Stopped)
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

            await handler.Execute();
        }

        _sharedExecutionState.MarkScenarioProducersCompleted(scenario.Name);
    }

    public async Task WaitForProducersToComplete()
    {
        await Task.WhenAll(_producerTasks);
        _sharedExecutionState.ScenarioExecutionQueue.CompleteAdding();
    }
}
