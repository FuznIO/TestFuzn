namespace FuznLabs.TestFuzn;

public abstract class BaseStep
{
    public string Name { get; set; }
    internal string ParentName { get; set; }
    public Func<BaseStepContext, Task> Action { get; set; }
    public Type ContextType { get; set; }
    internal List<BaseStep> Steps { get; set; }
}

public class Step<TStepContext> : BaseStep
    where TStepContext : StepContext<TStepContext>, new()
{
    public Step()
    {
        ContextType = typeof(TStepContext);
    }
}