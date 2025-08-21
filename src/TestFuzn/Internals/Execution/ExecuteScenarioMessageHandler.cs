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

        var stepContext = ContextFactory.CreateStepContext(_testFramework, scenario, "", currentInputData);

        if (_sharedExecutionState.TestType == TestType.Feature)
            iterationResult.CorrelationId = stepContext.CorrelationId;
        
        var scenarioDuration = new Stopwatch();
        scenarioDuration.Start();
        var stepDuration = new Stopwatch();
        ScenarioStatus? scenarioStatus = null;

        try
        {
            var totalSteps = scenario.Steps.Count;
            var stepIndex = 0;

            foreach (var step in scenario.Steps)
            {
                stepIndex++;

                stepDuration.Restart();

                var stepResult = new StepFeatureResult();
                stepResult.Name = step.Name;

                if (scenarioStatus == ScenarioStatus.Failed)
                {
                    stepResult.Status = StepStatus.Skipped;
                }
                else
                {
                    try
                    {
                        stepContext.CurrentStep = new CurrentStep(stepContext, step.Name);
                        await step.Action(stepContext);
                        stepResult.Status = StepStatus.Passed;
                        scenarioStatus = ScenarioStatus.Passed;
                    }
                    catch (Exception ex)
                    {
                        stepResult.Status = StepStatus.Failed;
                        scenarioStatus = ScenarioStatus.Failed;
                        stepResult.Exception = ex;
                        if (_sharedExecutionState.TestType == TestType.Feature
                            && !scenario.InputDataInfo.HasInputData
                            && _sharedExecutionState.TestRunState.FirstException == null)
                            _sharedExecutionState.TestRunState.FirstException = ex;
                    }
                    finally
                    {
                        if (stepContext.CurrentStep != null
                            && stepContext.CurrentStep.Attachments != null
                            && stepContext.CurrentStep.Attachments.Count > 0)
                        {
                            stepResult.Attachments = new();
                            stepResult.Attachments.AddRange(stepContext.CurrentStep.Attachments);
                        }
                    }
                }

                stepDuration.Stop();
                stepResult.Duration = stepDuration.Elapsed;

                iterationResult.StepResults.Add(stepResult.Name, stepResult);
            }
        }
        finally
        {
            scenarioDuration.Stop();

            iterationResult.ExecutionDuration = scenarioDuration.Elapsed;

            if (scenario.CleanupAfterEachIterationAction != null)
            {
                stepContext.CurrentStep = new CurrentStep(stepContext, "CleanupAfterEachIteration");
                await scenario.CleanupAfterEachIterationAction(stepContext);
            }
        }

        if (message.IsWarmup)
        {
            scenarioLoadCollector.RecordWarmup(scenarioStatus ?? ScenarioStatus.Failed);
            return;
        }

        scenarioLoadCollector.RecordMeasurement(scenarioStatus ?? ScenarioStatus.Failed, iterationResult);

        var scenarioLoadResult = scenarioLoadCollector.GetCurrentResult();

        foreach (var plugin in GlobalState.Configuration.ContextPlugins)
        {
            if (!plugin.RequireState)
                continue;

            var state = stepContext.Internals.Plugins.GetState(plugin.GetType());
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
