using System.Diagnostics;
using Fuzn.TestFuzn.Internals.State;
using Fuzn.TestFuzn.Contracts.Results.Feature;

namespace Fuzn.TestFuzn.Internals.Execution;

internal class ExecuteStepHandler
{
    private readonly SharedExecutionState _sharedExecutionState;
    private readonly IterationContext _iterationContext;
    public ScenarioStatus? CurrentScenarioStatus { get; set; }
    public StepFeatureResult? OuterStepResult { get; set; }

    public ExecuteStepHandler(SharedExecutionState sharedExecutionState,
        IterationContext iterationContext,
        ScenarioStatus? scenarioStatus)
    {
        _sharedExecutionState = sharedExecutionState;
        _iterationContext = iterationContext;
        CurrentScenarioStatus = scenarioStatus;
    }

    public async Task ExecuteStep(StepType stepType, Step step)
    {
        step.Validate();

        var stepDuration = new Stopwatch();
        stepDuration.Start();

        var stepResult = new StepFeatureResult();
        stepResult.Name = step.Name;

        if (stepType == StepType.Outer)
            OuterStepResult = stepResult;
        else if (stepType == StepType.Inner)
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

        if (CurrentScenarioStatus != null && CurrentScenarioStatus == ScenarioStatus.Failed)
        {
            stepResult.Status = StepStatus.Skipped;
        }
        else
        {
            var stepContext = ContextFactory.CreateStepContext(_iterationContext, step.Name, step.ParentName);
            _iterationContext.ExecuteStepHandler = this;

            try
            {    
                await step.Action(stepContext);

                stepResult.Status = StepStatus.Passed;
                CurrentScenarioStatus = ScenarioStatus.Passed;
            }
            catch (Exception ex)
            {
                stepResult.Status = StepStatus.Failed;
                CurrentScenarioStatus = ScenarioStatus.Failed;
                stepResult.Exception = ex;
                if (_sharedExecutionState.TestType == TestType.Feature
                    && !_iterationContext.Scenario.InputDataInfo.HasInputData
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

    public enum StepType
    {
        Outer = 1,
        Inner = 2
    }
}
