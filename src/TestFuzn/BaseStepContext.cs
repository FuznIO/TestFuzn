using FuznLabs.TestFuzn.Internals.Execution;

namespace FuznLabs.TestFuzn;

public abstract class BaseStepContext : Context
{
    public Scenario Scenario => IterationContext.Scenario;

    public BaseStepContext()
    {
    }

    public T InputData<T>()
    {
        try
        {
            return (T) IterationContext.InputData;
        }
        catch (InvalidCastException)
        {
            throw new InvalidCastException($"Input data is not of type {typeof(T).Name}");
        }
    }

    public T GetSharedData<T>(string key)
    {
        if (IterationContext.SharedData.TryGetValue(key, out var value))
        {
            return (T) value;
        }
        throw new KeyNotFoundException($"Key '{key}' not found in StepContext.Data.");
    }

    public void SetSharedData(string key, object value)
    {
        IterationContext.SharedData[key] = value;
    }

    protected async Task ExecuteStep(string name, Type contextType, Func<BaseStepContext, Task> action)
    {
        var step = new Step();
        step.ContextType = contextType;
        step.Name = name;
        step.ParentName = CurrentStep.Name;
        step.Action = action;
        await IterationContext.ExecuteStepHandler.ExecuteStep(ExecuteStepHandler.StepType.Inner, step);
    }
}

