using System.Collections.Concurrent;

namespace Fuzn.TestFuzn.Plugins.Http.Internals;

internal class HttpPluginState
{
    private readonly ConcurrentQueue<string> _requestLogs = new();

    public void AddLog(string log)
    {
        _requestLogs.Enqueue(log);
    }

    public List<string> GetLogs()
    {
        return _requestLogs.ToList();
    }

    public void Clear()
    {
        _requestLogs.Clear();
    }
}
