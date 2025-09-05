using Fuzn.TestFuzn.Internals.Execution;

namespace Fuzn.TestFuzn;

public sealed class StepContext<TCustomStepContext> : BaseStepContext
    where TCustomStepContext : new()
{
    public TCustomStepContext Custom => (TCustomStepContext) IterationContext.Custom;

    public async Task Step(string name, Func<StepContext<TCustomStepContext>, Task> action)
    {
        var step = new Step();
        step.ContextType = typeof(StepContext<TCustomStepContext>);
        step.Name = name;
        step.ParentName = CurrentStep.Name;
        step.Action = ctx => action((StepContext<TCustomStepContext>) ctx);
        await IterationContext.ExecuteStepHandler.ExecuteStep(ExecuteStepHandler.StepType.Inner, step);
    }


    public void Step(string name, Action<StepContext<TCustomStepContext>> action)
    {
        var step = new Step();
        step.ContextType = typeof(StepContext<TCustomStepContext>);
        step.Name = name;
        step.ParentName = CurrentStep.Name;
        step.Action = (ctx) =>
            {
                action((StepContext<TCustomStepContext>) ctx);
                return Task.CompletedTask;
            };
        IterationContext.ExecuteStepHandler.ExecuteStep(ExecuteStepHandler.StepType.Inner, step).GetAwaiter().GetResult();
    }
}
