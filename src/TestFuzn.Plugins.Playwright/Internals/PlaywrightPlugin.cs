using Fuzn.TestFuzn.Contracts.Plugins;

namespace Fuzn.TestFuzn.Plugins.Playwright.Internals;

internal class PlaywrightPlugin : IContextPlugin
{
    public bool RequireState => true;
    public bool RequireStepExceptionHandling => true;

    public async Task InitSuite()
    {
        await PlaywrightManager.InitializeGlobalResources();
    }
    
    public async Task CleanupSuite()
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

    public async Task HandleStepException(object state, IterationContext context, Exception exception)
    {
        var playwrightManager = state as PlaywrightManager;

        await playwrightManager.AddOrLogPageMetadata(context);
    }
}
