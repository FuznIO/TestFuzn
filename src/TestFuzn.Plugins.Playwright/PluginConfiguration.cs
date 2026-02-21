using Microsoft.Playwright;

namespace Fuzn.TestFuzn.Plugins.Playwright;

/// <summary>
/// Configuration options for the Playwright plugin.
/// </summary>
public class PluginConfiguration
{
    /// <summary>
    /// Gets or sets the list of browser types to use (e.g. "chromium", "firefox", "webkit").
    /// </summary>
    public List<string> BrowserTypes { get; set; } = new();

    /// <summary>
    /// Gets or sets a value indicating whether Playwright browsers should be installed automatically during initialization.
    /// </summary>
    public bool InstallPlaywright { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether tracing should be enabled for browser contexts.
    /// When enabled, traces including screenshots, snapshots, and sources are captured.
    /// </summary>
    public bool EnableTracing { get; set; }

    /// <summary>
    /// Gets or sets a callback to configure <see cref="BrowserTypeLaunchOptions"/> for each browser type.
    /// The first parameter is the browser type name; the second is the launch options to configure.
    /// </summary>
    /// <example>
    /// <code>
    /// ConfigureBrowserLaunchOptions = (browserType, launchOptions) =>
    /// {
    ///     launchOptions.Headless = true;
    /// };
    /// </code>
    /// </example>
    public Action<string, BrowserTypeLaunchOptions> ConfigureBrowserLaunchOptions { get; set; }

    /// <summary>
    /// Gets or sets a callback to configure <see cref="BrowserNewContextOptions"/> for each browser type.
    /// The first parameter is the browser type name; the second is the context options to configure.
    /// </summary>
    /// <example>
    /// <code>
    /// ConfigureBrowserContextOptions = (browserType, contextOptions) =>
    /// {
    ///     contextOptions.IgnoreHTTPSErrors = true;
    /// };
    /// </code>
    /// </example>
    public Action<string, BrowserNewContextOptions> ConfigureBrowserContextOptions { get; set; }

    /// <summary>
    /// Gets or sets a callback invoked after a new <see cref="IBrowserContext"/> is created.
    /// The first parameter is the browser type name; the second is the created browser context.
    /// </summary>
    /// <example>
    /// <code>
    /// AfterBrowserContextCreated = async (browserType, browserContext) =>
    /// {
    ///     await browserContext.AddCookiesAsync(cookies);
    /// };
    /// </code>
    /// </example>
    public Func<string, IBrowserContext, Task> AfterBrowserContextCreated { get; set; }

    /// <summary>
    /// Gets or sets a callback invoked after a new <see cref="IPage"/> is created.
    /// The first parameter is the browser type name; the second is the created page.
    /// </summary>
    /// <example>
    /// <code>
    /// AfterBrowserPageCreated = (browserType, page) =>
    /// {
    ///     page.SetDefaultTimeout(10000);
    ///     return Task.CompletedTask;
    /// };
    /// </code>
    /// </example>
    public Func<string, IPage, Task> AfterBrowserPageCreated { get; set; }

    internal PluginConfiguration()
    {
    }
}
