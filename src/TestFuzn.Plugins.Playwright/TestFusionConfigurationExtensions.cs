
using FuznLabs.TestFuzn.Plugins.Playwright.Internals;

namespace FuznLabs.TestFuzn.Plugins.Playwright;

public static class TestFusionConfigurationExtensions
{
    public static void UsePlaywright(this TestFusionConfiguration config)
    {
        var browserTestingConfiguration = new PluginConfiguration();
        ConfigurationManager.LoadPluginConfiguration("Playwright", browserTestingConfiguration);

        if (browserTestingConfiguration is null)
            throw new InvalidOperationException("Playwright configuration is not set in appsettings.json");

        GlobalState.Configuration = browserTestingConfiguration;

        config.AddContextPlugin(new PlaywrightPlugin());
    }

    public static void UsePlaywright(this TestFusionConfiguration config, 
        Action<PluginConfiguration> playwrightConfigAction)
    {
        if (playwrightConfigAction is null)
            throw new ArgumentNullException(nameof(playwrightConfigAction));

        var browserTestingConfiguration = new PluginConfiguration();
        GlobalState.Configuration = browserTestingConfiguration;
        playwrightConfigAction(browserTestingConfiguration);

        config.AddContextPlugin(new PlaywrightPlugin());
    }
}
