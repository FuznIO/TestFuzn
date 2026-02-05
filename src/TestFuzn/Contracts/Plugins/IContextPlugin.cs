using Microsoft.Extensions.DependencyInjection;

namespace Fuzn.TestFuzn.Contracts.Plugins;

public interface IContextPlugin
{
    bool RequireState { get; }
    bool RequireStepExceptionHandling { get; }
    
    /// <summary>
    /// Configures services for the plugin. Called during startup before InitSuite.
    /// </summary>
    /// <param name="services">The service collection to register services with.</param>
    void ConfigureServices(IServiceCollection services) { }
    
    Task InitSuite();
    Task CleanupSuite();
    object InitContext();
    Task HandleStepException(object state, IterationContext context, Exception exception);
    Task CleanupContext(object state);
}