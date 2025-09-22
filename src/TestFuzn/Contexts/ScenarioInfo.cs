namespace Fuzn.TestFuzn;

public class ScenarioInfo
{
    private readonly Scenario _scenario;

    public string Name => _scenario.Name;
    public string Id => _scenario.Id;

    public ScenarioInfo(Scenario scenario)
    {
        _scenario = scenario;
    }
}
