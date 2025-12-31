using Fuzn.TestFuzn.Contracts;
using Fuzn.TestFuzn.Contracts.Adapters;
using Fuzn.TestFuzn.Contracts.Results.Feature;
using Fuzn.TestFuzn.Contracts.Results.Load;
using Fuzn.TestFuzn.Internals.InputData;
using Fuzn.TestFuzn.Internals.State;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace Fuzn.TestFuzn.Internals.Execution;

internal class ExecuteScenarioMessageHandler
{
    private readonly ITestFrameworkAdapter _testFramework;
    private readonly SharedExecutionState _sharedExecutionState;
    private readonly InputDataFeeder _inputDataFeeder;
    private readonly static ConcurrentDictionary<string, DateTime> _lastSinkWrite = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _sinkSemaphores = new();

    public ExecuteScenarioMessageHandler(ITestFrameworkAdapter testFramework,
        SharedExecutionState sharedExecutionState,
        InputDataFeeder inputDataFeeder)
    {
        _testFramework = testFramework;
        _sharedExecutionState = sharedExecutionState;
        _inputDataFeeder = inputDataFeeder;
    }

    public async Task Execute(ExecuteScenarioMessage message, Scenario scenario)
    {
        object currentInputData = null;
        if (scenario.InputDataInfo.HasInputData)
            currentInputData = _inputDataFeeder.GetNextInput(scenario.Name);

        var iterationState = ContextFactory.CreateIterationState(_testFramework, scenario, currentInputData);

        var iterationResult = new IterationResult();
        iterationResult.CorrelationId = iterationState.Info.CorrelationId;

        var scenarioDuration = new Stopwatch();
        scenarioDuration.Start();

        var executeStepHandler = new ExecuteStepHandler(_sharedExecutionState, iterationState, null);

        try
        {
            if (scenario.BeforeIterationAction != null)
            {
                var stepContext = ContextFactory.CreateIterationContext(iterationState, "InitIteration", null, null);
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
                var stepContext = ContextFactory.CreateIterationContext(iterationState, "CleanupIteration", null, null);
                await scenario.AfterIterationAction(stepContext);
            }
        }

        if (_sharedExecutionState.TestType == TestType.Standard)
        {
            if (currentInputData != null)
                iterationResult.InputData = PropertyHelper.GetStringFromProperties(currentInputData);

            _sharedExecutionState.ResultState.FeatureCollectors[scenario.Name].IterationResults.Add(iterationResult);
            await CleanupContext(iterationState);
        }
        else if (_sharedExecutionState.TestType == TestType.Load)
        {
            var scenarioLoadCollector = _sharedExecutionState.ResultState.LoadCollectors[scenario.Name];

            if (message.IsWarmup)
            {
                scenarioLoadCollector.RecordWarmup(executeStepHandler.CurrentScenarioStatus ?? TestStatus.Failed);
                return;
            }

            scenarioLoadCollector.RecordMeasurement(executeStepHandler.CurrentScenarioStatus ?? TestStatus.Failed, iterationResult);

            var scenarioLoadResult = scenarioLoadCollector.GetCurrentResult();

            await CleanupContext(iterationState);

            if (scenario.AssertWhileRunningAction != null
                && _sharedExecutionState.TestRunState.ExecutionStatus == ExecutionStatus.Running)
            {
                try
                {
                    var context = ContextFactory.CreateScenarioContext(_testFramework, "AssertWhileRunning");
                    scenario.AssertWhileRunningAction(context, new AssertScenarioStats(scenarioLoadResult));
                }
                catch (Exception ex)
                {
                    _sharedExecutionState.TestRunState.ExecutionStatus = ExecutionStatus.Stopped;
                    _sharedExecutionState.TestRunState.ExecutionStoppedReason = ex;
                    _sharedExecutionState.TestRunState.FirstException = ex;
                    scenarioLoadCollector.SetAssertWhileRunningException(ex);
                    scenarioLoadCollector.SetStatus(TestStatus.Failed);
                }
            }

            await WriteToSinks(scenario, scenarioLoadResult, false);
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

    public async Task WriteToSinks(Scenario scenario, ScenarioLoadResult scenarioLoadResult, bool forceWrite)
    {
        var semaphore = _sinkSemaphores.GetOrAdd(scenario.Name, _ => new SemaphoreSlim(1, 1));

        if (await semaphore.WaitAsync(0))
        {
            try
            {
                var now = DateTime.UtcNow;
                var firstExecution = false;
                var lastWrite = _lastSinkWrite.GetOrAdd(scenario.Name, 
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
                            await sinkPlugin.WriteStats(GlobalState.TestRunId, _sharedExecutionState.IFeatureTestClassInstance.TestInfo.Group.Name, scenarioLoadResult);
                        }
                    }
                    _lastSinkWrite[scenario.Name] = now;
                }
            }
            finally
            {
                semaphore.Release();
            }
        }
    }
}
