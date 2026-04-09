using System.Diagnostics;
using Fuzn.TestFuzn.Internals.State;

namespace Fuzn.TestFuzn.Internals.Execution.Producers.Simulations;

internal class RandomLoadPerSecondHandler : ILoadHandler
{
    private readonly RandomLoadPerSecondConfiguration _configuration;
    private readonly Scenario _scenario;
    private readonly TestExecutionState _testExecutionState;

    public RandomLoadPerSecondHandler(
        RandomLoadPerSecondConfiguration configuration,
        Scenario scenario,
        TestExecutionState testExecutionState)
    {
        _configuration = configuration;
        _scenario = scenario;
        _testExecutionState = testExecutionState;
    }

    public async Task Execute()
    {   
        var minRate = _configuration.MinRate;
        var maxRate = _configuration.MaxRate;
        var random = new Random();
        var stopwatch = new Stopwatch();

        var end = DateTime.UtcNow.Add(_configuration.Duration);

        while (DateTime.UtcNow < end
            && _testExecutionState.ExecutionStatus != ExecutionStatus.Stopped)
        {
            var currentRate = random.Next(minRate, maxRate + 1);

            stopwatch.Reset();
            stopwatch.Start();

            for (int i = 0; i < currentRate; i++)
            {
                if (_testExecutionState.ExecutionStatus == ExecutionStatus.Stopped)
                    return;

                var message = new ExecuteScenarioMessage(_scenario, _configuration.IsWarmup);

                _testExecutionState.EnqueueScenarioExecution(message);
            }
            stopwatch.Stop();

            var sleep = 1000 - stopwatch.ElapsedMilliseconds;
            if (sleep > 0)
                await Task.Delay(TimeSpan.FromMilliseconds(sleep), _testExecutionState.CancellationToken);
        }
    }
}