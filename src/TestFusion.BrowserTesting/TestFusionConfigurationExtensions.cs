
using TestFusion.BrowserTesting.Internals;

namespace TestFusion.BrowserTesting;

public static class TestFusionConfigurationExtensions
{
    public static void UseBrowserTesting(this TestFusionConfiguration config)
    {
        var browserTestingConfiguration = new BrowserTestingConfiguration();
        ConfigurationManager.LoadPluginConfiguration("BrowserTesting", browserTestingConfiguration);

        if (browserTestingConfiguration is null)
            throw new InvalidOperationException("BrowserTesting configuration is not set in appsettings.json");

        GlobalState.Configuration = browserTestingConfiguration;

        config.AddContextPlugin(new BrowserTestingPlugin());
    }

    public static void UseBrowserTesting(this TestFusionConfiguration config, 
        Action<BrowserTestingConfiguration> browserTestingConfigAction)
    {
        if (browserTestingConfigAction is null)
            throw new ArgumentNullException(nameof(browserTestingConfigAction));

        var browserTestingConfiguration = new BrowserTestingConfiguration();
        GlobalState.Configuration = browserTestingConfiguration;
        browserTestingConfigAction(browserTestingConfiguration);

        config.AddContextPlugin(new BrowserTestingPlugin());
    }
}
