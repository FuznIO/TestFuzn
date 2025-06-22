using TestFusion.Internals.InputData;
using TestFusion.Internals.State;
using TestFusion.Contracts.Adapters;
using TestFusion.Internals.Execution.Producers.Simulations;

namespace TestFusion.Internals.Init;

internal class InitManager
{
    private readonly ITestFrameworkAdapter _testFramework;
    private readonly SharedExecutionState _sharedExecutionState;
    private readonly InputDataFeeder _inputDataFeeder;

    public InitManager(ITestFrameworkAdapter testFramework, 
        SharedExecutionState sharedExecutionState,
        InputDataFeeder inputDataFeeder)
    {
        _testFramework = testFramework;
        _sharedExecutionState = sharedExecutionState;
        _inputDataFeeder = inputDataFeeder;
    }

    public async Task Run()
    {
        foreach (var scenario in _sharedExecutionState.Scenarios)
        {
            var startTime = DateTime.UtcNow;
            _sharedExecutionState.ResultState.FeatureCollectors[scenario.Name].InitStartTime = startTime;
            _sharedExecutionState.ResultState.FeatureCollectors[scenario.Name].InitStartTime = startTime;
        }

        await ExecuteInitBeforeScenarioTest();
        
        var initPerScenarioTasks = new List<Task>();

        foreach (var scenario in _sharedExecutionState.Scenarios)
            initPerScenarioTasks.Add(ExecuteInitOnScenario(scenario));
        
        await Task.WhenAll(initPerScenarioTasks);

         _inputDataFeeder.Init();

        await SetupSimulations();
    }

    private async Task ExecuteInitBeforeScenarioTest()
    {
        var context = ContextFactory.CreateContext(_testFramework, "InitScenarioTest");
        await _sharedExecutionState.IFeatureTestClassInstance.InitScenarioTest(context);
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

    private async Task SetupSimulations()
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
