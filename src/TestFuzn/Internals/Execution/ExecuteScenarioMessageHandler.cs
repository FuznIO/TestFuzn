using Fuzn.TestFuzn.Contracts;
using Fuzn.TestFuzn.Contracts.Results.Standard;
using Fuzn.TestFuzn.Contracts.Results.Load;
using Fuzn.TestFuzn.Internals.State;
using System.Diagnostics;
using Fuzn.TestFuzn.Internals.InputData;

namespace Fuzn.TestFuzn.Internals.Execution;

internal class ExecuteScenarioMessageHandler
{
    private InputDataFeeder _inputDataFeeder;

    public ExecuteScenarioMessageHandler(InputDataFeeder inputDataFeeder)
    {
        _inputDataFeeder = inputDataFeeder;
    }

    public async Task Execute(
        TestExecutionState testExecutionState,
        ExecuteScenarioMessage message, 
        Scenario scenario)
    {
        object? currentInputData = null;
        if (scenario.InputDataInfo.HasInputData)
            currentInputData = _inputDataFeeder.GetNextInput(scenario.Name);

        var iterationState = ContextFactory.CreateIterationState(testExecutionState.TestFramework, scenario, currentInputData);

        var iterationResult = new IterationResult();
        iterationResult.CorrelationId = iterationState.Info.CorrelationId;

        var scenarioDuration = new Stopwatch();
        scenarioDuration.Start();

        var executeStepHandler = new ExecuteStepHandler(testExecutionState, iterationState, null);

        try
        {
            if (scenario.BeforeIterationAction != null)
            {
                var stepContext = ContextFactory.CreateIterationContext(iterationState, "BeforeIteration", null, null);
                await scenario.BeforeIterationAction(stepContext);
            }

            foreach (var (step, index) in scenario.Steps.Select((s,i)=> (s,i)))
            {
                using var loggingScope = GlobalState.Logger.BeginScope(new Dictionary<string, object?>
                {
                    ["scenario"] = scenario.Name,
                    ["step"] = index + 1
                });
                
                await executeStepHandler.ExecuteStep(step);
                iterationResult.StepResults.Add(executeStepHandler.RootStepResult.Name, executeStepHandler.RootStepResult);
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
        }

        if (testExecutionState.TestResult.TestType == TestType.Standard)
        {
            if (currentInputData != null)
                iterationResult.InputData = currentInputData.ToString();

            testExecutionState.TestResult.IterationResults.Add(iterationResult);
            await CleanupContext(iterationState);
        }
        else if (testExecutionState.TestResult.TestType == TestType.Load)
        {
            var scenarioLoadCollector = testExecutionState.LoadCollectors[scenario.Name];

            if (message.IsWarmup)
            {
                scenarioLoadCollector.RecordWarmup(executeStepHandler.CurrentScenarioStatus ?? TestStatus.Failed);
                return;
            }

            scenarioLoadCollector.RecordMeasurement(executeStepHandler.CurrentScenarioStatus ?? TestStatus.Failed, iterationResult);

            var scenarioLoadResult = scenarioLoadCollector.GetCurrentResult();

            await CleanupContext(iterationState);

            if (scenario.AssertWhileRunningAction != null
                && testExecutionState.ExecutionStatus == ExecutionStatus.Running)
            {
                try
                {
                    var context = ContextFactory.CreateScenarioContext(testExecutionState.TestFramework, "AssertWhileRunning");
                    scenario.AssertWhileRunningAction(context, new AssertScenarioStats(scenarioLoadResult));
                }
                catch (Exception ex)
                {
                    testExecutionState.ExecutionStatus = ExecutionStatus.Stopped;
                    testExecutionState.ExecutionStoppedReason = ex;
                    testExecutionState.TestResult.Status = TestStatus.Failed;
                    testExecutionState.FirstException = ex;
                    scenarioLoadCollector.SetAssertWhileRunningException(ex);
                    scenarioLoadCollector.SetStatus(TestStatus.Failed);
                }
            }

            await WriteToSinks(testExecutionState, scenario, scenarioLoadResult, false);
        }
    }

    private static async Task CleanupContext(IterationState iterationContext)
    {
        foreach (var plugin in GlobalState.Configuration.ContextPlugins)
        {
            if (!plugin.RequireState)
                continue;

            var state = iterationContext.Internals.Plugins.GetState(plugin.GetType());
            await plugin.CleanupContext(state);
        }
    }

    public async Task WriteToSinks(
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
                    || (now - lastWrite).TotalSeconds >= GlobalState.SinkWriteFrequency.TotalSeconds)
                {
                    var sinkPlugins = GlobalState.Configuration.SinkPlugins;
                    if (sinkPlugins != null && sinkPlugins.Count > 0)
                    {
                        foreach (var sinkPlugin in GlobalState.Configuration.SinkPlugins)
                        {
                            await sinkPlugin.WriteStats(GlobalState.TestRunId, testExecutionState.TestClassInstance.TestInfo.Group.Name, testExecutionState.TestClassInstance.TestInfo.Name, scenarioLoadResult);
                        }
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
