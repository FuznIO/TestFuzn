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
        var testType = context.IterationState.Scenario?.TestType ?? Contracts.TestType.Standard;

        var correlationId = context.Info.CorrelationId;
        var stepName = context.StepInfo?.Name ?? "Unknown";

        InjectCorrelationIdHeader(request, correlationId);

        string? requestBody = null;
        if (verbosity == LoggingVerbosity.Full && request.Content != null)
        {
            requestBody = await request.Content.ReadAsStringAsync(cancellationToken);
        }

        if (verbosity >= LoggingVerbosity.Normal)
        {
            context.Logger.LogInformation($"Step {stepName} - HTTP Request: {request.Method} {request.RequestUri} - CorrelationId: {correlationId}");
            
            if (verbosity == LoggingVerbosity.Full && requestBody != null)
            {
                context.Logger.LogInformation($"Step {stepName} - Request Body: {requestBody} - CorrelationId: {correlationId}");
            }
        }

        HttpRequestLog? requestLog = null;
        if (verbosity == LoggingVerbosity.Full && state != null)
        {
            requestLog = HttpRequestLog.Create(request, requestBody, correlationId);
        }

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var response = await base.SendAsync(request, cancellationToken);
            stopwatch.Stop();
            
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            LogResponse(context, request, stepName, correlationId, verbosity, response, responseBody);

            if (requestLog != null && state != null)
            {
                requestLog.SetResponse(response, responseBody, stopwatch.ElapsedMilliseconds);
                state.AddRequestLog(requestLog);
            }

            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            if (verbosity  >= LoggingVerbosity.Normal)
            {
                context.Logger.LogError(ex, $"Step {stepName} - HTTP Request failed - CorrelationId: {correlationId}");
            }

            if (requestLog != null && state != null)
            {
                requestLog.SetException(ex, stopwatch.ElapsedMilliseconds);
                state.AddRequestLog(requestLog);
            }

            throw;
        }
    }

    private static void InjectCorrelationIdHeader(HttpRequestMessage request, string correlationId)
    {
        var correlationHeaderName = HttpGlobalState.Configuration.CorrelationIdHeaderName;
        if (!request.Headers.Contains(correlationHeaderName))
        {
            request.Headers.TryAddWithoutValidation(correlationHeaderName, correlationId);
        }
    }

    private static void LogResponse(Context context, HttpRequestMessage request, string stepName, string correlationId, LoggingVerbosity verbosity, HttpResponseMessage response, string? responseBody)
    {
        if (verbosity >= LoggingVerbosity.Normal)
        {
            context.Logger.LogInformation($"Step {stepName} - HTTP Response: {(int)response.StatusCode} {response.ReasonPhrase} - CorrelationId: {correlationId}");

            if (verbosity == LoggingVerbosity.Full && responseBody != null)
            {
                context.Logger.LogInformation($"Step {stepName} - Response Body: {responseBody} - CorrelationId: {correlationId}");
            }
        }

        if (!response.IsSuccessStatusCode && HttpGlobalState.Configuration?.LogFailedRequestsToTestConsole == true && verbosity == LoggingVerbosity.Full)
        {
            context.Logger.LogError($"Step {stepName} - Request returned an error:\n{request} - CorrelationId: {correlationId}");
            context.Logger.LogError($"Step {stepName} - Response:\n{response} - CorrelationId: {correlationId}");
            context.Logger.LogError($"Step {stepName} - Response.Body:\n{responseBody} - CorrelationId: {correlationId}");
        }
    }
}
