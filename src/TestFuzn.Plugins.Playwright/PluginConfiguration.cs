namespace Fuzn.TestFuzn.Plugins.Playwright;

public class PluginConfiguration
{
    public List<string> BrowserTypesToUse { get; set; } = new();
    public bool Headless { get; set; }
    public bool InstallPlaywright { get; set; }
    public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(10);

    public PluginConfiguration()
    {
    }
}
