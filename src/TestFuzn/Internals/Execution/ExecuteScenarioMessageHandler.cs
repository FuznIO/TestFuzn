using System.Collections.Concurrent;
using System.Diagnostics;
using FuznLabs.TestFuzn.Internals.InputData;
using FuznLabs.TestFuzn.Internals.State;
using FuznLabs.TestFuzn.Contracts.Adapters;
using FuznLabs.TestFuzn.Contracts.Results.Feature;
using FuznLabs.TestFuzn.Contracts.Results.Load;

namespace FuznLabs.TestFuzn.Internals.Execution;

internal class ExecuteScenarioMessageHandler
{
    private readonly ITestFrameworkAdapter _testFramework;
    private readonly SharedExecutionState _sharedExecutionState;
    private readonly InputDataFeeder _loadInputFeeder;
    private readonly static ConcurrentDictionary<string, DateTime> _lastSinkWrite = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _sinkSemaphores = new();

    public ExecuteScenarioMessageHandler(ITestFrameworkAdapter testFramework,
        SharedExecutionState sharedExecutionState,
        InputDataFeeder loadInputFeeder)
    {
        _testFramework = testFramework;
        _sharedExecutionState = sharedExecutionState;
        _loadInputFeeder = loadInputFeeder;
    }

    public async Task Execute(ExecuteScenarioMessage message, Scenario scenario)
    {
        var scenarioLoadCollector = _sharedExecutionState.ResultState.LoadCollectors[scenario.Name];

        object currentInputData = null;

        if (scenario.InputDataInfo.HasInputData)
            currentInputData = _loadInputFeeder.GetNextInput(scenario.Name);

        var iterationResult = new IterationFeatureResult();

        if (_sharedExecutionState.TestType == TestType.Feature)
        {
            if (currentInputData != null)
            {
                iterationResult.InputData = PropertyHelper.GetStringFromProperties(currentInputData);
            }

            _sharedExecutionState.ResultState.FeatureCollectors[scenario.Name].IterationResults.Add(iterationResult);
        }

        var iterationContext = ContextFactory.CreateIterationContextForStepContext(_testFramework, scenario, currentInputData);

        if (_sharedExecutionState.TestType == TestType.Feature)
            iterationResult.CorrelationId = iterationContext.Info.CorrelationId;
        
        var scenarioDuration = new Stopwatch();
        scenarioDuration.Start();
        var stepDuration = new Stopwatch();

        var executeStepHandler = new ExecuteStepHandler(_sharedExecutionState, iterationContext, null);

        try
        {
            var totalSteps = scenario.Steps.Count;

            foreach (var step in scenario.Steps)
            {
                await executeStepHandler.ExecuteStep(ExecuteStepHandler.StepType.Outer, step);
                iterationResult.StepResults.Add(executeStepHandler.OuterStepResult.Name, executeStepHandler.OuterStepResult);
            }
        }
        finally
        {
            scenarioDuration.Stop();

            iterationResult.ExecutionDuration = scenarioDuration.Elapsed;

            if (scenario.CleanupAfterEachIterationAction != null)
            {
                var stepContext = ContextFactory.CreateStepContext(iterationContext, "CleanupAfterEachIteration", null);
                await scenario.CleanupAfterEachIterationAction(stepContext);
            }
        }

        if (message.IsWarmup)
        {
            scenarioLoadCollector.RecordWarmup(executeStepHandler.CurrentScenarioStatus ?? ScenarioStatus.Failed);
            return;
        }

        scenarioLoadCollector.RecordMeasurement(executeStepHandler.CurrentScenarioStatus ?? ScenarioStatus.Failed, iterationResult);

        var scenarioLoadResult = scenarioLoadCollector.GetCurrentResult();

        foreach (var plugin in GlobalState.Configuration.ContextPlugins)
        {
            if (!plugin.RequireState)
                continue;

            var state = iterationContext.Internals.Plugins.GetState(plugin.GetType());
            await plugin.CleanupContext(state);
        }

        if (scenario.AssertWhileRunningAction != null
            && _sharedExecutionState.TestRunState.ExecutionStatus == ExecutionStatus.Running)
        {
            try
            {
                var context = ContextFactory.CreateContext(_testFramework, "AssertWhileRunning");
                scenario.AssertWhileRunningAction(context, new AssertScenarioStats(scenarioLoadResult));
            }
            catch (Exception ex)
            {
                _sharedExecutionState.TestRunState.ExecutionStatus = ExecutionStatus.Stopped;
                _sharedExecutionState.TestRunState.ExecutionStoppedReason = ex;
                scenarioLoadCollector.SetStatus(ScenarioStatus.Failed);
            }
        }

        await WriteToSinks(scenario, scenarioLoadResult, false);
    }

    public async Task WriteToSinks(Scenario scenario, ScenarioLoadResult scenarioLoadResult, bool forceWrite)
    {
        if (_sharedExecutionState.TestType == TestType.Load)
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
                                await sinkPlugin.WriteMetrics(GlobalState.TestRunId, _sharedExecutionState.FeatureName, scenarioLoadResult);
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
}
