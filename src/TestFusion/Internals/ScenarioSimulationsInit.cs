using TestFusion.Internals.Execution.Producers.Simulations;
using TestFusion.Internals.State;
using TestFusion.Contracts.Adapters;

namespace TestFusion.Internals;

internal class ScenarioSimulationsInit
{
    private readonly ITestFrameworkAdapter _testFramework;
    private readonly SharedExecutionState _sharedExecutionState;

    public ScenarioSimulationsInit(ITestFrameworkAdapter testFramework, SharedExecutionState sharedExecutionState)
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
                if (scenario.WarmupAction != null)
                {
                    await scenario.WarmupAction(
                        ContextFactory.CreateContext(_testFramework, "Warmup"),
                        new SimulationsBuilder(scenario, isWarmup: true));
                }

                await scenario.SimulationsAction(
                    ContextFactory.CreateContext(_testFramework, "Simulations"), 
                    new SimulationsBuilder(scenario, isWarmup: false));
            }

            if (scenario.SimulationsInternal.Count == 0)
                throw new InvalidOperationException($"Scenario '{scenario.Name}' has no load simulations defined. Please define at least one simulation.");
        }
    }
}
