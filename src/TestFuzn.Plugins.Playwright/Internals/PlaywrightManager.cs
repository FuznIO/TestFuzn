using Microsoft.Playwright;

namespace Fuzn.TestFuzn.Plugins.Playwright.Internals;

internal class PlaywrightManager
{
    private static bool _isBrowserInitialized = false;
    private static IPlaywright _playwright;
    private static Dictionary<string, PlaywrightState> _state = new Dictionary<string, PlaywrightState>();
    private IList<IPage> _pages;

    public async static Task InitializeGlobalResources()
    {
        if (_isBrowserInitialized)
            return;

        if (PlaywrightGlobalState.Configuration.InstallPlaywright)
            Microsoft.Playwright.Program.Main(new[] { "install" }); // Ensures browsers are installed

        _playwright = await Microsoft.Playwright.Playwright.CreateAsync();

        foreach (var browserType in PlaywrightGlobalState.Configuration.BrowserTypesToUse)
        {
            var state = new PlaywrightState();
            state.Browser = await _playwright[browserType].LaunchAsync(new(new BrowserTypeLaunchOptions{Args = ["--disable-web-security", "--disable-features=IsolateOrigins,site-per-process"] }) { Headless = PlaywrightGlobalState.Configuration.Headless });
            state.BrowserContext = await state.Browser.NewContextAsync();
            _state.Add(browserType, state);
        }

        _isBrowserInitialized = true;
    }

    public async Task<IPage> CreatePage(string browserType = null)
    {
        if (browserType == null)
            browserType = PlaywrightGlobalState.Configuration.BrowserTypesToUse.First();

        if (!_isBrowserInitialized)
            throw new InvalidOperationException("PlaywrightManager not initialized. Call IniatializeGlobalResources() first.");

        if (_pages == null)
            _pages = new List<IPage>();

        if (!_state.ContainsKey(browserType))
            throw new KeyNotFoundException($"The browser type '{browserType}' is not available in the current Playwright state.");

        var browserContext = _state[browserType].BrowserContext;
        var page = await browserContext.NewPageAsync();
        _pages.Add(page);
        return page;
    }

    public async ValueTask CleanupContext()
    {
        if (_pages != null)
        {
            foreach (var page in _pages)
                await page.CloseAsync();
        }
    }

    internal static async Task CleanupGlobalResources()
    {
        foreach (var state in _state.Values)
        {
            if (state.BrowserContext != null)
                await state.BrowserContext.CloseAsync();
            if (state.Browser != null)
                await state.Browser.CloseAsync();
        }

        if (_playwright != null)
            _playwright.Dispose();
    }
}
