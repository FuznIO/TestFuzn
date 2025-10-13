using Fuzn.TestFuzn.Contracts.Plugins;

namespace Fuzn.TestFuzn.Plugins.WebSocket.Internals;

internal class WebSocketPlugin : IContextPlugin
{
    public WebSocketPlugin()
    {
    }
        
    public bool RequireState => false;
    
    public Task InitGlobal()
    {
        return Task.CompletedTask;
    }

    public async Task CleanupGlobal()
    {
        await Task.CompletedTask;
    }

    public object InitContext()
    {
        return null;
    }

    public Task CleanupContext(object state)
    {
        return Task.CompletedTask;
    }
}
