using TestFusion.Internals.Results.Load;
using TestFusion.Internals.State;
using TestFusion.Plugins.TestFrameworkProviders;

namespace TestFusion.Internals.Assertions;

internal class AssertionWhenDoneExecuter
{
    private readonly ITestFrameworkProvider _testFramework;
    private readonly SharedExecutionState _sharedExecutionState;
    private readonly LoadResultsManager _loadResultsManager;

    public AssertionWhenDoneExecuter(ITestFrameworkProvider testFramework, SharedExecutionState sharedExecutionState, LoadResultsManager loadResultsManager)
    {
        _testFramework = testFramework;
        _sharedExecutionState = sharedExecutionState;
        _loadResultsManager = loadResultsManager;
    }

    public void Execute()
    {
        foreach (var scenario in _sharedExecutionState.Scenarios)
        {
            var scenarioResult = _loadResultsManager.GetScenarioCollector(scenario.Name).GetCurrentResult(false);

            if (scenario.AssertWhenDoneAction != null)
            {
                var context = ContextFactory.CreateContext(_testFramework, "AssertWhenDone");
                scenario.AssertWhenDoneAction(context, new AssertScenarioStats(scenarioResult));
            }
        }
    }
}
