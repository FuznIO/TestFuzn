using TestFusion.Contracts.Results.Feature;

namespace TestFusion.Internals.Results.Feature;

internal class FeatureResultsManager
{
    private static TestSuiteFeatureResult _testSuiteResult { get; } = new TestSuiteFeatureResult();

    internal ScenarioFeatureResult CreateScenarioResult(string featureName, Scenario scenario)
    {
        var featureResult = _testSuiteResult.FeatureResults.GetOrAdd(featureName, new FeatureResult(featureName));

        var scenarioResult = new ScenarioFeatureResult(scenario);

        lock (featureResult.ScenarioResults)
            featureResult.ScenarioResults.Add(scenarioResult);

        return scenarioResult;
    }

    public TestSuiteFeatureResult GetTestSuiteResults()
    {
        return _testSuiteResult;
    }
}
