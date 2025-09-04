using Fuzn.TestFuzn.Contracts.Results.Feature;

namespace Fuzn.TestFuzn.Internals.Results.Feature;

internal class FeatureResultManager
{
    private static TestSuiteFeatureResult _testSuiteResult { get; } = new TestSuiteFeatureResult();

    public TestSuiteFeatureResult GetTestSuiteResults()
    {
        return _testSuiteResult;
    }

    internal void AddScenarioResults(string featureName, Dictionary<string, ScenarioFeatureResult> scenarioCollectors)
    {
        var featureResult = _testSuiteResult.FeatureResults.GetOrAdd(featureName, (key) => new FeatureResult(featureName));

        foreach (var scenarioCollector in scenarioCollectors)
        {
            if (!featureResult.ScenarioResults.TryAdd(scenarioCollector.Key, scenarioCollector.Value))
            {
                throw new Exception($"Scenario '{scenarioCollector.Key}' already exists in feature '{featureName}'.");
            }
        }
    }
}
