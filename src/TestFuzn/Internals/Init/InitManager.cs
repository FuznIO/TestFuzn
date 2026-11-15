using Fuzn.TestFuzn.Contracts;
using Fuzn.TestFuzn.Internals.Cluster;
using Fuzn.TestFuzn.Internals.Execution;
using Fuzn.TestFuzn.Internals.Execution.Producers.Simulations;
using Fuzn.TestFuzn.Internals.InputData;
using Fuzn.TestFuzn.Internals.State;

namespace Fuzn.TestFuzn.Internals.Init;

internal class InitManager
{
    private readonly IServiceProvider _serviceProvider;
    private TestExecutionState _testExecutionState;
    private InputDataFeeder _inputDataFeeder;

    public InitManager(
        IServiceProvider serviceProvider,
        TestExecutionState testExecutionState,
        InputDataFeeder inputDataFeeder)
    {
        _serviceProvider = serviceProvider;
        _testExecutionState = testExecutionState;
        _inputDataFeeder = inputDataFeeder;
    }

    public async Task Run()
    {
        var startedTimestamp = DateTime.UtcNow;

        foreach (var scenario in _testExecutionState.Scenarios)
        {       
            _testExecutionState.LoadCollectors[scenario.Name].MarkPhaseAsStarted(LoadTestPhase.Init, startedTimestamp);
        }

        var cluster = _testExecutionState.TestSession.Cluster;
        if (cluster.Role == ClusterRole.LoadChild)
            OnceHooks.ApplyOnceData(_testExecutionState);
        else if (_testExecutionState.TestType == TestType.Load)
            await OnceHooks.RunBefore(_testExecutionState, _serviceProvider);

        await ExecuteBeforeTestMethod();
        
        var initPerScenarioTasks = new List<Task>();

        foreach (var scenario in _testExecutionState.Scenarios)
            initPerScenarioTasks.Add(ExecuteBeforeScenarioAndInputData(scenario));
        
        await Task.WhenAll(initPerScenarioTasks);

        _inputDataFeeder.Init(_testExecutionState);

        if (cluster.Role == ClusterRole.LoadChild)
            RecordInputDataFingerprints();

        await SetupSimulations();

        // The descriptions keep the total load; the split below only changes what this worker runs.
        foreach (var scenario in _testExecutionState.Scenarios)
            _testExecutionState.LoadCollectors[scenario.Name].CacheSimulationDescriptions(scenario);

        if (cluster.WorkerCount > 1 && _testExecutionState.TestType == TestType.Load)
            SplitSimulations(cluster);

        var timestamp = DateTime.UtcNow;

        foreach (var scenario in _testExecutionState.Scenarios)
            _testExecutionState.LoadCollectors[scenario.Name].MarkPhaseAsCompleted(LoadTestPhase.Init, timestamp);

        _testExecutionState.TestResult.MarkPhaseAsCompleted(StandardTestPhase.Init, timestamp);
    }

    private async Task ExecuteBeforeTestMethod()
    {
        if (_testExecutionState.TestClassInstance is IBeforeTest testClassInstance)
        {
            var context = ContextFactory.CreateContext(_testExecutionState.TestSession, _serviceProvider, _testExecutionState.TestFramework, "BeforeTest", _testExecutionState.CancellationToken);
            await testClassInstance.BeforeTest(context);
        }
    }

    private async Task ExecuteBeforeScenarioAndInputData(Scenario scenario)
    {
        if (scenario.BeforeScenario != null)
        {
            var context = ContextFactory.CreateScenarioContext(_testExecutionState.TestSession, _serviceProvider, _testExecutionState.TestFramework, "BeforeScenario", _testExecutionState.CancellationToken, scenario);
            await scenario.BeforeScenario(context);
        }

        if (!scenario.InputDataInfo.HasInputData)
            await Task.CompletedTask;

        if (scenario.InputDataInfo.SourceType == InputDataSourceType.Action)
        {
            var context = ContextFactory.CreateScenarioContext(_testExecutionState.TestSession, _serviceProvider, _testExecutionState.TestFramework, "InputData", _testExecutionState.CancellationToken, scenario);
            scenario.InputDataInfo.InputDataList = await scenario.InputDataInfo.InputDataAction(context);
        }
    }

    private void RecordInputDataFingerprints()
    {
        foreach (var scenario in _testExecutionState.Scenarios)
        {
            var inputData = scenario.InputDataInfo.InputDataList;
            if (!scenario.InputDataInfo.HasInputData || inputData == null)
                continue;

            _testExecutionState.LoadCollectors[scenario.Name].SetInputDataInfo(inputData.Count, InputDataFingerprint.Hash(inputData));
        }
    }

    private void SplitSimulations(ClusterSessionInfo cluster)
    {
        foreach (var scenario in _testExecutionState.Scenarios)
        {
            var parts = scenario.SimulationsInternal
                .Select(simulation => simulation.Split(cluster.WorkerIndex, cluster.WorkerCount, cluster.Seed))
                .ToList();

            scenario.SimulationsInternal.Clear();
            scenario.SimulationsInternal.AddRange(parts);
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
                        ContextFactory.CreateScenarioContext(_testExecutionState.TestSession, _serviceProvider, _testExecutionState.TestFramework, "Warmup", _testExecutionState.CancellationToken, scenario),
                        new SimulationsBuilder(scenario, isWarmup: true));
                }

                await scenario.SimulationsAction(
                    ContextFactory.CreateScenarioContext(_testExecutionState.TestSession, _serviceProvider, _testExecutionState.TestFramework, "Simulations", _testExecutionState.CancellationToken, scenario),
                    new SimulationsBuilder(scenario, isWarmup: false));
            }

            if (scenario.SimulationsInternal.Count == 0)
                throw new InvalidOperationException($"Scenario '{scenario.Name}' has no load simulations defined. Please define at least one simulation.");
        }
    }
}
