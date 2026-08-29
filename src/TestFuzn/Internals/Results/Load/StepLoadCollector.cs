using Fuzn.TestFuzn.Contracts.Results.Standard;
using Fuzn.TestFuzn.Contracts.Results.Load;

namespace Fuzn.TestFuzn.Internals.Results.Load;

/// <summary>
/// Per-step load statistics: cumulative Ok/Failed stats, skipped count, distinct errors, the
/// per-interval latency and the nested sub-step collectors. Thread safety: every mutation of
/// this state — recording, error storage and the interval close inside
/// <see cref="GetCurrentResult"/> — happens under <c>_lock</c>; a sub-step collector has its
/// own lock and is always entered while the parent's is held (parent → child, the same order
/// in <see cref="Record"/> and <see cref="GetCurrentResult"/>), and the scenario collector
/// serializes both entry points under its own lock above that (scenario → step → sub-step).
/// </summary>
internal class StepLoadCollector
{
    private readonly object _lock = new object();
    private int _maxDistinctErrorCount = 10;
    private string _name;
    private string _id;
    private StatsCollector _ok = new(trackIntervalLatency: true);
    private StatsCollector _failed = new();
    private IntervalLatency _intervalLatency = IntervalLatency.Empty;
    private int _skippedCount;
    private Dictionary<string, ErrorEntry> _errors = new();
    private Dictionary<string, StepLoadCollector> _steps;
    public StepLoadCollector(string name, string id)
    {
        _name = name;
        _id = id;
    }

    internal void Record(StepStandardResult result, DateTime startTime, DateTime endTime)
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

            if (result.StepResults != null && result.StepResults.Count > 0)
            {
                foreach (var innerResult in result.StepResults)
                {
                    if (_steps == null)
                        _steps = new Dictionary<string, StepLoadCollector>();

                    if (!_steps.TryGetValue(innerResult.Name, out var innerStep))
                    {
                        innerStep = new StepLoadCollector(innerResult.Name, innerResult.Id);
                        _steps.Add(innerResult.Name, innerStep);
                    }

                    innerStep.Record(innerResult, startTime, endTime);
                }
            }
        }
    }

    /// <summary>
    /// <paramref name="forceRefresh"/> closes this step's and its sub-steps' latency interval —
    /// the scenario collector passes its own force-refresh flag through, so a step's interval
    /// covers the same iterations as the scenario's. A plain call reuses the last closed
    /// interval, exactly as the scenario collector does.
    /// </summary>
    internal StepLoadResult GetCurrentResult(bool forceRefresh = false)
    {
        lock (_lock)
        {
            if (forceRefresh)
                _intervalLatency = _ok.GetIntervalLatency();

            var result = new StepLoadResult();
            result.Name = _name;
            result.Id = _id;
            result.Ok = _ok.GetCurrentResult();
            result.Failed = _failed.GetCurrentResult();
            result.IntervalLatency = _intervalLatency;
            result.SkippedCount = _skippedCount;
            result.Steps = new List<StepLoadResult>();
            if (_steps != null)
            {
                foreach (var step in _steps)
                {
                    result.Steps.Add(step.Value.GetCurrentResult(forceRefresh));
                }
            }

            if (_errors.Count > 0)
            {
                result.Errors = new Dictionary<string, ErrorEntry>(_errors);
            }

            return result;
        }
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
        error.Details = BuildExceptionDetails(exception);
        error.Count = 1;

        _errors.Add(key, error);
    }

    private string BuildExceptionDetails(Exception exception)
    {
        if (exception == null)
            return string.Empty;

        var details = new System.Text.StringBuilder();
        details.AppendLine(exception.Message);
        details.AppendLine(exception.StackTrace);
        if (exception.InnerException != null)
        {
            details.AppendLine("Inner Exception:");
            details.AppendLine(BuildExceptionDetails(exception.InnerException));
        }
        return details.ToString();
    }
}
