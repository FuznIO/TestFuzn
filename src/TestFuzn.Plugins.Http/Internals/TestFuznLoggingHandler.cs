using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Fuzn.TestFuzn.Plugins.Http.Internals;

/// <summary>
/// A DelegatingHandler that adds TestFuzn-specific logging and correlation ID injection.
/// </summary>
internal class TestFuznLoggingHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var context = request.Options.GetTestFuznContext();
        var state = request.Options.GetTestFuznState();

        var verbosity = GlobalState.LoggingVerbosity;

        // Inject correlation ID header
        var correlationId = context.Info.CorrelationId;
        var correlationHeaderName = HttpGlobalState.Configuration?.CorrelationIdHeaderName;
        if (!request.Headers.Contains(correlationHeaderName))
        {
            request.Headers.TryAddWithoutValidation(correlationHeaderName, correlationId);
        }

        var stepName = context.StepInfo?.Name ?? "Unknown";

        // Log request
        if (verbosity >= LoggingVerbosity.Normal)
        {
            context.Logger.LogInformation($"Step {stepName} - HTTP Request: {request.Method} {request.RequestUri} - CorrelationId: {correlationId}");
        }

        string? requestBody = null;
        if (verbosity == LoggingVerbosity.Full && request.Content != null)
        {
            requestBody = await request.Content.ReadAsStringAsync(cancellationToken);
            context.Logger.LogInformation($"Step {stepName} - Request Body: {requestBody} - CorrelationId: {correlationId}");
        }

        // Create request log for tracking (only when verbosity is Full)
        HttpRequestLog? requestLog = null;
        if (verbosity == LoggingVerbosity.Full && state != null)
        {
            requestLog = new HttpRequestLog
            {
                Timestamp = DateTime.UtcNow,
                Method = request.Method.ToString(),
                Url = request.RequestUri?.ToString() ?? string.Empty,
                RequestHeaders = FormatHeaders(request.Headers, request.Content?.Headers),
                RequestBody = requestBody,
                CorrelationId = correlationId
            };
        }

        var stopwatch = Stopwatch.StartNew();
        HttpResponseMessage? response = null;
        string? responseBody = null;

        try
        {
            response = await base.SendAsync(request, cancellationToken);
            stopwatch.Stop();
            responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            // Log response
            if (verbosity >= LoggingVerbosity.Normal)
            {
                context.Logger.LogInformation($"Step {stepName} - HTTP Response: {(int)response.StatusCode} {response.ReasonPhrase} - CorrelationId: {correlationId}");
            }

            if (verbosity == LoggingVerbosity.Full && responseBody != null)
            {
                context.Logger.LogInformation($"Step {stepName} - Response Body: {responseBody} - CorrelationId: {correlationId}");
            }

            // Log errors
            if (!response.IsSuccessStatusCode && HttpGlobalState.Configuration?.LogFailedRequestsToTestConsole == true)
            {
                if (verbosity == LoggingVerbosity.Full)
                {
                    context.Logger.LogError($"Step {stepName} - Request returned an error:\n{request} - CorrelationId: {correlationId}");
                    context.Logger.LogError($"Step {stepName} - Response:\n{response} - CorrelationId: {correlationId}");
                    context.Logger.LogError($"Step {stepName} - Response.Body:\n{responseBody} - CorrelationId: {correlationId}");
                }
            }

            // Record to state for potential attachment on failure
            if (requestLog != null && state != null)
            {
                requestLog.DurationMs = stopwatch.ElapsedMilliseconds;
                requestLog.StatusCode = (int)response.StatusCode;
                requestLog.ReasonPhrase = response.ReasonPhrase;
                requestLog.ResponseHeaders = FormatHeaders(response.Headers, response.Content?.Headers);
                requestLog.ResponseBody = responseBody;
                state.AddRequestLog(requestLog);
            }

            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            if (verbosity > LoggingVerbosity.None)
            {
                context.Logger.LogError(ex, $"Step {stepName} - HTTP Request failed - CorrelationId: {correlationId}");
            }

            // Record failed request to state
            if (requestLog != null && state != null)
            {
                requestLog.DurationMs = stopwatch.ElapsedMilliseconds;
                requestLog.ExceptionMessage = ex.Message;
                state.AddRequestLog(requestLog);
            }

            throw;
        }
    }

    private static string FormatHeaders(System.Net.Http.Headers.HttpHeaders? headers, System.Net.Http.Headers.HttpContentHeaders? contentHeaders)
    {
        var sb = new System.Text.StringBuilder();

        if (headers != null)
        {
            foreach (var header in headers)
            {
                sb.AppendLine($"{header.Key}: {string.Join(", ", header.Value)}");
            }
        }

        if (contentHeaders != null)
        {
            foreach (var header in contentHeaders)
            {
                sb.AppendLine($"{header.Key}: {string.Join(", ", header.Value)}");
            }
        }

        return sb.ToString().TrimEnd();
    }
}
