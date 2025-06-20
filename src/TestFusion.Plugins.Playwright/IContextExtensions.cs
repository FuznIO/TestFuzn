using Microsoft.Playwright;
using TestFusion.Plugins.Playwright.Internals;

namespace TestFusion.Plugins.Playwright;

public static class IContextExtensions
{
    public static async Task<IPage> CreateBrowserPage(this TestFusion.Context context, string browserType = null)
    {
        var playwrightManager = (PlaywrightManager) context.Internals.Plugins.GetState(typeof(PlaywrightPlugin));

        var page = await playwrightManager.CreatePage(browserType);

        return page;
    }
}
