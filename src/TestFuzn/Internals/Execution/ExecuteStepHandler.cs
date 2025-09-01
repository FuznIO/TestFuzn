using System.Diagnostics;
using FuznLabs.TestFuzn.Internals.State;
using FuznLabs.TestFuzn.Contracts.Results.Feature;

namespace FuznLabs.TestFuzn.Internals.Execution;

internal class ExecuteStepHandler
{
    private readonly SharedExecutionState _sharedExecutionState;
    private readonly Scenario _scenario;
    private readonly BaseStepContext _stepContext;
    public ScenarioStatus? CurrentScenarioStatus { get; set; }
    public StepFeatureResult? OuterStepResult { get; set; }

    public ExecuteStepHandler(SharedExecutionState sharedExecutionState,
        Scenario scenario,
        BaseStepContext stepContext,
        ScenarioStatus? scenarioStatus)
    {
        _sharedExecutionState = sharedExecutionState;
        _scenario = scenario;
        _stepContext = stepContext;
        CurrentScenarioStatus = scenarioStatus;
    }

    public async Task ExecuteStep(BaseStep step)
    {
        var stepDuration = new Stopwatch();
        stepDuration.Start();

        var stepResult = new StepFeatureResult();
        stepResult.Name = step.Name;

        if (OuterStepResult == null)
            OuterStepResult = stepResult;
        else
        {
            if (OuterStepResult.Name == step.ParentName)
            {
                if (OuterStepResult.StepResults == null)
                    OuterStepResult.StepResults = new();
                OuterStepResult.StepResults.Add(stepResult);
            }
            else
            {
                var parentStep = FindParentStep(step.ParentName, OuterStepResult.StepResults);
                if (parentStep == null)
                    throw new Exception(@$"Parent step '{step.ParentName}' not found.");

                if (parentStep.StepResults == null)
                    parentStep.StepResults = new();
                parentStep.StepResults.Add(stepResult);
            }
        }

        if (CurrentScenarioStatus == ScenarioStatus.Failed)
        {
            stepResult.Status = StepStatus.Skipped;
        }
        else
        {
            try
            {
                var prevStep = _stepContext.CurrentStep;
                _stepContext.CurrentStep = new CurrentStep(_stepContext, step.Name, step.ParentName);
                _stepContext.ExecuteStepHandler = this;
                await step.Action(_stepContext);
                _stepContext.CurrentStep = prevStep;

                stepResult.Status = StepStatus.Passed;
                CurrentScenarioStatus = ScenarioStatus.Passed;
            }
            catch (Exception ex)
            {
                stepResult.Status = StepStatus.Failed;
                CurrentScenarioStatus = ScenarioStatus.Failed;
                stepResult.Exception = ex;
                if (_sharedExecutionState.TestType == TestType.Feature
                    && !_scenario.InputDataInfo.HasInputData
                    && _sharedExecutionState.TestRunState.FirstException == null)
                    _sharedExecutionState.TestRunState.FirstException = ex;
            }
            finally
            {
                if (_stepContext.CurrentStep != null
                    && _stepContext.CurrentStep.Attachments != null
                    && _stepContext.CurrentStep.Attachments.Count > 0)
                {
                    stepResult.Attachments = new();
                    stepResult.Attachments.AddRange(_stepContext.CurrentStep.Attachments);
                }
            }
        }

        stepDuration.Stop();
        stepResult.Duration = stepDuration.Elapsed;
    }

    private StepFeatureResult? FindParentStep(string parentName, List<StepFeatureResult> results)
    {
        if (results == null || results.Count == 0)
            return null;

        foreach (var result in results)
        {
            if (result.Name == parentName)
                return result;

            if (result.StepResults != null && result.StepResults.Count > 0)
            {
                var parentStep = FindParentStep(parentName, result.StepResults);
                if (parentStep != null)
                    return parentStep;
            }
        }

        return null;
    }
}
