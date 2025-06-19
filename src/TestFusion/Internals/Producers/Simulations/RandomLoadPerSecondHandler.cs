using System.Diagnostics;
using TestFusion.Internals.State;

namespace TestFusion.Internals.Producers.Simulations;

internal class RandomLoadPerSecondHandler : ILoadHandler
{
    private readonly RandomLoadPerSecondConfiguration _randomLoadPerSecond;
    private readonly string _scenarioName;
    private readonly SharedExecutionState _sharedExecutionState;

    public RandomLoadPerSecondHandler(
        RandomLoadPerSecondConfiguration randomLoadPerSecond,
        string scenarioName,
        SharedExecutionState sharedExecutionState)
    {
        _randomLoadPerSecond = randomLoadPerSecond;
        _scenarioName = scenarioName;
        _sharedExecutionState = sharedExecutionState;
    }

    public async Task Execute()
    {   
        var minRate = _randomLoadPerSecond.MinRate;
        var maxRate = _randomLoadPerSecond.MaxRate;
        var random = new Random();
        var stopwatch = new Stopwatch();

        var end = DateTime.UtcNow.Add(_randomLoadPerSecond.Duration);

        while (DateTime.UtcNow < end
            && _sharedExecutionState.ExecutionStatus != ExecutionStatus.Stopped)
        {
            var currentRate = random.Next(minRate, maxRate);

            stopwatch.Reset();
            stopwatch.Start();

            for (int i = 0; i < currentRate; i++)
            {
                var scenarioExecution = new ScenarioExecutionInfo(_scenarioName);

                _sharedExecutionState.EnqueueScenarioExecution(scenarioExecution);
            }
            stopwatch.Stop();

            var sleep = 1000 - stopwatch.ElapsedMilliseconds;
            if (sleep > 0)
                await Task.Delay(TimeSpan.FromMilliseconds(sleep));
        }
    }
}