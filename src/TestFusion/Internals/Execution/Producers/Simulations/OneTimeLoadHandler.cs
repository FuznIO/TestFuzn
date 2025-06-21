using TestFusion.Internals.State;

namespace TestFusion.Internals.Execution.Producers.Simulations;

internal class OneTimeLoadHandler : ILoadHandler
{
    private readonly OneTimeLoadConfiguration _configuration;
    private readonly string _scenarioName;
    private readonly SharedExecutionState _sharedExecutionState;

    public OneTimeLoadHandler(
        OneTimeLoadConfiguration configuration,
        string scenarioName,
        SharedExecutionState sharedExecutionState)
    {
        _configuration = configuration;
        _scenarioName = scenarioName;
        _sharedExecutionState = sharedExecutionState;
    }

    public Task Execute()
    {
        var oneTimeLoadCount = _configuration.Count;

        for (int i = 0; i < oneTimeLoadCount; i++)
        {
            if (_sharedExecutionState.ExecutionStatus == ExecutionStatus.Stopped)
                return Task.CompletedTask;

            var scenarioExecution = new ScenarioExecutionInfo(_scenarioName, _configuration.IsWarmup);

            _sharedExecutionState.EnqueueScenarioExecution(scenarioExecution);
        }

        return Task.CompletedTask;
    }
}