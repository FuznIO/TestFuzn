using System.Diagnostics;
using Fuzn.TestFuzn.Internals.State;
using Fuzn.TestFuzn.Contracts.Results.Standard;
using Fuzn.TestFuzn.Contracts;

namespace Fuzn.TestFuzn.Internals.Execution;

internal class ExecuteStepHandler
{
    private readonly TestExecutionState _testExecutionState;
    private readonly IterationState _iterationContext;
    public TestStatus? CurrentScenarioStatus { get; set; }
    public StepStandardResult? RootStepResult { get; set; }

    public ExecuteStepHandler(TestExecutionState testExecutionState,
        IterationState iterationContext,
        TestStatus? scenarioStatus)
    {
        _testExecutionState = testExecutionState;
        _iterationContext = iterationContext;
        CurrentScenarioStatus = scenarioStatus;
    }

    public async Task ExecuteStep(Step step)
    {
        step.Validate();

        var stepDuration = new Stopwatch();
        stepDuration.Start();

        var stepResult = new StepStandardResult();
        stepResult.Name = step.Name;

        AddResultToParentStep(step, stepResult);

        if (CurrentScenarioStatus != null && CurrentScenarioStatus == TestStatus.Failed)
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

                if (CurrentScenarioStatus != null && CurrentScenarioStatus == TestStatus.Failed)
                {
                    // Can happen if a child step fails.
                    stepResult.Status = StepStatus.Failed;
                }
                else
                {
                    stepResult.Status = StepStatus.Passed;  
                    CurrentScenarioStatus = TestStatus.Passed;
                }
                    
            }
            catch (Exception ex)
            {
                await HandlePluginExceptionHandler(stepContext, ex);

                stepResult.Status = StepStatus.Failed;
                CurrentScenarioStatus = TestStatus.Failed;
                stepResult.Exception = ex;
                if (_testExecutionState.TestResult.TestType == TestType.Standard
                    && _testExecutionState.FirstException == null)
                    _testExecutionState.FirstException = ex;
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

    private void AddResultToParentStep(Step step, StepStandardResult stepResult)
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

    private static StepStandardResult? FindParentRecursive(StepStandardResult current, string parentName)
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

    private static async Task HandlePluginExceptionHandler(IterationContext iterationContext, Exception exception)
    {
        foreach (var plugin in GlobalState.Configuration.ContextPlugins)
        {
            try
            {
                if (!plugin.RequireStepExceptionHandling)
                    continue;

                var state = iterationContext.Internals.Plugins.GetState(plugin.GetType());
                await plugin.HandleStepException(state, iterationContext, exception);
            }
            catch (Exception ex)
            {
                GlobalState.Logger.LogError(ex, $"An exception occurred in the plugin {plugin.GetType()} step exception handler.");
            }
        }
    }
}
