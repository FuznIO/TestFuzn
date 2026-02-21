using Fuzn.TestFuzn.Plugins.Playwright.Internals;

namespace Fuzn.TestFuzn.Plugins.Playwright;

/// <summary>
/// Extension methods for registering the Playwright plugin with the TestFuzn configuration.
/// </summary>
public static class TestFuznConfigurationExtensions
{
    /// <summary>
    /// Registers the Playwright plugin with the specified configuration.
    /// </summary>
    /// <param name="config">The TestFuzn configuration to extend.</param>
    /// <param name="playwrightConfigAction">An action to configure the Playwright plugin options.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="playwrightConfigAction"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the configuration is invalid (e.g. no browser types specified or an unsupported browser type).</exception>
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
