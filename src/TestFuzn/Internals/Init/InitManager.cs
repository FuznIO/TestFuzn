using Fuzn.TestFuzn.Internals.InputData;
using Fuzn.TestFuzn.Internals.State;
using Fuzn.TestFuzn.Contracts.Adapters;
using Fuzn.TestFuzn.Internals.Execution.Producers.Simulations;
using Fuzn.TestFuzn.Internals.Execution;

namespace Fuzn.TestFuzn.Internals.Init;

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
            _sharedExecutionState.ResultState.FeatureCollectors[scenario.Name].MarkPhaseAsStarted(FeatureTestPhase.Init);
            _sharedExecutionState.ResultState.LoadCollectors[scenario.Name].MarkPhaseAsStarted(LoadTestPhase.Init);
        }

        await ExecuteInitTestMethod();
        
        var initPerScenarioTasks = new List<Task>();

        foreach (var scenario in _sharedExecutionState.Scenarios)
            initPerScenarioTasks.Add(ExecuteInitMethodsOnScenario(scenario));
        
        await Task.WhenAll(initPerScenarioTasks);

         _inputDataFeeder.Init();

        await SetupSimulations();

        foreach (var scenario in _sharedExecutionState.Scenarios)
        {
            _sharedExecutionState.ResultState.FeatureCollectors[scenario.Name].MarkPhaseAsCompleted(FeatureTestPhase.Init);
            _sharedExecutionState.ResultState.LoadCollectors[scenario.Name].MarkPhaseAsCompleted(LoadTestPhase.Init);
        }
    }

    private async Task ExecuteInitTestMethod()
    {
        var context = ContextFactory.CreateContext(_testFramework, "InitTestMethod");
        await _sharedExecutionState.IFeatureTestClassInstance.InitTestMethod(context);
    }

    private async Task ExecuteInitMethodsOnScenario(Scenario scenario)
    {
        if (scenario.InitScenario != null)
        {
            var context = ContextFactory.CreateScenarioContext(_testFramework, "InitScenario");
            await scenario.InitScenario(context);
        }

        if (!scenario.InputDataInfo.HasInputData)
            await Task.CompletedTask;

        if (scenario.InputDataInfo.SourceType == InputDataSourceType.Action)
        {
            var context = ContextFactory.CreateScenarioContext(_testFramework, "Inputs");
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
                        ContextFactory.CreateScenarioContext(_testFramework, "Warmup"),
                        new SimulationsBuilder(scenario, isWarmup: true));
                }

                await scenario.SimulationsAction(
                    ContextFactory.CreateScenarioContext(_testFramework, "Simulations"), 
                    new SimulationsBuilder(scenario, isWarmup: false));
            }

            if (scenario.SimulationsInternal.Count == 0)
                throw new InvalidOperationException($"Scenario '{scenario.Name}' has no load simulations defined. Please define at least one simulation.");
        }
    }
}
