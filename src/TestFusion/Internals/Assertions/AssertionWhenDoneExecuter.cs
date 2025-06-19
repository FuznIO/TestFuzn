using TestFusion.Internals.Results.Load;
using TestFusion.Internals.State;

namespace TestFusion.Internals.Assertions;

internal class AssertionWhenDoneExecuter
{
    private readonly SharedExecutionState _sharedExecutionState;
    private readonly LoadResultsManager _loadResultsManager;

    public AssertionWhenDoneExecuter(SharedExecutionState sharedExecutionState, LoadResultsManager loadResultsManager)
    {
        _sharedExecutionState = sharedExecutionState;
        _loadResultsManager = loadResultsManager;
    }

    public void Execute()
    {
        foreach (var scenario in _sharedExecutionState.Scenarios)
        {
            var scenarioResult = _loadResultsManager.GetScenarioCollector(scenario.Name).GetCurrentResult(false);

            if (scenario.AssertWhenDoneAction != null)
                scenario.AssertWhenDoneAction(new AssertScenarioStats(scenarioResult));
        }
    }
}
