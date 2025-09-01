namespace FuznLabs.TestFuzn;

public class StepContext : StepContext<StepContext>
{
}

public class StepContext<TStepContext> : BaseStepContext
    where TStepContext : StepContext<TStepContext>
{
    public async Task Step(string name, Func<TStepContext, Task> action)
    {
        var stepType = typeof(Step<>).MakeGenericType(typeof(TStepContext));
        var step = (BaseStep?) Activator.CreateInstance(stepType);

        if (step == null)
        {
            throw new InvalidOperationException($"Failed to create an instance of type {stepType.FullName}");
        }

        step.ContextType = typeof(TStepContext);
        step.Name = name;
        step.ParentName = CurrentStep.Name;
        step.Action = (Func<BaseStepContext, Task>) (ctx => action((TStepContext) ctx));

        await ExecuteStepHandler.ExecuteStep(step);
    }

    public void Step(string name, Action<TStepContext> action)
    {
        var stepType = typeof(Step<>).MakeGenericType(typeof(TStepContext));
        var step = (BaseStep?) Activator.CreateInstance(stepType);

        if (step == null)
        {
            throw new InvalidOperationException($"Failed to create an instance of type {stepType.FullName}");
        }

        step.ContextType = typeof(TStepContext);
        step.Name = name;
        step.ParentName = CurrentStep.Name;
        step.Action = (Func<BaseStepContext, Task>) (ctx =>
        {
            action((TStepContext) ctx);
            return Task.CompletedTask;
        });

        ExecuteStepHandler.ExecuteStep(step).GetAwaiter().GetResult();
    }
}
