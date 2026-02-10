namespace Fuzn.TestFuzn.Plugins.Http.Internals;

internal class HttpRequestLog
{
    public DateTime Timestamp { get; set; }
    public string Method { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public int? StatusCode { get; set; }
    public string? ReasonPhrase { get; set; }
    public long DurationMs { get; set; }
    public string? CorrelationId { get; set; }
    public string FormattedRequest { get; set; } = string.Empty;
    public string? FormattedResponse { get; set; }

    public static HttpRequestLog Create(HttpRequestMessage request, string? requestBody, string correlationId)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"=== HTTP REQUEST ===");
        sb.AppendLine($"Timestamp: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}");
        sb.AppendLine($"Method: {request.Method}");
        sb.AppendLine($"URL: {request.RequestUri}");
        sb.AppendLine($"Correlation-ID: {correlationId}");
        sb.AppendLine();
        sb.AppendLine("--- Request Headers ---");
        sb.AppendLine(FormatHeaders(request.Headers, request.Content?.Headers));
        
        if (!string.IsNullOrEmpty(requestBody))
        {
            sb.AppendLine();
            sb.AppendLine("--- Request Body ---");
            sb.AppendLine(requestBody);
        }

        return new HttpRequestLog
        {
            Timestamp = DateTime.UtcNow,
            Method = request.Method.ToString(),
            Url = request.RequestUri?.ToString() ?? string.Empty,
            CorrelationId = correlationId,
            FormattedRequest = sb.ToString()
        };
    }

    public void SetResponse(HttpResponseMessage response, string? responseBody, long durationMs)
    {
        DurationMs = durationMs;
        StatusCode = (int)response.StatusCode;
        ReasonPhrase = response.ReasonPhrase;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"=== HTTP RESPONSE ===");
        sb.AppendLine($"Duration: {durationMs}ms");
        sb.AppendLine($"Status: {(int)response.StatusCode} {response.ReasonPhrase}");
        sb.AppendLine();
        sb.AppendLine("--- Response Headers ---");
        sb.AppendLine(FormatHeaders(response.Headers, response.Content?.Headers));
        
        if (!string.IsNullOrEmpty(responseBody))
        {
            sb.AppendLine();
            sb.AppendLine("--- Response Body ---");
            sb.AppendLine(responseBody);
        }

        FormattedResponse = sb.ToString();
    }

    public void SetException(Exception ex, long durationMs)
    {
        DurationMs = durationMs;
        
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"=== HTTP RESPONSE ===");
        sb.AppendLine($"Duration: {durationMs}ms");
        sb.AppendLine($"Exception: {ex.Message}");
        
        FormattedResponse = sb.ToString();
    }

    public string FormatRequest() => FormattedRequest;

    public string FormatResponse() => FormattedResponse ?? string.Empty;

    public string FormatFull()
    {
        return FormattedRequest + Environment.NewLine + FormattedResponse;
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
