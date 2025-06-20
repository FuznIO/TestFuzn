using TestFusion.Internals.Results.Load;
using TestFusion.Internals.State;
using TestFusion.Contracts.Adapters;

namespace TestFusion.Internals;

internal class ScenarioFinalizer
{
    private readonly ITestFrameworkAdapter _testFramework;
    private readonly SharedExecutionState _sharedExecutionState;
    private readonly LoadResultsManager _loadResultsManager;

    public ScenarioFinalizer(ITestFrameworkAdapter testFramework, 
        SharedExecutionState sharedExecutionState,
        LoadResultsManager loadResultsManager)
    {
        _testFramework = testFramework;
        _sharedExecutionState = sharedExecutionState;
        _loadResultsManager = loadResultsManager;
    }

    public void Complete()
    {
        _sharedExecutionState.ScenarioResult.MarkAsCompleted();

        if (_sharedExecutionState.TestType == TestType.Feature)
            return;

        if (_sharedExecutionState.ExecutionStatus != ExecutionStatus.Stopped)
        {
            foreach (var scenario in _sharedExecutionState.Scenarios)
            {
                var scenarioCollector = _loadResultsManager.GetScenarioCollector(scenario.Name);
                scenarioCollector.MarkAsCompleted();
                var scenarioResult = scenarioCollector.GetCurrentResult();
                if (scenario.AssertWhenDoneAction != null)
                {
                    try
                    {
                        var context = ContextFactory.CreateContext(_testFramework, "AssertWhenDoneAction");
                        scenario.AssertWhenDoneAction(context, new AssertScenarioStats(scenarioResult));
                    }
                    catch (Exception e)
                    {
                        _sharedExecutionState.FirstException = e;
                        scenarioCollector.AssertWhenDoneException(e);
                        scenarioCollector.SetStatus(ScenarioStatus.Failed);
                    }
                }
            }
        }
        _loadResultsManager.Complete();
    }
}
