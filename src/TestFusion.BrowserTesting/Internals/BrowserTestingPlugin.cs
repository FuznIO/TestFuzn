using TestFusion.Plugins.Context;

namespace TestFusion.BrowserTesting.Internals;

internal class BrowserTestingPlugin : IContextPlugin
{
    public bool RequireState => true;

    public async Task InitGlobal()
    {
        await PlaywrightManager.InitializeGlobalResources();
    }
    
    public async Task CleanupGlobal()
    {
        await PlaywrightManager.CleanupGlobalResources();
    }

    public object InitContext()
    {
        var playwrightManager = new PlaywrightManager();
        return playwrightManager;
    }

    public async Task CleanupContext(object state)
    {
        var playwrightManager = state as PlaywrightManager;

        await playwrightManager.CleanupContext();
    }
}
