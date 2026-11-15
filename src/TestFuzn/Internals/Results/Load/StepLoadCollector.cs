using Fuzn.TestFuzn.Contracts.Results.Standard;
using Fuzn.TestFuzn.Contracts.Results.Load;
using Fuzn.TestFuzn.Internals.Cluster;

namespace Fuzn.TestFuzn.Internals.Results.Load;

internal class StepLoadCollector
{
    private readonly object _lock = new object();
    private int _maxDistinctErrorCount = 10;
    private string _name;
    private string _id;
    private StatsCollector _ok = new();
    private StatsCollector _failed = new();
    private int _skippedCount;
    private Dictionary<string, ErrorEntry> _errors = new();
    private int _otherErrorCount;
    private Dictionary<int, long>? _statusCodes;
    private Dictionary<string, StepLoadCollector> _steps;

    internal string Id => _id;
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

            if (result.StatusCodes != null)
            {
                foreach (var code in result.StatusCodes)
                    AddStatusCode(code.Key, code.Value);
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

    internal StepLoadResult GetCurrentResult()
    {
        var result = new StepLoadResult();
        result.Name = _name;
        result.Id = _id;
        result.Ok = _ok.GetCurrentResult();
        result.Failed = _failed.GetCurrentResult();
        result.SkippedCount = _skippedCount;
        result.Steps = new List<StepLoadResult>();
        foreach (var step in _steps ?? [])
        {
            result.Steps.Add(step.Value.GetCurrentResult());
        }

        if (_errors.Count > 0)
        {
            result.Errors = new Dictionary<string, ErrorEntry>(_errors);
        }
        if (_statusCodes != null)
            result.StatusCodes = new Dictionary<int, long>(_statusCodes);

        if (_otherErrorCount > 0)
        {
            if (result.Errors == null)
                result.Errors = new Dictionary<string, ErrorEntry>();
            result.Errors[ErrorShape.OtherErrorsKey] = new ErrorEntry { Message = ErrorShape.OtherErrors, Details = ErrorShape.OtherErrorsDetails, Count = _otherErrorCount };
        }

        return result;
    }

    internal StepStatsState GetState()
    {
        lock (_lock)
        {
            var state = new StepStatsState();
            state.Name = _name;
            state.Id = _id;
            state.Ok = _ok.GetState();
            state.Failed = _failed.GetState();
            state.Skipped = _skippedCount;
            foreach (var error in _errors.Values)
                state.Errors.Add(new ErrorState { Message = error.Message, Details = error.Details, Count = error.Count });
            state.OtherErrors = _otherErrorCount;
            if (_statusCodes != null)
                state.StatusCodes = new Dictionary<int, long>(_statusCodes);
            if (_steps != null)
            {
                foreach (var step in _steps)
                    state.Steps.Add(step.Value.GetState());
            }

            return state;
        }
    }

    internal void Add(StepStatsState state)
    {
        lock (_lock)
        {
            _ok.Add(state.Ok);
            _failed.Add(state.Failed);
            _skippedCount += state.Skipped;

            _otherErrorCount += state.OtherErrors;
            if (state.StatusCodes != null)
            {
                foreach (var code in state.StatusCodes)
                    AddStatusCode(code.Key, code.Value);
            }
            foreach (var error in state.Errors)
            {
                if (_errors.TryGetValue(error.Message, out var existing))
                {
                    existing.Count += error.Count;
                    continue;
                }

                if (_errors.Count >= _maxDistinctErrorCount)
                {
                    _otherErrorCount += error.Count;
                    continue;
                }

                _errors.Add(error.Message, new ErrorEntry { Message = error.Message, Details = error.Details, Count = error.Count });
            }

            foreach (var innerState in state.Steps)
            {
                if (_steps == null)
                    _steps = new Dictionary<string, StepLoadCollector>();

                if (!_steps.TryGetValue(innerState.Name, out var innerStep))
                {
                    innerStep = new StepLoadCollector(innerState.Name, innerState.Id);
                    _steps.Add(innerState.Name, innerStep);
                }

                innerStep.Add(innerState);
            }
        }
    }

    internal void SetPerSecond(TimeSpan measurementDuration)
    {
        lock (_lock)
        {
            _ok.SetPerSecond(measurementDuration);
            _failed.SetPerSecond(measurementDuration);
            if (_steps != null)
            {
                foreach (var step in _steps)
                    step.Value.SetPerSecond(measurementDuration);
            }
        }
    }

    private void AddStatusCode(int statusCode, long count)
    {
        if (_statusCodes == null)
            _statusCodes = new Dictionary<int, long>();
        _statusCodes.TryGetValue(statusCode, out var existing);
        _statusCodes[statusCode] = existing + count;
    }

    private void StoreError(Exception exception)
    {
        if (exception == null)
            return;

        var key = ErrorShape.Of(exception.Message);

        if (_errors.TryGetValue(key, out var existing))
        {
            Interlocked.Increment(ref existing.Count);
            return;
        }

        if (_errors.Count >= _maxDistinctErrorCount)
        {
            _otherErrorCount++;
            return;
        }

        var error = new ErrorEntry();
        error.Message = key;
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
