using System.Diagnostics;
using Fuzn.TestFuzn.Internals.State;
using Fuzn.TestFuzn.Contracts.Results.Feature;

namespace Fuzn.TestFuzn.Internals.Execution;

internal class ExecuteStepHandler
{
    private readonly SharedExecutionState _sharedExecutionState;
    private readonly IterationContext _iterationContext;
    public ScenarioStatus? CurrentScenarioStatus { get; set; }
    public StepFeatureResult? RootStepResult { get; set; }

    public ExecuteStepHandler(SharedExecutionState sharedExecutionState,
        IterationContext iterationContext,
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
            var stepContext = ContextFactory.CreateStepContext(_iterationContext, step.Name, step.ParentName);
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

    private void AddResultToParentStep(Step step, StepFeatureResult stepResult)
    {
        if (step.ParentName == null)
        {
            RootStepResult = stepResult;
            return;
        }

        if (RootStepResult.Name == step.ParentName)
        {
            if (RootStepResult.StepResults == null)
                RootStepResult.StepResults = new();
            RootStepResult.StepResults.Add(stepResult);
            return;
        }

        foreach (var parentResult in RootStepResult.StepResults)
        {
            if (parentResult.Name == step.ParentName)
            {
                if (parentResult.StepResults == null)
                    parentResult.StepResults = new();
                parentResult.StepResults.Add(stepResult);
                return;
            }
        }

        throw new Exception(@$"Parent step '{step.ParentName}' not found.");
    }
}
