using Microsoft.Extensions.Logging;
using System.Text;

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

        StringBuilder? logBuilder = null;
        if (verbosity == LoggingVerbosity.Full && state != null && testType == Contracts.TestType.Standard)
        {
            logBuilder = new StringBuilder();
            AppendRequest(logBuilder, request, requestBody, correlationId);
        }

        try
        {
            var response = await base.SendAsync(request, cancellationToken);
            
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            LogResponse(context, request, stepName, correlationId, verbosity, response, responseBody);

            if (logBuilder != null && state != null)
            {
                AppendResponse(logBuilder, response, responseBody);
                state.AddLog(logBuilder.ToString());
            }

            return response;
        }
        catch (Exception ex)
        {
            if (verbosity  >= LoggingVerbosity.Normal)
            {
                context.Logger.LogError(ex, $"Step {stepName} - HTTP Request failed - CorrelationId: {correlationId}");
            }

            if (logBuilder != null && state != null)
            {
                AppendException(logBuilder, ex);
                state.AddLog(logBuilder.ToString());
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

    private static void AppendRequest(StringBuilder sb, HttpRequestMessage request, string? requestBody, string correlationId)
    {
        sb.AppendLine($"=== HTTP REQUEST ===");
        sb.AppendLine($"Timestamp: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}");
        sb.AppendLine($"Method: {request.Method}");
        sb.AppendLine($"URL: {request.RequestUri}");
        sb.AppendLine($"Correlation-ID: {correlationId}");
        sb.AppendLine();
        sb.AppendLine("--- Request Headers ---");
        AppendHeaders(sb, request.Headers, request.Content?.Headers);
        
        if (!string.IsNullOrEmpty(requestBody))
        {
            sb.AppendLine();
            sb.AppendLine("--- Request Body ---");
            sb.AppendLine(requestBody);
        }
    }

    private static void AppendResponse(StringBuilder sb, HttpResponseMessage response, string? responseBody)
    {
        sb.AppendLine();
        sb.AppendLine($"=== HTTP RESPONSE ===");
        sb.AppendLine($"Status: {(int)response.StatusCode} {response.ReasonPhrase}");
        sb.AppendLine();
        sb.AppendLine("--- Response Headers ---");
        AppendHeaders(sb, response.Headers, response.Content?.Headers);
        
        if (!string.IsNullOrEmpty(responseBody))
        {
            sb.AppendLine();
            sb.AppendLine("--- Response Body ---");
            sb.AppendLine(responseBody);
        }
    }

    private static void AppendException(StringBuilder sb, Exception ex)
    {
        sb.AppendLine();
        sb.AppendLine($"=== HTTP RESPONSE ===");
        sb.AppendLine($"Exception: {ex.Message}");
    }

    private static void AppendHeaders(StringBuilder sb, System.Net.Http.Headers.HttpHeaders? headers, System.Net.Http.Headers.HttpContentHeaders? contentHeaders)
    {
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
    }
}
