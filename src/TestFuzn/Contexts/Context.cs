using Fuzn.TestFuzn.Contracts.Adapters;
using Fuzn.TestFuzn.Internals;

namespace Fuzn.TestFuzn;

/// <summary>
/// Provides contextual information and services for test execution, including execution metadata,
/// logging, and dependency injection capabilities.
/// </summary>
public class Context
{
    internal ITestFrameworkAdapter TestFramework => IterationState.TestFramework;
    internal IterationState IterationState { get; set; }

    /// <summary>
    /// Gets execution information for the current test iteration, including scenario details and timing.
    /// </summary>
    public ExecutionInfo Info => IterationState.Info;
    
    /// <summary>
    /// Gets internal context utilities for advanced framework operations.
    /// </summary>
    public ContextInternals Internals => IterationState.Internals;
    
    /// <summary>
    /// Gets the logger for recording test execution events and diagnostics.
    /// </summary>
    public ILogger Logger => IterationState.Logger; 
    
    
    
    /// <summary>
    /// Gets information about the currently executing step, including its name and hierarchy.
    /// </summary>
    public StepInfo StepInfo { get; internal set; }
    
    /// <summary>
    /// Gets the service provider for resolving dependencies from the IoC container.
    /// </summary>
    public IServiceProvider Services => GlobalState.Configuration.ServiceProvider;
}
