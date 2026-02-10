namespace Fuzn.TestFuzn.Plugins.Http.Internals;

internal class HttpRequestLog
{
    public DateTime Timestamp { get; set; }
    public string Method { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string RequestHeaders { get; set; } = string.Empty;
    public string? RequestBody { get; set; }
    public int? StatusCode { get; set; }
    public string? ReasonPhrase { get; set; }
    public string? ResponseHeaders { get; set; }
    public string? ResponseBody { get; set; }
    public long DurationMs { get; set; }
    public string? CorrelationId { get; set; }
    public string? ExceptionMessage { get; set; }
    public string FormatRequest()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"=== HTTP REQUEST ===");
        sb.AppendLine($"Timestamp: {Timestamp:yyyy-MM-dd HH:mm:ss.fff}");
        sb.AppendLine($"Method: {Method}");
        sb.AppendLine($"URL: {Url}");
        sb.AppendLine($"Correlation-ID: {CorrelationId}");
        sb.AppendLine();
        sb.AppendLine("--- Request Headers ---");
        sb.AppendLine(RequestHeaders);
        if (!string.IsNullOrEmpty(RequestBody))
        {
            sb.AppendLine();
            sb.AppendLine("--- Request Body ---");
            sb.AppendLine(RequestBody);
        }
        return sb.ToString();
    }

    /// <summary>
    /// Formats the response details as a string suitable for logging or attachment.
    /// </summary>
    public string FormatResponse()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"=== HTTP RESPONSE ===");
        sb.AppendLine($"Duration: {DurationMs}ms");
        if (StatusCode.HasValue)
        {
            sb.AppendLine($"Status: {StatusCode} {ReasonPhrase}");
            sb.AppendLine();
            sb.AppendLine("--- Response Headers ---");
            sb.AppendLine(ResponseHeaders);
            if (!string.IsNullOrEmpty(ResponseBody))
            {
                sb.AppendLine();
                sb.AppendLine("--- Response Body ---");
                sb.AppendLine(ResponseBody);
            }
        }
        else if (!string.IsNullOrEmpty(ExceptionMessage))
        {
            sb.AppendLine($"Exception: {ExceptionMessage}");
        }
        return sb.ToString();
    }

    /// <summary>
    /// Formats both request and response as a combined string.
    /// </summary>
    public string FormatFull()
    {
        return FormatRequest() + Environment.NewLine + FormatResponse();
    }
}
