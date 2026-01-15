using Microsoft.Playwright;

namespace Fuzn.TestFuzn.Plugins.Playwright.Internals;

internal class PlaywrightManager
{
    private static bool _isBrowserInitialized = false;
    private static IPlaywright _playwright;
    private static Dictionary<string, IBrowser> _browsers = new Dictionary<string, IBrowser>();
    private IList<(IPage Page, IBrowserContext Context)> _pageContexts;

    public async static Task InitializeGlobalResources()
    {
        if (_isBrowserInitialized)
            return;

        if (PlaywrightGlobalState.Configuration.InstallPlaywright)
            Microsoft.Playwright.Program.Main(new[] { "install" }); // Ensures browsers are installed

        _playwright = await Microsoft.Playwright.Playwright.CreateAsync();

        foreach (var browserType in PlaywrightGlobalState.Configuration.BrowserTypes)
        {
            if (PlaywrightGlobalState.Configuration.ConfigureBrowserLaunchOptions == null)
                throw new InvalidOperationException("ConfigureBrowserLaunchOptions must be set in Playwright configuration.");

            var launchOptions = new BrowserTypeLaunchOptions();
            PlaywrightGlobalState.Configuration.ConfigureBrowserLaunchOptions(browserType, launchOptions);
            var browser = await _playwright[browserType].LaunchAsync(launchOptions);

            _browsers.Add(browserType, browser);
        }

        _isBrowserInitialized = true;
    }

    public async Task<IPage> CreatePage(string? browserType = null)
    {
        if (browserType == null)
            browserType = PlaywrightGlobalState.Configuration.BrowserTypes.First();

        if (!_isBrowserInitialized)
            throw new InvalidOperationException("Playwright is not initialized. Call configuration.AddPlaywright() in the Startup.cs");

        _pageContexts ??= new List<(IPage, IBrowserContext)>();

        if (!_browsers.ContainsKey(browserType))
            throw new KeyNotFoundException($"The browser type '{browserType}' is not available in the current Playwright state.");

        var browser = _browsers[browserType];

        IBrowserContext browserContext;
        if (PlaywrightGlobalState.Configuration.ConfigureContextOptions != null)
        {
            var options = new BrowserNewContextOptions();
            PlaywrightGlobalState.Configuration.ConfigureContextOptions(browserType, options);
            browserContext = await browser.NewContextAsync(options);
        }
        else
        {
            browserContext = await browser.NewContextAsync();
        }

        var page = await browserContext.NewPageAsync();
        if (PlaywrightGlobalState.Configuration.AfterPageCreated != null)
            await PlaywrightGlobalState.Configuration.AfterPageCreated(browserType, page);

        _pageContexts.Add((page, browserContext));
        return page;
    }

    public async ValueTask CleanupContext()
    {
        if (_pageContexts == null)
            return;

        foreach (var (page, context) in _pageContexts)
        {
            await page.CloseAsync();
            await context.CloseAsync();
        }
    }

    public async ValueTask AddOrLogPageMetadata(IterationContext context)
    {
        if (_pageContexts == null)
            return;

        foreach (var (page, _) in _pageContexts)
        {
            var title = await page.TitleAsync();
            var url = page.Url;
            context.Comment($"Playwright Page - Title: '{title}', URL: '{url}'");
            var html = await page.EvaluateAsync<string>("() => document.documentElement.outerHTML");
            await context.Attach($"playwright-page-html", html);
            var screenshot = await page.ScreenshotAsync(new PageScreenshotOptions { FullPage = true, Type = ScreenshotType.Png });
            await context.Attach("playwright-page-screenshot.png", screenshot);
        }
    }

    internal static async Task CleanupGlobalResources()
    {
        foreach (var browser in _browsers.Values)
        {
            await browser.CloseAsync();
        }

        if (_playwright != null)
            _playwright.Dispose();
    }
}
