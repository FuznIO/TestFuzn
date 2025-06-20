using TestFusion.Internals.Producers.Simulations;
using TestFusion.Internals.State;
using TestFusion.Plugins.TestFrameworkProviders;

namespace TestFusion.Internals;

internal class ScenarioSimulationsSetup
{
    private readonly ITestFrameworkProvider _testFramework;
    private readonly SharedExecutionState _sharedExecutionState;

    public ScenarioSimulationsSetup(ITestFrameworkProvider testFramework, SharedExecutionState sharedExecutionState)
    {
        _testFramework = testFramework ?? throw new ArgumentNullException(nameof(testFramework));
        _sharedExecutionState = sharedExecutionState ?? throw new ArgumentNullException(nameof(sharedExecutionState));
    }

    public async Task Setup()
    {
        foreach (var scenario in _sharedExecutionState.Scenarios)
        {
            if (_sharedExecutionState.TestType == TestType.Feature)
            {
                var totalExecutions = 1;
                if (scenario.InputDataInfo.HasInputData)
                    totalExecutions = scenario.InputDataInfo.InputDataList.Count;

                scenario.SimulationsInternal.Add(new FixedConcurrentLoadConfiguration(1, totalExecutions));
            }
            else if (_sharedExecutionState.TestType == TestType.Load)
            {
                var context = ContextFactory.CreateContext(_testFramework, "Simulations");
                var simulationsBuilder = new SimulationsBuilder(scenario);
                await scenario.Simulations(context, simulationsBuilder);
            }

            if (scenario.SimulationsInternal.Count == 0)
                throw new InvalidOperationException($"Scenario '{scenario.Name}' has no load simulations defined. Please define at least one simulation.");
        }
    }
}
