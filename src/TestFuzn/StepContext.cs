namespace FuznLabs.TestFuzn;

public class StepContext : StepContext<DefaultCustomStepContext>
{
}

public class StepContext<TCustomStepContext> : BaseStepContext
    where TCustomStepContext : new()
{
    public TCustomStepContext Custom { get; set; }

    public async Task Step(string name, Func<StepContext<TCustomStepContext>, Task> action)
    {
        var stepType = typeof(Step<>).MakeGenericType(typeof(TCustomStepContext));
        var step = (BaseStep?) Activator.CreateInstance(stepType);

        if (step == null)
        {
            throw new InvalidOperationException($"Failed to create an instance of type {stepType.FullName}");
        }

        step.ContextType = typeof(StepContext<TCustomStepContext>);
        step.Name = name;
        step.ParentName = CurrentStep.Name;
        step.Action = (Func<BaseStepContext, Task>) (ctx => action((StepContext<TCustomStepContext>) ctx));

        await ExecuteStepHandler.ExecuteStep(step);
    }

    public void Step(string name, Action<StepContext<TCustomStepContext>> action)
    {
        var stepType = typeof(Step<>).MakeGenericType(typeof(TCustomStepContext));
        var step = (BaseStep?) Activator.CreateInstance(stepType);

        if (step == null)
        {
            throw new InvalidOperationException($"Failed to create an instance of type {stepType.FullName}");
        }

        step.ContextType = typeof(StepContext<TCustomStepContext>);
        step.Name = name;
        step.ParentName = CurrentStep.Name;
        step.Action = (Func<BaseStepContext, Task>) (ctx =>
        {
            action((StepContext<TCustomStepContext>) ctx);
            return Task.CompletedTask;
        });

        ExecuteStepHandler.ExecuteStep(step).GetAwaiter().GetResult();
    }
}
