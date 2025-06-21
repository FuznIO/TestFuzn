namespace TestFusion.Internals.Execution.Producers.Simulations;

internal class PauseLoadHandler : ILoadHandler
{
    private readonly PauseLoadConfiguration _configuration;

    public PauseLoadHandler(PauseLoadConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task Execute()
    {
        await Task.Delay(_configuration.Duration);
    }
}
