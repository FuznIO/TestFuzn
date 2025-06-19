namespace TestFusion.Results.Feature;

public class FeatureResult(string name)
{
    public string Name { get; set; } = name;
    public List<ScenarioResult> ScenarioResults { get; set; } = new List<ScenarioResult>();
}