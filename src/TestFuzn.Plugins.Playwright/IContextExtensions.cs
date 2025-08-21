using Microsoft.Playwright;
using FuznLabs.TestFuzn.Plugins.Playwright.Internals;

namespace FuznLabs.TestFuzn.Plugins.Playwright;

public static class IContextExtensions
{
    public static async Task<IPage> CreateBrowserPage(this TestFuzn.Context context, string browserType = null)
    {
        var playwrightManager = (PlaywrightManager) context.Internals.Plugins.GetState(typeof(PlaywrightPlugin));

        var page = await playwrightManager.CreatePage(browserType);
        page.SetDefaultTimeout((float)GlobalState.Configuration.DefaultTimeout.TotalMilliseconds);

        return page;
    }
}
