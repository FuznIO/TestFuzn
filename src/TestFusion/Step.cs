namespace TestFusion;

public abstract class BaseStep
{
    public string Name { get; set; }
    public Func<StepContext, Task> Action { get; set; }
    public Type ContextType { get; set; }
}

public class Step<TStepContext> : BaseStep
    where TStepContext : StepContext, new()
{
    internal Step()
    {
        ContextType = typeof(TStepContext);
    }
}