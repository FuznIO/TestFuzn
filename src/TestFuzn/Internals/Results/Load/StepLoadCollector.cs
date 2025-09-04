using Fuzn.TestFuzn.Contracts.Results.Feature;
using Fuzn.TestFuzn.Contracts.Results.Load;

namespace Fuzn.TestFuzn.Internals.Results.Load;

internal class StepLoadCollector
{
    private readonly object _lock = new object();
    private int _maxDistinctErrorCount = 10;
    private string _name;
    private string _parentName;
    private StatsCollector _ok = new();
    private StatsCollector _failed = new();
    private int _skippedCount;
    private Dictionary<string, ErrorEntry> _errors = new();

    public StepLoadCollector(string name)
    {
        _name = name;
    }

    internal void Record(StepFeatureResult result, DateTime startTime, DateTime endTime)
    {
        lock (_lock)
        {
            if (result.Status == StepStatus.Passed)
            {
                _ok.Record(result.Duration, startTime, endTime);
            }
            else if (result.Status == StepStatus.Failed)
            {
                _failed.Record(result.Duration, startTime, endTime);
                StoreError(result.Exception);
            }
            else if (result.Status == StepStatus.Skipped)
            {
                _skippedCount++;
            }
        }
    }

    internal StepLoadResult GetCurrentResult()
    {
        var result = new StepLoadResult();
        result.Name = _name;
        result.Ok = _ok.GetCurrentResult();
        result.Failed = _failed.GetCurrentResult();
        result.SkippedCount = _skippedCount;

        if (_errors.Count > 0)
        {
            result.Errors = new Dictionary<string, ErrorEntry>(_errors);
        }

        return result;
    }

    private void StoreError(Exception exception)
    {
        if (exception == null)
            return;

        var key = exception.Message;

        if (_errors.TryGetValue(key, out var existing))
        {
            Interlocked.Increment(ref existing.Count);
            return;
        }

        if (_errors.Count >= _maxDistinctErrorCount)
            return;

        var error = new ErrorEntry();
        error.Message = exception.Message;
        error.Count = 1;

        _errors.Add(key, error);
    }
}
