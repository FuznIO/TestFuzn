namespace TestFusion.Contracts.Results.Feature;

public class FeatureResult(string name)
{
    public string Name { get; set; } = name;
    public List<ScenarioFeatureResult> ScenarioResults { get; set; } = new List<ScenarioFeatureResult>();
}