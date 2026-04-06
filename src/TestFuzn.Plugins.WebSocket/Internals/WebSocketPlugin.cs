using Fuzn.TestFuzn.Contracts.Plugins;

namespace Fuzn.TestFuzn.Plugins.WebSocket.Internals;

internal class WebSocketPlugin : IContextPlugin
{
    public bool RequireIterationState => true;
    public bool RequireStepExceptionHandling => false;

    public Task InitSuite()
    {
        return Task.CompletedTask;
    }

    public Task CleanupSuite()
    {
        return Task.CompletedTask;
    }

    public object InitIteration(IServiceProvider serviceProvider)
    {
        return new WebSocketManager();
    }

    public async Task CleanupIteration(object state)
    {
        if (state is WebSocketManager webSocketManager)
        {
            await webSocketManager.CleanupIteration();
        }
    }

    public Task HandleStepException(object state, IterationContext context, Exception exception)
    {
        throw new NotImplementedException();
    }
}
