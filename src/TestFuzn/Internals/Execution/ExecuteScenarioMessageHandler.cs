using Fuzn.TestFuzn.Contracts;
using Fuzn.TestFuzn.Contracts.Results.Standard;
using Fuzn.TestFuzn.Contracts.Results.Load;
using Fuzn.TestFuzn.Contracts.Sinks;
using Fuzn.TestFuzn.Internals.State;
using System.Diagnostics;
using Fuzn.TestFuzn.Internals.InputData;
using Microsoft.Extensions.DependencyInjection;

namespace Fuzn.TestFuzn.Internals.Execution;

internal class ExecuteScenarioMessageHandler
{
    private readonly IServiceProvider _serviceProvider;
    private readonly TestExecutionState _testExecutionState;
    private readonly IEnumerable<ISinkPlugin> _sinkPlugins;
    private InputDataFeeder _inputDataFeeder;

    public ExecuteScenarioMessageHandler(
        IServiceProvider serviceProvider,
        TestExecutionState testExecutionState,
        InputDataFeeder inputDataFeeder,
        IEnumerable<ISinkPlugin> sinkPlugins,
        TestSession testSession)
    {
        _serviceProvider = serviceProvider;
        _testExecutionState = testExecutionState;
        _inputDataFeeder = inputDataFeeder;
        _sinkPlugins = sinkPlugins;
    }

    public async Task Execute(
        ExecuteScenarioMessage message,
        Scenario scenario)
    {
        // Create a new scope for the iteration execution.
        await using var iterationScope = _serviceProvider.CreateAsyncScope();
        var iterationServiceProvider = iterationScope.ServiceProvider;

        object? currentInputData = null;
        if (scenario.InputDataInfo.HasInputData)
            currentInputData = _inputDataFeeder.GetNextInput(scenario.Name);

        var iterationState = ContextFactory.CreateIterationState(_testExecutionState.TestSession, iterationServiceProvider, _testExecutionState.TestFramework, scenario, currentInputData, _testExecutionState.CancellationToken);

        var iterationResult = new IterationResult();
        iterationResult.CorrelationId = iterationState.Info.CorrelationId;

        var scenarioDuration = new Stopwatch();
        scenarioDuration.Start();

        var executeStepHandler = new ExecuteStepHandler(_testExecutionState, iterationState, null);

        try
        {
            if (scenario.BeforeIterationAction != null)
            {
                var stepContext = ContextFactory.CreateIterationContext(iterationState, "BeforeIteration", null, null);
                await scenario.BeforeIterationAction(stepContext);
            }

            foreach (var (step, index) in scenario.Steps.Select((s,i)=> (s,i)))
            {
                using var loggingScope = _testExecutionState.TestSession.Logger.BeginScope(new Dictionary<string, object?>
                {
                    ["scenario"] = scenario.Name,
                    ["step"] = index + 1
                });

                await executeStepHandler.ExecuteStep(step);
                iterationResult.StepResults.Add(executeStepHandler.RootStepResult!.Name, executeStepHandler.RootStepResult);
            }
        }
        finally
        {
            scenarioDuration.Stop();

            iterationResult.ExecutionDuration = scenarioDuration.Elapsed;

            if (scenario.AfterIterationAction != null)
            {
                var stepContext = ContextFactory.CreateIterationContext(iterationState, "AfterIteration", null, null);
                await scenario.AfterIterationAction(stepContext);
            }

            await CleanupContext(iterationState);
        }

        if (_testExecutionState.TestResult.TestType == TestType.Standard)
        {
            if (currentInputData != null)
                iterationResult.InputData = currentInputData.ToString();

            _testExecutionState.TestResult.IterationResults.Add(iterationResult);
        }
        else if (_testExecutionState.TestResult.TestType == TestType.Load)
        {
            var scenarioLoadCollector = _testExecutionState.LoadCollectors[scenario.Name];

            if (message.IsWarmup)
            {
                scenarioLoadCollector.RecordWarmup(executeStepHandler.CurrentScenarioStatus ?? TestStatus.Failed);
                return;
            }

            scenarioLoadCollector.RecordMeasurement(executeStepHandler.CurrentScenarioStatus ?? TestStatus.Failed, iterationResult);

            var scenarioLoadResult = scenarioLoadCollector.GetCurrentResult();

            if (scenario.AssertWhileRunningAction != null
                && _testExecutionState.ExecutionStatus == ExecutionStatus.Running)
            {
                try
                {
                    var context = ContextFactory.CreateScenarioContext(_testExecutionState.TestSession, iterationServiceProvider, _testExecutionState.TestFramework, "AssertWhileRunning", _testExecutionState.CancellationToken);
                    scenario.AssertWhileRunningAction(context, new AssertScenarioStats(scenarioLoadResult));
                }
                catch (Exception ex)
                {
                    _testExecutionState.ExecutionStatus = ExecutionStatus.Stopped;
                    _testExecutionState.ExecutionStoppedReason = ex;
                    _testExecutionState.TestResult.Status = TestStatus.Failed;
                    _testExecutionState.FirstException = ex;
                    scenarioLoadCollector.SetAssertWhileRunningException(ex);
                    scenarioLoadCollector.SetStatus(TestStatus.Failed);
                }
            }

            await WriteToSinksAndSnapshotCollector(_testExecutionState, scenario, scenarioLoadResult, false);
        }
    }

    private async Task CleanupContext(IterationState iterationContext)
    {
        foreach (var plugin in _testExecutionState.TestSession.Configuration.ContextPlugins)
        {
            if (!plugin.RequireState)
                continue;

            var state = iterationContext.Internals.Plugins.GetState(plugin.GetType());
            await plugin.CleanupContext(state);
        }
    }

    public async Task WriteToSinksAndSnapshotCollector(
        TestExecutionState testExecutionState,
        Scenario scenario, ScenarioLoadResult scenarioLoadResult, 
        bool forceWrite)
    {
        var semaphore = testExecutionState.SinkSemaphores.GetOrAdd(scenario.Name, _ => new SemaphoreSlim(1, 1));

        if (await semaphore.WaitAsync(0))
        {
            try
            {
                var now = DateTime.UtcNow;
                var firstExecution = false;
                var lastWrite = testExecutionState.LastSinkWrite.GetOrAdd(scenario.Name, 
                    (scenarioName) => {
                        firstExecution = true;
                        return now; 
                    });

                if (firstExecution
                    || forceWrite
                    || (now - lastWrite).TotalSeconds >= _testExecutionState.TestSession.SinkWriteFrequency.TotalSeconds)
                {
                    await testExecutionState.LoadSnapshotCollector.WriteStats(scenarioLoadResult);

                    foreach (var sinkPlugin in _sinkPlugins)
                    {
                        await sinkPlugin.WriteStats(_testExecutionState.TestSession.TestRunId, testExecutionState.TestClassInstance.TestInfo.Group.Name, testExecutionState.TestClassInstance.TestInfo.Name, scenarioLoadResult);
                    }
                    testExecutionState.LastSinkWrite[scenario.Name] = now;
                }
            }
            finally
            {
                semaphore.Release();
            }
        }
    }
}
