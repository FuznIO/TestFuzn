using Microsoft.Playwright;

namespace TestFusion.Plugins.Playwright.Internals;

internal class PlaywrightState
{
    public IBrowser Browser { get; set; }
    public IBrowserContext BrowserContext { get; set; }
}
