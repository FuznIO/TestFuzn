namespace Fuzn.TestFuzn;

public class ScenarioInfo
{
    private readonly Scenario _scenario;

    public string Name => _scenario.Name;
    public string Id => _scenario.Id;

    internal ScenarioInfo(Scenario scenario)
    {
        _scenario = scenario;
    }
}
