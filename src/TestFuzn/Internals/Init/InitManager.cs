using Fuzn.TestFuzn.Contracts;
using Fuzn.TestFuzn.Contracts.Adapters;
using Fuzn.TestFuzn.Internals.Execution;
using Fuzn.TestFuzn.Internals.Execution.Producers.Simulations;
using Fuzn.TestFuzn.Internals.InputData;
using Fuzn.TestFuzn.Internals.State;
using HdrHistogram;

namespace Fuzn.TestFuzn.Internals.Init;

internal class InitManager
{
    private readonly ITestFrameworkAdapter _testFramework;
    private readonly TestExecutionState _testExecutionState;
    private readonly InputDataFeeder _inputDataFeeder;

    public InitManager(ITestFrameworkAdapter testFramework, 
        TestExecutionState testExecutionState,
        InputDataFeeder inputDataFeeder)
    {
        _testFramework = testFramework;
        _testExecutionState = testExecutionState;
        _inputDataFeeder = inputDataFeeder;
    }

    public async Task Run()
    {
        var startedTimestamp = DateTime.UtcNow;

        _testExecutionState.TestResult.MarkPhaseAsStarted(StandardTestPhase.Init, startedTimestamp);

        foreach (var scenario in _testExecutionState.Scenarios)
        {       
            _testExecutionState.LoadCollectors[scenario.Name].MarkPhaseAsStarted(LoadTestPhase.Init, startedTimestamp);
        }

        await ExecuteBeforeTestMethod();
        
        var initPerScenarioTasks = new List<Task>();

        foreach (var scenario in _testExecutionState.Scenarios)
            initPerScenarioTasks.Add(ExecuteBeforeScenarioAndInputData(scenario));
        
        await Task.WhenAll(initPerScenarioTasks);

         _inputDataFeeder.Init();

        await SetupSimulations();

        var timestamp = DateTime.UtcNow;

        foreach (var scenario in _testExecutionState.Scenarios)
            _testExecutionState.LoadCollectors[scenario.Name].MarkPhaseAsCompleted(LoadTestPhase.Init, timestamp);

        _testExecutionState.TestResult.MarkPhaseAsCompleted(StandardTestPhase.Init, timestamp);
    }

    private async Task ExecuteBeforeTestMethod()
    {
        if (_testExecutionState.TestClassInstance is IBeforeTest testClassInstance)
        {
            var context = ContextFactory.CreateContext(_testFramework, "BeforeTest");
            await testClassInstance.BeforeTest(context);
        }
    }

    private async Task ExecuteBeforeScenarioAndInputData(Scenario scenario)
    {
        if (scenario.BeforeScenario != null)
        {
            var context = ContextFactory.CreateScenarioContext(_testFramework, "BeforeScenario");
            await scenario.BeforeScenario(context);
        }

        if (!scenario.InputDataInfo.HasInputData)
            await Task.CompletedTask;

        if (scenario.InputDataInfo.SourceType == InputDataSourceType.Action)
        {
            var context = ContextFactory.CreateScenarioContext(_testFramework, "InputData");
            scenario.InputDataInfo.InputDataList = await scenario.InputDataInfo.InputDataAction(context);
        }
    }

    private async Task SetupSimulations()
    {
        foreach (var scenario in _testExecutionState.Scenarios)
        {
            if (_testExecutionState.TestResult.TestType == TestType.Standard)
            {
                var totalExecutions = 1;
                if (scenario.InputDataInfo.HasInputData)
                    totalExecutions = scenario.InputDataInfo.InputDataList.Count;

                scenario.SimulationsInternal.Add(new FixedConcurrentLoadConfiguration(1, totalExecutions));
            }
            else if (_testExecutionState.TestResult.TestType == TestType.Load)
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
