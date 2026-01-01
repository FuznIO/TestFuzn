
using Fuzn.TestFuzn.Plugins.Playwright.Internals;

namespace Fuzn.TestFuzn.Plugins.Playwright;

public static class TestFuznConfigurationExtensions
{
    public static void UsePlaywright(this TestFuznConfiguration config, 
        Action<PluginConfiguration> playwrightConfigAction)
    {
        if (playwrightConfigAction is null)
            throw new ArgumentNullException(nameof(playwrightConfigAction));

        var playwrightConfiguration = new PluginConfiguration();
        playwrightConfigAction(playwrightConfiguration);

        ValidateConfiguration(playwrightConfiguration);

        PlaywrightGlobalState.Configuration = playwrightConfiguration;

        config.AddContextPlugin(new PlaywrightPlugin());
    }

    private static void ValidateConfiguration(PluginConfiguration config)
    {
        if (config.BrowserTypes == null || !config.BrowserTypes.Any())
            throw new InvalidOperationException("At least one browser type must be specified in the Playwright configuration.");

        var validBrowserTypes = new HashSet<string> { "chromium", "firefox", "webkit" };
        foreach (var browserType in config.BrowserTypes)
        {
            if (!validBrowserTypes.Contains(browserType.ToLower()))
                throw new InvalidOperationException($"Invalid browser type '{browserType}' specified in the Playwright configuration. Valid options are: chromium, firefox, webkit.");
        }
    }
}
