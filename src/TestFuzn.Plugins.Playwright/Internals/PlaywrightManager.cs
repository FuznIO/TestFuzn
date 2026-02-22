using System.Collections.Concurrent;
using Microsoft.Playwright;

namespace Fuzn.TestFuzn.Plugins.Playwright.Internals;

internal class PlaywrightManager
{
    private static readonly ConcurrentDictionary<string, IBrowser> _browsers = new();
    private readonly object _contextsLock = new();
    private readonly List<IBrowserContext> _contexts = new();

    public static async Task InitializeGlobalResources()
    {
        if (PlaywrightGlobalState.Configuration.InstallPlaywright)
            Microsoft.Playwright.Program.Main(new[] { "install" });

        PlaywrightGlobalState.Playwright = await Microsoft.Playwright.Playwright.CreateAsync();

        foreach (var browserType in PlaywrightGlobalState.Configuration.BrowserTypes)
        {
            var launchOptions = new BrowserTypeLaunchOptions();
            PlaywrightGlobalState.Configuration.ConfigureBrowserLaunchOptions?.Invoke(browserType, launchOptions);
            var browser = await PlaywrightGlobalState.Playwright[browserType].LaunchAsync(launchOptions);

            _browsers.TryAdd(browserType, browser);
        }
    }

    public async Task<IPage> CreatePage(
        string? browserType = null,
        string? device = null,
        Action<BrowserNewContextOptions>? configureBrowserContext = null)
    {
        if (browserType == null)
            browserType = PlaywrightGlobalState.Configuration.BrowserTypes.First();

        if (!_browsers.TryGetValue(browserType, out var browser))
            throw new KeyNotFoundException($"The browser type '{browserType}' is not available in the current Playwright state.");

        var options = device != null
            ? PlaywrightGlobalState.Playwright.Devices[device]
            : new BrowserNewContextOptions();

        PlaywrightGlobalState.Configuration.ConfigureBrowserContextOptions?.Invoke(browserType, options);

        configureBrowserContext?.Invoke(options);

        var browserContext = await browser.NewContextAsync(options);

        if (PlaywrightGlobalState.Configuration.AfterBrowserContextCreated != null)
            await PlaywrightGlobalState.Configuration.AfterBrowserContextCreated(browserType, browserContext);

        var page = await browserContext.NewPageAsync();

        if (PlaywrightGlobalState.Configuration.AfterBrowserPageCreated != null)
            await PlaywrightGlobalState.Configuration.AfterBrowserPageCreated(browserType, page);

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

    public async ValueTask CleanupContext()
    {
        if (_contexts.Count == 0)
            return;

        foreach (var browserContext in _contexts)
        {
            await browserContext.CloseAsync();
        }
    }

    public static async Task CleanupGlobalResources()
    {
        foreach (var browser in _browsers.Values)
        {
            await browser.CloseAsync();
        }

        if (PlaywrightGlobalState.Playwright != null)
            PlaywrightGlobalState.Playwright.Dispose();
    }
}
