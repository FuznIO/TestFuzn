using Microsoft.Extensions.DependencyInjection;

namespace Fuzn.TestFuzn.Contracts.Plugins;

public interface IContextPlugin
{
    bool RequireState { get; }
    bool RequireStepExceptionHandling { get; }
    
    Task InitSuite();
    Task CleanupSuite();
    object InitContext();
    Task HandleStepException(object state, IterationContext context, Exception exception);
    Task CleanupContext(object state);
}