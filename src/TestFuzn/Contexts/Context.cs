using Fuzn.TestFuzn.Contracts.Adapters;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;

namespace Fuzn.TestFuzn;

/// <summary>
/// Provides contextual information and services for test execution, including execution metadata,
/// logging, and dependency injection capabilities.
/// </summary>
public class Context
{
    internal IterationState IterationState { get; set; }
    internal ITestFrameworkAdapter TestFramework => IterationState.TestFramework;
    
    /// <summary>
    /// Gets execution information for the current test iteration, including scenario details and timing.
    /// </summary>
    public ExecutionInfo Info => IterationState.Info;
    
    /// <summary>
    /// Gets internal context utilities for advanced framework operations.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public ContextInternals Internals => IterationState.Internals;
    
    /// <summary>
    /// Gets the logger for recording test execution events and diagnostics.
    /// </summary>
    public ILogger Logger => IterationState.Info.TestSession.Logger; 
    
    /// <summary>
    /// Gets information about the currently executing step, including its name and hierarchy.
    /// </summary>
    public StepInfo StepInfo { get; internal set; }
    
    /// <summary>
    /// Gets the service provider for resolving dependencies from the IoC container.
    /// </summary>
    public IServiceProvider ServicesProvider => IterationState.ServiceProvider;

    /// <summary>
    /// Gets the file manager for loading test data from CSV and JSON files.
    /// </summary>
    public FileManager Files => IterationState.ServiceProvider.GetRequiredService<FileManager>();

    /// <summary>
    /// Gets the configuration manager for accessing values from appsettings.json and environment-specific configuration files.
    /// </summary>
    public ConfigurationManager Configuration => IterationState.ServiceProvider.GetRequiredService<ConfigurationManager>();
}
