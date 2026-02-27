using Fuzn.TestFuzn.Internals.State;

namespace Fuzn.TestFuzn.Internals.Execution.Producers.Simulations;

internal class PauseLoadHandler : ILoadHandler
{
    private readonly PauseLoadConfiguration _configuration;
    private readonly TestExecutionState _testExecutionState;

    public PauseLoadHandler(PauseLoadConfiguration configuration, TestExecutionState testExecutionState)
    {
        _configuration = configuration;
        _testExecutionState = testExecutionState;
    }

    public async Task Execute()
    {
        var end = DateTime.UtcNow.Add(_configuration.Duration);

        while (DateTime.UtcNow < end
            && _testExecutionState.ExecutionStatus != ExecutionStatus.Stopped)
        {
            var remaining = end - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
                break;

            var delay = remaining < TimeSpan.FromMilliseconds(100)
                ? remaining
                : TimeSpan.FromMilliseconds(100);

            await Task.Delay(delay);
        }
    }
}
