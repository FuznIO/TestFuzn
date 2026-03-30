using Fuzn.TestFuzn.Contracts.Plugins;

namespace Fuzn.TestFuzn.Tests.Cancellation;

public class TrackingContextPlugin : IContextPlugin
{
    public bool RequireIterationState => true;
    public bool RequireStepExceptionHandling => false;

    public Task InitSuite() => Task.CompletedTask;
    public Task CleanupSuite() => Task.CompletedTask;

    public object InitIteration(IServiceProvider serviceProvider)
    {
        return new TrackingState();
    }

    public Task HandleStepException(object state, IterationContext context, Exception exception)
        => Task.CompletedTask;

    public Task CleanupIteration(object state)
    {
        if (state is TrackingState tracking)
            tracking.MarkCleanedUp();
        return Task.CompletedTask;
    }

    public class TrackingState
    {
        public bool CleanedUp { get; private set; }
        internal void MarkCleanedUp() => CleanedUp = true;
    }
}
