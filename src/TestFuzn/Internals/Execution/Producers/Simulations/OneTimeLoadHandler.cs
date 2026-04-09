using Fuzn.TestFuzn.Internals.State;

namespace Fuzn.TestFuzn.Internals.Execution.Producers.Simulations;

internal class OneTimeLoadHandler : ILoadHandler
{
    private readonly OneTimeLoadConfiguration _configuration;
    private readonly Scenario _scenario;
    private readonly TestExecutionState _testExecutionState;

    public OneTimeLoadHandler(
        OneTimeLoadConfiguration configuration,
        Scenario scenario,
        TestExecutionState testExecutionState)
    {
        _configuration = configuration;
        _scenario = scenario;
        _testExecutionState = testExecutionState;
    }

    public Task Execute()
    {
        var oneTimeLoadCount = _configuration.Count;

        for (int i = 0; i < oneTimeLoadCount; i++)
        {
            if (_testExecutionState.ExecutionStatus == ExecutionStatus.Stopped)
                return Task.CompletedTask;

            var message = new ExecuteScenarioMessage(_scenario, _configuration.IsWarmup);

            _testExecutionState.EnqueueScenarioExecution(message);
        }

        return Task.CompletedTask;
    }
}