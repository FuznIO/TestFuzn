using System.Collections.Concurrent;

namespace Fuzn.TestFuzn.Plugins.Http.Internals;

/// <summary>
/// Maintains per-iteration state for the HTTP plugin.
/// Tracks HTTP requests made during a test iteration for debugging on failure.
/// </summary>
internal class HttpPluginState
{
    private readonly ConcurrentQueue<HttpRequestLog> _requestLogs = new();

    /// <summary>
    /// Adds a request log entry to the state.
    /// </summary>
    /// <param name="log">The request log to add.</param>
    public void AddRequestLog(HttpRequestLog log)
    {
        _requestLogs.Enqueue(log);
    }

    /// <summary>
    /// Gets all logged requests and clears the internal buffer.
    /// </summary>
    /// <returns>A list of all logged HTTP requests.</returns>
    public List<HttpRequestLog> GetAndClearRequestLogs()
    {
        var logs = new List<HttpRequestLog>();
        while (_requestLogs.TryDequeue(out var log))
        {
            logs.Add(log);
        }
        return logs;
    }

    /// <summary>
    /// Gets all logged requests without clearing the buffer.
    /// </summary>
    /// <returns>A list of all logged HTTP requests.</returns>
    public List<HttpRequestLog> GetRequestLogs()
    {
        return _requestLogs.ToList();
    }

    /// <summary>
    /// Gets the count of logged requests.
    /// </summary>
    public int RequestCount => _requestLogs.Count;

    /// <summary>
    /// Clears all logged requests.
    /// </summary>
    public void Clear()
    {
        while (_requestLogs.TryDequeue(out _)) { }
    }
}
