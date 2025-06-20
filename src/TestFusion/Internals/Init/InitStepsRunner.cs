using TestFusion.Internals.InputData;
using TestFusion.Internals.State;
using TestFusion.Contracts.Adapters;

namespace TestFusion.Internals.Init;

internal class InitStepsRunner
{
    private readonly ITestFrameworkAdapter _testFramework;
    private readonly SharedExecutionState _sharedExecutionState;

    public InitStepsRunner(ITestFrameworkAdapter testFramework, SharedExecutionState sharedExecutionState)
    {
        _testFramework = testFramework;
        _sharedExecutionState = sharedExecutionState;
    }

    public async Task Run()
    {
        await ExecuteInitBeforeScenarioTest();        
        
        var initPerScenarioTasks = new List<Task>();

        foreach (var scenario in _sharedExecutionState.Scenarios)
            initPerScenarioTasks.Add(ExecuteInitOnScenario(scenario));
        
        await Task.WhenAll(initPerScenarioTasks);
    }

    private async Task ExecuteInitBeforeScenarioTest()
    {
        var context = ContextFactory.CreateContext(_testFramework, "BeforeEachScenarioTest");
        await _sharedExecutionState.FeatureTestClassInstance.InitScenarioTest(context);
    }

    private async Task ExecuteInitOnScenario(Scenario scenario)
    {
        if (scenario.Init != null)
        {
            var context = ContextFactory.CreateContext(_testFramework, "Init");
            await scenario.Init(context);
        }

        if (!scenario.InputDataInfo.HasInputData)
            await Task.CompletedTask;

        if (scenario.InputDataInfo.SourceType == InputDataSourceType.Action)
        {
            var context = ContextFactory.CreateContext(_testFramework, "Inputs");
            scenario.InputDataInfo.InputDataList = await scenario.InputDataInfo.InputDataAction(context);
        }
    }
}
