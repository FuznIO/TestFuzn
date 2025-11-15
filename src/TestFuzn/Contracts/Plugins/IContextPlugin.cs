namespace Fuzn.TestFuzn.Contracts.Plugins;

public interface IContextPlugin
{
    bool RequireState { get; }
    Task InitGlobal();
    Task CleanupGlobal();
    object InitContext();
    Task CleanupContext(object state);
}