using Fuzn.TestFuzn.Contracts.Results.Feature;
using Fuzn.TestFuzn.Internals.State;
using System.Collections.Generic;

namespace Fuzn.TestFuzn.Internals.Results.Feature;

internal class FeatureResultManager
{
    private static SuiteResult _suiteResult { get; } = new SuiteResult();

    public SuiteResult GetSuiteResults()
    {
        return _suiteResult;
    }

    public void AddScenarioResults(SharedExecutionState sharedExecutionState)
    {
        var featureTest = sharedExecutionState.IFeatureTestClassInstance;
        var scenarioCollectors = sharedExecutionState.ResultState.FeatureCollectors;

        var groupResult = _suiteResult.GroupResults.GetOrAdd(featureTest.TestInfo.Group.Name, 
                                (key) => new GroupResult(featureTest.TestInfo.Group.Name));

        var testResult = new TestResult();
        testResult.Name = featureTest.TestInfo.Name;
        // TODO
        //testResult.FullName = featureTest.Test.FullName;
        testResult.Id = featureTest.TestInfo.Id;
        testResult.Metadata = featureTest.TestInfo.Metadata;
        testResult.Tags = featureTest.TestInfo.Tags;

        if (sharedExecutionState.TestType == Contracts.TestType.Standard)
        {
            testResult.ScenarioResult = scenarioCollectors.First().Value;
            testResult.Duration = scenarioCollectors.First().Value.TestRunTotalDuration();
        }
        else
        {
            var duration = TimeSpan.Zero;

            foreach (var scenario in scenarioCollectors)
            {
                duration += scenario.Value.TestRunTotalDuration();
            }

            testResult.Duration = duration;
        }

        if (!groupResult.TestResults.TryAdd(testResult.Name, testResult))
        {
            throw new Exception($"Test name '{testResult.Name}' is duplicated, it already exists in feature '{featureTest.TestInfo.Group.Name}'.");
        }
    }
}
