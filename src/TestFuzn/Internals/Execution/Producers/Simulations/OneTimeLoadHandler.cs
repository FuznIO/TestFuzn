using Fuzn.TestFuzn.Internals.State;

namespace Fuzn.TestFuzn.Internals.Execution.Producers.Simulations;

internal class OneTimeLoadHandler : ILoadHandler
{
    private readonly OneTimeLoadConfiguration _configuration;
    private readonly string _scenarioName;
    private readonly TestExecutionState _testExecutionState;

    public OneTimeLoadHandler(
        OneTimeLoadConfiguration configuration,
        string scenarioName,
        TestExecutionState testExecutionState)
    {
        _configuration = configuration;
        _scenarioName = scenarioName;
        _testExecutionState = testExecutionState;
    }

    public Task Execute()
    {
        var oneTimeLoadCount = _configuration.Count;

        for (int i = 0; i < oneTimeLoadCount; i++)
        {
            if (_testExecutionState.ExecutionStatus == ExecutionStatus.Stopped)
                return Task.CompletedTask;

            var message = new ExecuteScenarioMessage(_scenarioName, _configuration.IsWarmup);

            _testExecutionState.EnqueueScenarioExecution(message);
        }

        return Task.CompletedTask;
    }
}