namespace TestFusion.BrowserTesting;

public class BrowserTestingConfiguration
{
    private string _defaultBrowserToUse;

    public List<string> BrowserTypesToUse { get; set; } = new();
    public bool Headless { get; set; }
    public bool InstallPlaywright { get; set; }

    public BrowserTestingConfiguration()
    {
    }
}
