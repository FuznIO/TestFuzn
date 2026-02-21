using Microsoft.Playwright;

namespace Fuzn.TestFuzn.Plugins.Playwright;

public class PluginConfiguration
{
    public List<string> BrowserTypes { get; set; } = new();
    public bool InstallPlaywright { get; set; }
    public bool EnableTracing { get; set; }
    public Action<string, BrowserTypeLaunchOptions> ConfigureBrowserLaunchOptions { get; set; }
    public Action<string, BrowserNewContextOptions> ConfigureBrowserContextOptions { get; set; }
    public Func<string, IBrowserContext, Task> AfterBrowserContextCreated { get; set; }
    public Func<string, IPage, Task> AfterBrowserPageCreated { get; set; }
    
    public PluginConfiguration()
    {
    }
}
