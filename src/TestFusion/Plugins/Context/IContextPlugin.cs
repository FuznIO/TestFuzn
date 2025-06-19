namespace TestFusion.Plugins.Context;

public interface IContextPlugin
{
    bool RequireState { get; }
    Task InitGlobal();
    Task CleanupGlobal();
    object InitContext();
    Task CleanupContext(object state);
}