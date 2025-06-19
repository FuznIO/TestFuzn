using Microsoft.Playwright;

namespace TestFusion.BrowserTesting.Internals;

internal class PlaywrightState
{
    public IBrowser Browser { get; set; }
    public IBrowserContext BrowserContext { get; set; }
}
