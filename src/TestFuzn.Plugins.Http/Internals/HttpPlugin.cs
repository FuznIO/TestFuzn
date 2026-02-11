using Fuzn.TestFuzn.Contracts.Plugins;

namespace Fuzn.TestFuzn.Plugins.Http.Internals;

internal class HttpPlugin : IContextPlugin
{
    public bool RequireState => true;
    public bool RequireStepExceptionHandling => true;

    public Task InitSuite()
    {
        return Task.CompletedTask;
    }

    public Task CleanupSuite()
    {
        return Task.CompletedTask;
    }

    public object InitContext()
    {
        return new HttpPluginState();
    }

    public Task CleanupContext(object state)
    {
        if (state is HttpPluginState httpState)
        {
            httpState.Clear();
        }
        return Task.CompletedTask;
    }

    public async Task HandleStepException(object state, IterationContext context, Exception exception)
    {
        if (state is not HttpPluginState httpState)
            return;

        var verbosity = GlobalState.LoggingVerbosity;

        if (verbosity != LoggingVerbosity.Full)
            return;

        if (context.IterationState.Scenario?.TestType == Contracts.TestType.Load)
            return;

        var requestLogs = httpState.GetLogs();
        if (requestLogs.Count == 0)
            return;

        context.Comment($"HTTP Plugin: {requestLogs.Count} HTTP request(s) captured during this step");

        for (int i = 0; i < requestLogs.Count; i++)
        {
            var log = requestLogs[i];
            var index = i + 1;
            var fileName = $"http-log-{index}.txt";

            await context.Attach(fileName, log);
        }
    }
}
