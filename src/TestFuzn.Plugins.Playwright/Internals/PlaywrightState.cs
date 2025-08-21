using Microsoft.Playwright;

namespace FuznLabs.TestFuzn.Plugins.Playwright.Internals;

internal class PlaywrightState
{
    public IBrowser Browser { get; set; }
    public IBrowserContext BrowserContext { get; set; }
}
