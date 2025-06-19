using Microsoft.Playwright;
using TestFusion.BrowserTesting.Internals;

namespace TestFusion.BrowserTesting;

public static class IContextExtensions
{
    public static async Task<IPage> CreateBrowserPage(this Context context, string browserType = null)
    {
        var playwrightManager = (PlaywrightManager) context.Internals.Plugins.GetState(typeof(BrowserTestingPlugin));

        var page = await playwrightManager.CreatePage(browserType);

        return page;
    }
}
