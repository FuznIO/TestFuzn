using Fuzn.TestFuzn.Plugins.Playwright.Internals;
using Microsoft.Extensions.DependencyInjection;

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
        Action<PlaywrightPluginConfiguration> playwrightConfigAction)
    {
        if (playwrightConfigAction is null)
            throw new ArgumentNullException(nameof(playwrightConfigAction));

        var playwrightConfiguration = new PlaywrightPluginConfiguration();
        playwrightConfigAction(playwrightConfiguration);

        ValidateConfiguration(playwrightConfiguration);

        config.Services.AddSingleton(playwrightConfiguration);

        var playwrightPlugin = new PlaywrightPlugin(playwrightConfiguration);
        config.Services.AddSingleton(playwrightPlugin);

        config.AddContextPlugin(playwrightPlugin);

        config.Services.AddTransient<PlaywrightManager>();
    }

    private static void ValidateConfiguration(PlaywrightPluginConfiguration config)
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
