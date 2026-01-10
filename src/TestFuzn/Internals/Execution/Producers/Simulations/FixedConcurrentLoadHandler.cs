using System.Diagnostics;
using Fuzn.TestFuzn.Internals.State;

namespace Fuzn.TestFuzn.Internals.Execution.Producers.Simulations;

internal class FixedConcurrentLoadHandler : ILoadHandler
{
    private readonly FixedConcurrentLoadConfiguration _configuration;
    private readonly TestExecutionState _testExecutionState;
    private readonly string _scenarioName;

    public FixedConcurrentLoadHandler(
        FixedConcurrentLoadConfiguration configuration,
        string scenarioName,
        TestExecutionState testExecutionState
        )
    {
        _configuration = configuration;
        _scenarioName = scenarioName;
        _testExecutionState = testExecutionState;
    }

    public async Task Execute()
    {
        var duration = _configuration.Duration;
        var stopwatch = Stopwatch.StartNew();
        var totalCount = _configuration.TotalCount;
        var i = 0;

        while (_testExecutionState.TestRunState.ExecutionStatus != ExecutionStatus.Stopped)
        {
            if (totalCount > 0)
            {
                if (i >= totalCount)
                    break;
            }
            else if (stopwatch.Elapsed >= duration)
            {
                break;
            }

            var addCount = _configuration.FixedCount - _testExecutionState.GetConstantQueueCount(_scenarioName);

            while (addCount > 0)
            {
                addCount--;
                i++;

                var message = new ExecuteScenarioMessage(_scenarioName, _configuration.IsWarmup);

                _testExecutionState.AddToConstantQueue(message);
                _testExecutionState.EnqueueScenarioExecution(message);
            }
            await Task.Delay(TimeSpan.FromMilliseconds(10));
        }
        stopwatch.Stop();
    }
}
