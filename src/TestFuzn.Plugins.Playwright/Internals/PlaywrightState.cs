using Microsoft.Playwright;

namespace Fuzn.TestFuzn.Plugins.Playwright.Internals;

internal class PlaywrightState
{
    public IBrowser Browser { get; set; }
    public IBrowserContext BrowserContext { get; set; }
}
