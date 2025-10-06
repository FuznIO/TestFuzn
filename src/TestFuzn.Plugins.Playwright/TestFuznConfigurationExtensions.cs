
using Fuzn.TestFuzn.Plugins.Playwright.Internals;

namespace Fuzn.TestFuzn.Plugins.Playwright;

public static class TestFuznConfigurationExtensions
{
    public static void UsePlaywright(this TestFuznConfiguration config)
    {
        var browserTestingConfiguration = new PluginConfiguration();
        ConfigurationManager.LoadPluginConfiguration("Playwright", browserTestingConfiguration);

        if (browserTestingConfiguration is null)
            throw new InvalidOperationException("Playwright configuration is not set in appsettings.json");

        PlaywrightGlobalState.Configuration = browserTestingConfiguration;

        config.AddContextPlugin(new PlaywrightPlugin());
    }

    public static void UsePlaywright(this TestFuznConfiguration config, 
        Action<PluginConfiguration> playwrightConfigAction)
    {
        if (playwrightConfigAction is null)
            throw new ArgumentNullException(nameof(playwrightConfigAction));

        var browserTestingConfiguration = new PluginConfiguration();
        PlaywrightGlobalState.Configuration = browserTestingConfiguration;
        playwrightConfigAction(browserTestingConfiguration);

        config.AddContextPlugin(new PlaywrightPlugin());
    }
}
