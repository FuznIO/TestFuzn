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
            httpState.LatestRequest = null;
        }
        return Task.CompletedTask;
    }

    public async Task HandleStepException(object state, IterationContext context, Exception exception)
    {
        if (state is not HttpPluginState httpState)
            return;

        var verbosity = GlobalState.LoggingVerbosity;
        var testType = context.IterationState.Scenario?.TestType ?? Contracts.TestType.Standard;
        var writeHttpDetailsOnStepFailure = HttpGlobalState.Configuration?.WriteHttpDetailsToConsoleOnStepFailure ?? false;

        // Only process for standard tests, not load tests
        if (testType == Contracts.TestType.Load)
            return;

        var latestRequest = httpState.LatestRequest;
        if (latestRequest == null)
            return;

        // Write HTTP details to console if enabled (happens when step fails)
        if (writeHttpDetailsOnStepFailure)
        {
            context.TestFramework.WriteMarkup($"[red]HTTP Plugin: Latest HTTP request captured during failed step[/]");
            context.TestFramework.WriteMarkup("");
            context.TestFramework.WriteMarkup("[grey]" + latestRequest.Replace("[", "[[") + "[/]");
            context.TestFramework.WriteMarkup("");
        }

        // Attach logs as files if verbosity is Full
        if (verbosity == LoggingVerbosity.Full)
        {
            context.Comment("HTTP Plugin: Latest HTTP request captured during this step");
            await context.Attach("http-request-response.txt", latestRequest);
        }
    }
}
