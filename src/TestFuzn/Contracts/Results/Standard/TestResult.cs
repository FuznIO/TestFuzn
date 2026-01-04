namespace Fuzn.TestFuzn.Contracts.Results.Standard;

internal class TestResult
{
    public string Name { get; set; }
    public string FullName { get; set; }
    public string Id { get; set; }
    public Dictionary<string, string> Metadata { get; set; }
    public List<string> Tags { get; set; }
    public TimeSpan Duration { get; set; }
    public ScenarioStandardResult ScenarioResult { get; set; }
    public TestStatus Status 
    {
        get
        {
            if (ScenarioResult == null)
                return field;
            return ScenarioResult.Status;
        }
        set
        {
            field = value;
        }
    }
    
}