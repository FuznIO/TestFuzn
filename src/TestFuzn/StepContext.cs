namespace Fuzn.TestFuzn;

public sealed class StepContext : BaseStepContext
{
    public async Task Step(string name, Func<StepContext, Task> action)
    {
        await ExecuteStep(name,
            typeof(StepContext),
            ctx => action((StepContext) ctx));
    }

    public void Step(string name, Action<StepContext> action)
    {
        ExecuteStep(name, 
            typeof(StepContext),
            (ctx) =>
            {
                action((StepContext) ctx);
                return Task.CompletedTask;
            }).GetAwaiter().GetResult();
    }
}

public sealed class StepContext<TCustomStepContext> : BaseStepContext
    where TCustomStepContext : new()
{
    public TCustomStepContext Custom => (TCustomStepContext) IterationContext.Custom;

    public async Task Step(string name, Func<StepContext<TCustomStepContext>, Task> action)
    {
        await ExecuteStep(name,
            typeof(StepContext<TCustomStepContext>),
            ctx => action((StepContext<TCustomStepContext>) ctx));
    }

    public void Step(string name, Action<StepContext<TCustomStepContext>> action)
    {
        ExecuteStep(name, 
            typeof(StepContext<TCustomStepContext>),
            (ctx) =>
            {
                action((StepContext<TCustomStepContext>) ctx);
                return Task.CompletedTask;
            }).GetAwaiter().GetResult();
    }
}
