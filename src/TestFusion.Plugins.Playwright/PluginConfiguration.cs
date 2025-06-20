namespace TestFusion.Plugins.Playwright;

public class PluginConfiguration
{
    public List<string> BrowserTypesToUse { get; set; } = new();
    public bool Headless { get; set; }
    public bool InstallPlaywright { get; set; }

    public PluginConfiguration()
    {
    }
}
