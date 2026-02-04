using Microsoft.Extensions.Logging;

namespace Fuzn.TestFuzn.Plugins.Http.Internals;

/// <summary>
/// A DelegatingHandler that adds TestFuzn-specific logging and correlation ID injection.
/// </summary>
internal class TestFuznLoggingHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Get context from AsyncLocal storage
        var context = TestFuznHttpContext.Current;
        var verbosity = HttpGlobalState.Configuration?.LoggingVerbosity ?? LoggingVerbosity.Normal;

        // Inject correlation ID header
        var correlationHeaderName = HttpGlobalState.Configuration?.CorrelationIdHeaderName ?? "X-Correlation-ID";
        if (context != null && !request.Headers.Contains(correlationHeaderName))
        {
            request.Headers.TryAddWithoutValidation(correlationHeaderName, context.Info.CorrelationId);
        }

        var stepName = context?.StepInfo?.Name ?? "Unknown";
        var correlationId = context?.Info.CorrelationId ?? "N/A";

        // Log request
        if (verbosity >= LoggingVerbosity.Normal)
        {
            context?.Logger.LogInformation($"Step {stepName} - HTTP Request: {request.Method} {request.RequestUri} - CorrelationId: {correlationId}");
        }

        if (verbosity == LoggingVerbosity.Full && request.Content != null)
        {
            var requestBody = await request.Content.ReadAsStringAsync(cancellationToken);
            context?.Logger.LogInformation($"Step {stepName} - Request Body: {requestBody} - CorrelationId: {correlationId}");
        }

        HttpResponseMessage? response = null;
        string? responseBody = null;

        try
        {
            response = await base.SendAsync(request, cancellationToken);
            responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            // Log response
            if (verbosity >= LoggingVerbosity.Normal)
            {
                context?.Logger.LogInformation($"Step {stepName} - HTTP Response: {(int)response.StatusCode} {response.ReasonPhrase} - CorrelationId: {correlationId}");
            }

            if (verbosity == LoggingVerbosity.Full && responseBody != null)
            {
                context?.Logger.LogInformation($"Step {stepName} - Response Body: {responseBody} - CorrelationId: {correlationId}");
            }

            // Log errors
            if (!response.IsSuccessStatusCode && HttpGlobalState.Configuration?.LogFailedRequestsToTestConsole == true)
            {
                if (verbosity == LoggingVerbosity.Full)
                {
                    context?.Logger.LogError($"Step {stepName} - Request returned an error:\n{request} - CorrelationId: {correlationId}");
                    context?.Logger.LogError($"Step {stepName} - Response:\n{response} - CorrelationId: {correlationId}");
                    context?.Logger.LogError($"Step {stepName} - Response.Body:\n{responseBody} - CorrelationId: {correlationId}");
                }
            }

            return response;
        }
        catch (Exception ex)
        {
            if (verbosity > LoggingVerbosity.None)
            {
                context?.Logger.LogError(ex, $"Step {stepName} - HTTP Request failed - CorrelationId: {correlationId}");
            }

            throw;
        }
    }
}

/// <summary>
/// Provides AsyncLocal storage for the current TestFuzn context during HTTP requests.
/// </summary>
internal static class TestFuznHttpContext
{
    private static readonly AsyncLocal<Context?> _current = new();

    /// <summary>
    /// Gets or sets the current context for the async flow.
    /// </summary>
    public static Context? Current
    {
        get => _current.Value;
        set => _current.Value = value;
    }
}
