using System.Collections.Concurrent;
using System.Diagnostics;
using TestFusion.Internals.InputData;
using TestFusion.Internals.State;
using TestFusion.Contracts.Adapters;
using TestFusion.Contracts.Results.Feature;
using TestFusion.Contracts.Results.Load;

namespace TestFusion.Internals.Execution;

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
                        stepContext.Step = new CurrentStep(stepContext, step.Name);
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
                        if (stepContext.Step != null
                            && stepContext.Step.Attachments != null
                            && stepContext.Step.Attachments.Count > 0)
                        {
                            stepResult.Attachments = new();
                            stepResult.Attachments.AddRange(stepContext.Step.Attachments);
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
                stepContext.Step = new CurrentStep(stepContext, "CleanupAfterEachIteration");
                await scenario.CleanupAfterEachIterationAction(stepContext);
            }
        }

        if (message.IsWarmup)
            return;

        scenarioLoadCollector.RecordMeasurement(scenarioStatus ?? ScenarioStatus.Failed, iterationResult);

        var scenarioLoadResult = scenarioLoadCollector.GetCurrentResult();

        foreach (var plugin in GlobalState.Configuration.ContextPlugins)
        {
            if (!plugin.RequireState)
                continue;

            var state = stepContext.Internals.Plugins.GetState(plugin.GetType());
            await plugin.CleanupContext(state);
        }

        if (scenario.AssertWhileRunningAction != null)
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
                                await sinkPlugin.WriteMetrics(GlobalState.TestRunId, scenario.Name, scenarioLoadResult);
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
