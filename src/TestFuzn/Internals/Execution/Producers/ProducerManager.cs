using Fuzn.TestFuzn.Internals.Execution.Producers.Simulations;
using Fuzn.TestFuzn.Internals.State;

namespace Fuzn.TestFuzn.Internals.Execution.Producers;

internal class ProducerManager
{
    private readonly TestExecutionState _testExecutionState;
    private List<Task> _producerTasks = new();

    public ProducerManager(TestExecutionState testExecutionState)
    {
        _testExecutionState = testExecutionState;
    }

    public void StartProducers()
    {
        foreach (var scenario in _testExecutionState.Scenarios)
        {
            var producerTask = Task.Run(async () => await Produce(scenario));
            _producerTasks.Add(producerTask);
        }
    }

    private async Task Produce(Scenario scenario)
    {
        var loadCollector = _testExecutionState.LoadCollectors[scenario.Name];
        var hasWarmupPhase = false;
        var measurementPhaseStarted = false;

        foreach (var loadSimulation in scenario.SimulationsInternal)
        {
            if (_testExecutionState.ExecutionStatus == ExecutionStatus.Stopped)
                break;

            ILoadHandler handler = loadSimulation switch
            {
                OneTimeLoadConfiguration oneTimeLoad => new OneTimeLoadHandler(oneTimeLoad, scenario.Name, _testExecutionState),
                FixedConcurrentLoadConfiguration fixedConcurrent => new FixedConcurrentLoadHandler(fixedConcurrent, scenario.Name, _testExecutionState),
                FixedLoadConfiguration fixedLoad => new FixedLoadHandler(fixedLoad, scenario.Name, _testExecutionState),
                RandomLoadPerSecondConfiguration randomLoadPerSecond => new RandomLoadPerSecondHandler(randomLoadPerSecond, scenario.Name, _testExecutionState),
                GradualLoadIncreaseConfiguration gradualLoadIncrease => new GradualLoadIncreaseHandler(gradualLoadIncrease, scenario.Name, _testExecutionState),
                PauseLoadConfiguration pauseLoad => new PauseLoadHandler(pauseLoad, _testExecutionState),
                _ => throw new NotSupportedException($"Load simulation type {loadSimulation.GetType().Name} is not supported."),
            };

            if (loadSimulation.IsWarmup && !hasWarmupPhase)
            {
                hasWarmupPhase = true;
                loadCollector.MarkPhaseAsStarted(LoadTestPhase.Warmup, DateTime.UtcNow);
            }

            if (hasWarmupPhase && !loadSimulation.IsWarmup)
            {
                while (_testExecutionState.IsExecutionQueueEmpty(scenario.Name) == false)
                    await Task.Delay(TimeSpan.FromMilliseconds(100));

                loadCollector.MarkPhaseAsCompleted(LoadTestPhase.Warmup, DateTime.UtcNow);
            }

            if (!loadSimulation.IsWarmup && !measurementPhaseStarted)
            {
                measurementPhaseStarted = true;
                var timestamp = DateTime.UtcNow;
                _testExecutionState.TestResult.MarkPhaseAsStarted(StandardTestPhase.Execute, timestamp);
                loadCollector.MarkPhaseAsStarted(LoadTestPhase.Measurement, timestamp);
            }

            await handler.Execute();
        }

        _testExecutionState.MarkScenarioProducersCompleted(scenario.Name);
    }

    public async Task WaitForProducersToComplete()
    {
        await Task.WhenAll(_producerTasks);
        _testExecutionState.ExecutionState.MessageQueue.CompleteAdding();
    }
}
