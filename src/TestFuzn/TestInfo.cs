namespace Fuzn.TestFuzn;

public class TestInfo
{
    public string Name { get; set; }
    public string FullName { get; set; }
    public string Id { get; set; }
    public bool Skipped { get; set; }
    public string SkipReason { get; set; }
    public bool HasSkipAttribute { get; set; }
    public string SkipAttributeReason { get; set; }
    public string Description { get; set; }
    public GroupInfo Group { get; set; }
    public Dictionary<string, string> Metadata { get; set; }
    public List<string> Tags { get; set; }
    public List<string> TargetEnvironments { get; set; }
}
