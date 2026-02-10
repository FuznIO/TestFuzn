using Fuzn.TestFuzn.Contracts.Plugins;
using Microsoft.Extensions.Logging;

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

        // Only attach HTTP logs when verbosity is Full
        if (verbosity != LoggingVerbosity.Full)
            return;

        var requestLogs = httpState.GetAndClearRequestLogs();
        if (requestLogs.Count == 0)
            return;

        context.Comment($"HTTP Plugin: {requestLogs.Count} HTTP request(s) captured during this step");

        for (int i = 0; i < requestLogs.Count; i++)
        {
            var log = requestLogs[i];
            var index = i + 1;

            // Output summary to console
            context.Logger.LogError($"HTTP Request {index}/{requestLogs.Count}: {log.Method} {log.Url} -> {log.StatusCode} {log.ReasonPhrase} ({log.DurationMs}ms)");

            // Output full details to console
            context.Logger.LogError(log.FormatFull());

            // Attach as files
            var requestFileName = $"http-request-{index}.txt";
            var responseFileName = $"http-response-{index}.txt";

            await context.Attach(requestFileName, log.FormatRequest());
            await context.Attach(responseFileName, log.FormatResponse());
        }
    }
}
