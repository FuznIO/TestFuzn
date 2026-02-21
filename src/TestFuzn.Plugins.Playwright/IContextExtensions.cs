using Microsoft.Playwright;
using Fuzn.TestFuzn.Plugins.Playwright.Internals;

namespace Fuzn.TestFuzn.Plugins.Playwright;

/// <summary>
/// Extension methods for accessing Playwright functionality from a <see cref="TestFuzn.Context"/>.
/// </summary>
public static class IContextExtensions
{
    /// <summary>
    /// Gets the Playwright instance for direct access to Playwright APIs such as device descriptors.
    /// </summary>
    public static IPlaywright GetPlaywright(this TestFuzn.Context context)
    {
        return PlaywrightGlobalState.Playwright;
    }

    /// <summary>
    /// Creates a new isolated browser page with its own browser context.
    /// Each call creates a new context and page — they do not share cookies, storage, or session state.
    /// The browser context is accessible via <c>page.Context</c> for setting cookies, routes, credentials, etc.
    /// The framework automatically handles cleanup and failure diagnostics.
    /// When a device is specified (e.g. "iPhone 13"), its descriptor is used as the base context options.
    /// Options are layered: device defaults → global ConfigureContextOptions → per-call configureContext.
    /// </summary>
    public static async Task<IPage> CreateBrowserPage(this TestFuzn.Context context,
        string? browserType = null,
        string? device = null,
        Action<BrowserNewContextOptions>? configureContext = null)
    {
        var playwrightManager = GetPlaywrightManager(context);
        return await playwrightManager.CreatePage(browserType, device, configureContext);
    }

    private static PlaywrightManager GetPlaywrightManager(TestFuzn.Context context)
    {
        return (PlaywrightManager) context.Internals.Plugins.GetState(typeof(PlaywrightPlugin));
    }
}
