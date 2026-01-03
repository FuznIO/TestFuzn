using Fuzn.TestFuzn.Contracts.Plugins;

namespace Fuzn.TestFuzn.Plugins.WebSocket.Internals;

internal class WebSocketPlugin : IContextPlugin
{
    public WebSocketPlugin()
    {
    }
        
    public bool RequireState => true;
    public bool RequireStepExceptionHandling => false;
    
    public Task InitSuite()
    {
        return Task.CompletedTask;
    }

    public async Task CleanupSuite()
    {
        await Task.CompletedTask;
    }

    public object InitContext()
    {
        var webSocketManager = new WebSocketManager();
        return webSocketManager;
    }

    public async Task CleanupContext(object state)
    {
        var webSocketManager = state as WebSocketManager;
        if (webSocketManager != null)
        {
            await webSocketManager.CleanupContext();
        }
    }

    public Task HandleStepException(object state, IterationContext context, Exception exception)
    {
        throw new NotImplementedException();
    }
}
