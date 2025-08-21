namespace FuznLabs.TestFuzn;

public class ContextPluginsState
{
    private Dictionary<Type, object> _pluginStates = new Dictionary<Type, object>();    

    public void SetState(Type pluginType, object state)
    {
        _pluginStates.Add(pluginType, state);
    }

    public object GetState(Type pluginType)
    {
        return _pluginStates[pluginType];
    }    

    public T GetState<T>(Type pluginType) where T : class
    {
        var state = GetState(pluginType);
        var castedState = state as T;
        return castedState ?? throw new InvalidCastException($"{state.GetType()} cannot be cast to {typeof(T)}");
    }    
}
