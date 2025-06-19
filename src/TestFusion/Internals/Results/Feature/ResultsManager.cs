using TestFusion.Results.Feature;

namespace TestFusion.Internals.Results.Feature;

internal class ResultsManager
{
    private static TestSuiteResults _testSuiteResults { get; } = new TestSuiteResults();

    internal ScenarioResult CreateScenarioResult(string featureName, Scenario scenario)
    {
        var featureResult = _testSuiteResults.FeatureResults.GetOrAdd(featureName, new FeatureResult(featureName));

        var scenarioResult = new ScenarioResult(scenario);

        lock (featureResult.ScenarioResults)
            featureResult.ScenarioResults.Add(scenarioResult);

        return scenarioResult;
    }

    public TestSuiteResults GetTestSuiteResults()
    {
        return _testSuiteResults;
    }
}
