using Microsoft.Playwright;

namespace Fuzn.TestFuzn.Plugins.Playwright.Internals;

internal class PlaywrightManager
{
    private readonly PlaywrightPluginConfiguration _configuration;
    private readonly PlaywrightPlugin _plugin;
    private readonly object _contextsLock = new();
    private readonly List<IBrowserContext> _contexts = new();

    public IPlaywright Playwright => _plugin.Playwright;

    public PlaywrightManager(PlaywrightPluginConfiguration configuration, PlaywrightPlugin plugin)
    {
        _configuration = configuration;
        _plugin = plugin;
    }

    public async Task<IPage> CreatePage(
        string? browserType = null,
        string? device = null,
        Action<BrowserNewContextOptions>? configureBrowserContext = null)
    {
        if (browserType == null)
            browserType = _configuration.BrowserTypes.First();

        if (!_plugin.Browsers.TryGetValue(browserType, out var browser))
            throw new KeyNotFoundException($"The browser type '{browserType}' is not available in the current Playwright state.");

        var options = device != null
            ? _plugin.Playwright.Devices[device]
            : new BrowserNewContextOptions();

        _configuration.ConfigureBrowserContextOptions?.Invoke(browserType, options);

        configureBrowserContext?.Invoke(options);

        var browserContext = await browser.NewContextAsync(options);

        if (_configuration.AfterBrowserContextCreated != null)
            await _configuration.AfterBrowserContextCreated(browserType, browserContext);

        var page = await browserContext.NewPageAsync();

        if (_configuration.AfterBrowserPageCreated != null)
            await _configuration.AfterBrowserPageCreated(browserType, page);

        lock (_contextsLock)
        {
            _contexts.Add(browserContext);
        }

        return page;
    }

    public async ValueTask AddOrLogPageMetadata(IterationContext context)
    {
        if (_contexts.Count == 0)
            return;

        foreach (var browserContext in _contexts)
        {
            foreach (var page in browserContext.Pages)
            {
                var title = await page.TitleAsync();
                var url = page.Url;
                context.Comment($"Playwright Page - Title: '{title}', URL: '{url}'");
                var html = await page.EvaluateAsync<string>("() => document.documentElement.outerHTML");
                await context.Attach("playwright-page-html", html);
                var screenshot = await page.ScreenshotAsync(new PageScreenshotOptions { FullPage = true, Type = ScreenshotType.Png });
                await context.Attach("playwright-page-screenshot.png", screenshot);
            }
        }
    }

    public async ValueTask CleanupIteration()
    {
        if (_contexts.Count == 0)
            return;

        foreach (var browserContext in _contexts)
        {
            await browserContext.CloseAsync();
        }
    }
}
