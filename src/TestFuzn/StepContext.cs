using FuznLabs.TestFuzn.Internals.Execution;

namespace FuznLabs.TestFuzn;

public sealed class StepContext : BaseStepContext
{
    public async Task Step(string name, Func<StepContext, Task> action)
    {
        var stepType = typeof(Step<>).MakeGenericType(typeof(StepContext));
        var step = (BaseStep?) Activator.CreateInstance(stepType);

        if (step == null)
        {
            throw new InvalidOperationException($"Failed to create an instance of type {stepType.FullName}");
        }

        step.ContextType = typeof(StepContext);
        step.Name = name;
        step.ParentName = CurrentStep.Name;
        step.Action = (ctx => action((StepContext) ctx));

        await ExecuteStepHandler.ExecuteStep(step);
    }

    public void Step(string name, Action<StepContext> action)
    {
        var stepType = typeof(Step<>).MakeGenericType(typeof(StepContext));
        var step = (BaseStep?) Activator.CreateInstance(stepType);

        if (step == null)
        {
            throw new InvalidOperationException($"Failed to create an instance of type {stepType.FullName}");
        }

        step.ContextType = typeof(StepContext);
        step.Name = name;
        step.ParentName = CurrentStep.Name;
        step.Action = ctx =>
        {
            action((StepContext) ctx);
            return Task.CompletedTask;
        };

        ExecuteStepHandler.ExecuteStep(step).GetAwaiter().GetResult();
    }
}

public sealed class StepContext<TCustomStepContext> : BaseStepContext
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
