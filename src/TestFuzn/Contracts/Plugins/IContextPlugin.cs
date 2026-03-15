namespace Fuzn.TestFuzn.Contracts.Plugins;

/// <summary>
/// Defines the contract for a context plugin that manages per-iteration state and
/// participates in the test suite lifecycle.
/// </summary>
public interface IContextPlugin
{
    /// <summary>
    /// Gets a value indicating whether this plugin requires per-iteration state.
    /// When <see langword="true"/>, <see cref="InitContext"/> is called for each iteration
    /// and the resulting state is stored in <see cref="ContextPluginsState"/>.
    /// </summary>
    bool RequireState { get; }

    /// <summary>
    /// Gets a value indicating whether this plugin should be notified when a step throws an exception.
    /// When <see langword="true"/>, <see cref="HandleStepException"/> is called on step failure.
    /// </summary>
    bool RequireStepExceptionHandling { get; }

    /// <summary>
    /// Performs one-time initialization when the test suite starts.
    /// Called before any test scenarios are executed.
    /// </summary>
    Task InitSuite();

    /// <summary>
    /// Performs cleanup when the test suite ends.
    /// Called after all test scenarios have completed.
    /// </summary>
    Task CleanupSuite();

    /// <summary>
    /// Creates a new state object for a single iteration.
    /// The returned object is stored and later passed to <see cref="HandleStepException"/>
    /// and <see cref="CleanupContext"/>.
    /// </summary>
    /// <param name="serviceProvider">The scoped service provider for the current iteration.</param>
    /// <returns>An opaque state object managed by this plugin.</returns>
    object InitContext(IServiceProvider serviceProvider);

    /// <summary>
    /// Handles an exception that occurred during step execution.
    /// Use this to capture diagnostics such as logs, screenshots, or request details.
    /// </summary>
    /// <param name="state">The plugin state created by <see cref="InitContext"/>.</param>
    /// <param name="context">The iteration context for the current step.</param>
    /// <param name="exception">The exception thrown by the step.</param>
    Task HandleStepException(object state, IterationContext context, Exception exception);

    /// <summary>
    /// Cleans up the per-iteration state after a scenario iteration completes.
    /// </summary>
    /// <param name="state">The plugin state created by <see cref="InitContext"/>.</param>
    Task CleanupContext(object state);
}