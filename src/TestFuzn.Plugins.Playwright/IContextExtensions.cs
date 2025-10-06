using Microsoft.Playwright;
using Fuzn.TestFuzn.Plugins.Playwright.Internals;

namespace Fuzn.TestFuzn.Plugins.Playwright;

public static class IContextExtensions
{
    public static async Task<IPage> CreateBrowserPage(this TestFuzn.Context context, string browserType = null)
    {
        var playwrightManager = (PlaywrightManager) context.Internals.Plugins.GetState(typeof(PlaywrightPlugin));

        var page = await playwrightManager.CreatePage(browserType);
        page.SetDefaultTimeout((float) PlaywrightGlobalState.Configuration.DefaultTimeout.TotalMilliseconds);

        return page;
    }
}
