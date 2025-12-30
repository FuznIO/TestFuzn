using Fuzn.TestFuzn.Contracts.Results.Feature;

namespace Fuzn.TestFuzn.Internals.Results.Feature;

internal class FeatureResultManager
{
    private static TestSuiteFeatureResult _testSuiteResult { get; } = new TestSuiteFeatureResult();

    public TestSuiteFeatureResult GetTestSuiteResults()
    {
        return _testSuiteResult;
    }

    public void AddScenarioResults(IFeatureTest featureTest,
        Dictionary<string, ScenarioFeatureResult> scenarioCollectors)
    {
        var featureResult = _testSuiteResult.FeatureResults.GetOrAdd(featureTest.Group.Name, 
                                (key) => new GroupResult(featureTest.Group.Name, featureTest.Group.Id, featureTest.Group.Metadata));

        foreach (var scenarioCollector in scenarioCollectors)
        {
            if (!featureResult.TestResults.TryAdd(scenarioCollector.Key, scenarioCollector.Value))
            {
                throw new Exception($"Test name '{scenarioCollector.Key}' is duplicated, it already exists in feature '{featureTest.Group.Name}'.");
            }
        }
    }
}
