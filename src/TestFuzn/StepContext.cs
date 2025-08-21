namespace FuznLabs.TestFuzn;

public class StepContext : Context
{
    public Scenario Scenario { get; internal set; }
    internal Dictionary<string, object> SharedData { get; } = new Dictionary<string, object>();
    internal object InputDataInternal { get; set; }

    public StepContext()
    {
    }

    public T InputData<T>()
    {
        try
        {
            return (T) InputDataInternal;
        }
        catch (InvalidCastException)
        {
            throw new InvalidCastException($"Input data is not of type {typeof(T).Name}");
        }
    }

    public T GetSharedData<T>(string key)
    {
        if (SharedData.TryGetValue(key, out var value))
        {
            return (T) value;
        }
        throw new KeyNotFoundException($"Key '{key}' not found in StepContext.Data.");
    }

    public void SetSharedData(string key, object value)
    {
        SharedData[key] = value;
    }


}

