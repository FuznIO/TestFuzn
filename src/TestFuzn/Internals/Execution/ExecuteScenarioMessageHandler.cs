using Fuzn.TestFuzn.Contracts;
using Fuzn.TestFuzn.Contracts.Results.Standard;
using Fuzn.TestFuzn.Contracts.Results.Load;
using Fuzn.TestFuzn.Contracts.Sinks;
using Fuzn.TestFuzn.Internals.Logging;
using Fuzn.TestFuzn.Internals.State;
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
        ExecuteScenarioMessage message)
    {
        var scenario = message.Scenario;

        // Create a new scope for the iteration execution.
        await using var iterationScope = _serviceProvider.CreateAsyncScope();
        var iterationServiceProvider = iterationScope.ServiceProvider;

        object? currentInputData = null;
        if (scenario.InputDataInfo.HasInputData)
            currentInputData = _inputDataFeeder.GetNextInput(scenario.Name);

        var iterationState = ContextFactory.CreateIterationState(_testExecutionState.TestSession, iterationServiceProvider, _testExecutionState.TestFramework, scenario, currentInputData, message.MessageId, _testExecutionState.CancellationToken);

        var iterationResult = new IterationResult();
        iterationResult.CorrelationId = iterationState.Info.CorrelationId;

        var isStandardTest = _testExecutionState.TestResult.TestType == TestType.Standard;

        var executeStepHandler = new ExecuteStepHandler(_testExecutionState, iterationState, null);

        try
        {
            if (isStandardTest)
                iterationResult.InitStartTime = DateTime.UtcNow;

            if (scenario.BeforeIterationAction != null)
            {
                var stepContext = ContextFactory.CreateIterationContext(iterationState, "BeforeIteration", null, null);
                await scenario.BeforeIterationAction(stepContext);
            }

            if (isStandardTest)
                iterationResult.InitEndTime = DateTime.UtcNow;

            iterationResult.ExecuteStartTime = DateTime.UtcNow;

            foreach (var (step, index) in scenario.Steps.Select((s,i)=> (s,i)))
            {
                using var loggingScope = _testExecutionState.TestSession.Logger.BeginScope(new LoggingScopeState(scenario.Name, index + 1));

                await executeStepHandler.ExecuteStep(step);
                iterationResult.StepResults.Add(executeStepHandler.RootStepResult!.Name, executeStepHandler.RootStepResult);
            }
        }
        finally
        {
            iterationResult.ExecuteEndTime = DateTime.UtcNow;

            if (isStandardTest)
                iterationResult.CleanupStartTime = DateTime.UtcNow;

            if (iterationState.CleanupActions != null)
            {
                for (int i = iterationState.CleanupActions.Count - 1; i >= 0; i--)
                {
                    await iterationState.CleanupActions[i]();
                }
            }

            if (scenario.AfterIterationAction != null)
            {
                var stepContext = ContextFactory.CreateIterationContext(iterationState, "AfterIteration", null, null);
                await scenario.AfterIterationAction(stepContext);
            }

            await CleanupIteration(iterationState);

            if (isStandardTest)
                iterationResult.CleanupEndTime = DateTime.UtcNow;
        }

        if (_testExecutionState.TestResult.TestType == TestType.Standard)
        {
            if (currentInputData != null)
                iterationResult.InputData = currentInputData;

            _testExecutionState.TestResult.IterationResults.Add(iterationResult);
        }
        else if (_testExecutionState.TestResult.TestType == TestType.Load)
        {
            var scenarioLoadCollector = _testExecutionState.LoadCollectors[scenario.Name];

            if (message.IsWarmup)
            {
                scenarioLoadCollector.RecordWarmup(executeStepHandler.CurrentScenarioStatus ?? TestStatus.Failed);

                if (scenario.AssertWhileWarmingUpAction != null
                    && _testExecutionState.ExecutionStatus == ExecutionStatus.Running)
                {
                    try
                    {
                        var warmupStats = scenarioLoadCollector.GetWarmupStats();
                        var context = ContextFactory.CreateScenarioContext(_testExecutionState.TestSession, iterationServiceProvider, _testExecutionState.TestFramework, "AssertWhileWarmingUp", _testExecutionState.CancellationToken);
                        scenario.AssertWhileWarmingUpAction(context, warmupStats);
                    }
                    catch (Exception ex)
                    {
                        _testExecutionState.ExecutionStatus = ExecutionStatus.Stopped;
                        _testExecutionState.ExecutionStoppedReason = ex;
                        _testExecutionState.TestResult.Status = TestStatus.Failed;
                        _testExecutionState.FirstException = ex;
                        scenarioLoadCollector.SetAssertWhileWarmingUpException(ex);
                        scenarioLoadCollector.SetStatus(TestStatus.Failed);
                    }
                }

                return;
            }

            scenarioLoadCollector.RecordMeasurement(executeStepHandler.CurrentScenarioStatus ?? TestStatus.Failed, iterationResult);

            ScenarioLoadResult scenarioLoadResult = null;

            if (scenario.AssertWhileRunningAction != null
                && _testExecutionState.ExecutionStatus == ExecutionStatus.Running)
            {
                try
                {
                    scenarioLoadResult = scenarioLoadCollector.GetCurrentResult();
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

            await WriteToSinksAndSnapshotCollector(_testExecutionState, scenario, () => scenarioLoadResult ?? scenarioLoadCollector.GetCurrentResult(), false);
        }
    }

    private async Task CleanupIteration(IterationState iterationContext)
    {
        foreach (var plugin in _testExecutionState.TestSession.Configuration.ContextPlugins)
        {
            if (!plugin.RequireIterationState)
                continue;

            var state = iterationContext.Internals.Plugins.GetState(plugin.GetType());
            await plugin.CleanupIteration(state);
        }
    }

    public async Task WriteToSinksAndSnapshotCollector(
        TestExecutionState testExecutionState,
        Scenario scenario, Func<ScenarioLoadResult> getScenarioLoadResult,
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
                    var scenarioLoadResult = getScenarioLoadResult();

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
