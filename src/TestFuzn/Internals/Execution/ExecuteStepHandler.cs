using System.Diagnostics;
using Fuzn.TestFuzn.Internals.State;
using Fuzn.TestFuzn.Contracts.Results.Feature;
using Fuzn.TestFuzn.Contracts;

namespace Fuzn.TestFuzn.Internals.Execution;

internal class ExecuteStepHandler
{
    private readonly SharedExecutionState _sharedExecutionState;
    private readonly IterationState _iterationContext;
    public ScenarioStatus? CurrentScenarioStatus { get; set; }
    public StepFeatureResult? RootStepResult { get; set; }

    public ExecuteStepHandler(SharedExecutionState sharedExecutionState,
        IterationState iterationContext,
        ScenarioStatus? scenarioStatus)
    {
        _sharedExecutionState = sharedExecutionState;
        _iterationContext = iterationContext;
        CurrentScenarioStatus = scenarioStatus;
    }

    public async Task ExecuteStep(Step step)
    {
        step.Validate();

        var stepDuration = new Stopwatch();
        stepDuration.Start();

        var stepResult = new StepFeatureResult();
        stepResult.Name = step.Name;

        AddResultToParentStep(step, stepResult);

        if (CurrentScenarioStatus != null && CurrentScenarioStatus == ScenarioStatus.Failed)
        {
            stepResult.Status = StepStatus.Skipped;
        }
        else
        {
            var stepContext = ContextFactory.CreateIterationContext(_iterationContext, step.Name, step.Id, step.ParentName);
            _iterationContext.ExecuteStepHandler = this;

            try
            {    
                await step.Action(stepContext);

                if (CurrentScenarioStatus != null && CurrentScenarioStatus == ScenarioStatus.Failed)
                {
                    // Can happen if a child step fails.
                    stepResult.Status = StepStatus.Failed;
                }
                else
                {
                    stepResult.Status = StepStatus.Passed;  
                    CurrentScenarioStatus = ScenarioStatus.Passed;
                }
                    
            }
            catch (Exception ex)
            {
                stepResult.Status = StepStatus.Failed;
                CurrentScenarioStatus = ScenarioStatus.Failed;
                stepResult.Exception = ex;
                if (_sharedExecutionState.TestType == TestType.Feature
                    && _sharedExecutionState.TestRunState.FirstException == null)
                    _sharedExecutionState.TestRunState.FirstException = ex;
            }
            finally
            {
                if (stepContext.StepInfo != null)
                {
                    if (stepContext.StepInfo.Comments != null && stepContext.StepInfo.Comments.Count > 0)
                    {
                        stepResult.Comments = new();
                        stepResult.Comments.AddRange(stepContext.StepInfo.Comments);
                    }

                    if (stepContext.StepInfo.Attachments != null && stepContext.StepInfo.Attachments.Count > 0)
                    {
                        stepResult.Attachments = new();
                        stepResult.Attachments.AddRange(stepContext.StepInfo.Attachments);
                    }
                }
            }
        }

        stepDuration.Stop();
        stepResult.Duration = stepDuration.Elapsed;
    }

    private void AddResultToParentStep(Step step, StepFeatureResult stepResult)
    {
        if (step.ParentName == null)
        {
            RootStepResult = stepResult;
            return;
        }

        var parentResult = RootStepResult.Name == step.ParentName
            ? RootStepResult
            : FindParentRecursive(RootStepResult, step.ParentName);

        if (parentResult == null)
            throw new Exception($"Parent step '{step.ParentName}' not found.");

        parentResult.StepResults ??= [];
        parentResult.StepResults.Add(stepResult);
    }

    private static StepFeatureResult? FindParentRecursive(StepFeatureResult current, string parentName)
    {
        if (current.StepResults == null || current.StepResults.Count == 0)
            return null;

        foreach (var child in current.StepResults)
        {
            if (child.Name == parentName)
                return child;

            var found = FindParentRecursive(child, parentName);
            if (found != null)
                return found;
        }

        return null;
    }
}
