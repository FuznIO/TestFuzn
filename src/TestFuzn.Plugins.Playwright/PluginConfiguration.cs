using Microsoft.Playwright;

namespace Fuzn.TestFuzn.Plugins.Playwright;

public class PluginConfiguration
{
    public List<string> BrowserTypesToUse { get; set; } = new();
    public bool InstallPlaywright { get; set; }
    public Action<string, BrowserTypeLaunchOptions> ConfigureBrowserLaunchOptions { get; set; }
    public Action<string, BrowserNewContextOptions> ConfigureContextOptions { get; set; }
    public Func<string, IPage, Task> AfterPageCreated { get; set; }
    
    public PluginConfiguration()
    {
    }
}
