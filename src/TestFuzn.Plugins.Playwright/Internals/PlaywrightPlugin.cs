using System.Collections.Concurrent;
using Fuzn.TestFuzn.Contracts.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace Fuzn.TestFuzn.Plugins.Playwright.Internals;

internal class PlaywrightPlugin : IContextPlugin
{
    private readonly PlaywrightPluginConfiguration _configuration;
    private readonly ConcurrentDictionary<string, IBrowser> _browsers = new();

    public IPlaywright Playwright { get; private set; } = null!;
    public ConcurrentDictionary<string, IBrowser> Browsers => _browsers;
    public bool RequireState => true;
    public bool RequireStepExceptionHandling => true;

    public PlaywrightPlugin(PlaywrightPluginConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task InitSuite()
    {
        if (_configuration.InstallPlaywright)
            Microsoft.Playwright.Program.Main(new[] { "install" });

        Playwright = await Microsoft.Playwright.Playwright.CreateAsync();

        foreach (var browserType in _configuration.BrowserTypes)
        {
            var launchOptions = new BrowserTypeLaunchOptions();
            _configuration.ConfigureBrowserLaunchOptions?.Invoke(browserType, launchOptions);
            var browser = await Playwright[browserType].LaunchAsync(launchOptions);

            _browsers.TryAdd(browserType, browser);
        }
    }

    public async Task CleanupSuite()
    {
        foreach (var browser in _browsers.Values)
        {
            await browser.CloseAsync();
        }

        if (Playwright != null)
            Playwright.Dispose();
    }

    public object InitContext(IServiceProvider serviceProvider)
    {
        return serviceProvider.GetRequiredService<PlaywrightManager>();
    }

    public async Task CleanupContext(object state)
    {
        var playwrightManager = state as PlaywrightManager;
        if (playwrightManager == null)
            throw new InvalidOperationException("PlaywrightManager state is null in CleanupContext.");

        await playwrightManager.CleanupContext();
    }

    public async Task HandleStepException(object state, IterationContext context, Exception exception)
    {
        var playwrightManager = state as PlaywrightManager;
        if (playwrightManager == null)
            throw new InvalidOperationException("PlaywrightManager state is null in HandleStepException.");

        await playwrightManager.AddOrLogPageMetadata(context);
    }
}
