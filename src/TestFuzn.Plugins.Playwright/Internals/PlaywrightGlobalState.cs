using Microsoft.Playwright;

namespace Fuzn.TestFuzn.Plugins.Playwright.Internals;

internal class PlaywrightGlobalState
{
    public static PluginConfiguration Configuration { get; set; }
    public static IPlaywright Playwright { get; set; }
}
