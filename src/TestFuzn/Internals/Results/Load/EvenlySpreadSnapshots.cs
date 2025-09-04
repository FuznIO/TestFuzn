using Fuzn.TestFuzn.Internals.State;
using Fuzn.TestFuzn.Contracts.Results.Load;

namespace Fuzn.TestFuzn.Internals.Results.Load;

internal class EvenlySpreadSnapshots
{
    private readonly int _maxSnapshots;
    private readonly ScenarioLoadResult[] _snapshots;
    private readonly TimeSpan[] _offsets;
    private DateTime _startTime;
    private DateTime _endTime;
    private ScenarioLoadResult _first;
    private ScenarioLoadResult _last;

    public EvenlySpreadSnapshots(int maxSnapshots)
    {
        _maxSnapshots = maxSnapshots;
        _snapshots = new ScenarioLoadResult[_maxSnapshots - 2];
        _offsets  = new TimeSpan[_snapshots.Length];
    }

    public void AddSnapshot(ScenarioLoadResult snapshot)
    {
        if (_first == null)
        {
            _first = snapshot;
            _startTime = snapshot.Created;
            return;
        }

        _last = snapshot;
        _endTime = snapshot.Created;

        // Calculate time range between first and last
        var totalDuration = _endTime - _startTime;
        if (totalDuration.TotalSeconds <= 0)
            return;

        // Find which bucket this sample belongs to
        for (int i = 0; i < _snapshots.Length; i++)
        {
            // Defensive: avoid division by zero and negative durations
            if (totalDuration.Ticks <= 0 || _snapshots.Length == 0)
                break;

            long offsetTicks = (i + 1) * totalDuration.Ticks / (_snapshots.Length + 1);

            // Clamp negative offsets to zero
            if (offsetTicks < 0)
                offsetTicks = 0;

            // Clamp to max possible offset to avoid DateTime overflow
            long maxOffsetTicks = DateTime.MaxValue.Ticks - _startTime.Ticks;
            if (offsetTicks > maxOffsetTicks)
                offsetTicks = maxOffsetTicks;

            var offset = TimeSpan.FromTicks(offsetTicks);
            var bucketTimestamp = _startTime + offset;

            // Store first sample that hits this bucket
            if (_offsets[i] == default && snapshot.Created >= bucketTimestamp)
            {
                _snapshots[i] = snapshot;
                _offsets[i] = offset;
                break;
            }
        }
    }

    public IReadOnlyList<ScenarioLoadResult> GetSnapshots()
    {
        var result = new List<ScenarioLoadResult>(_maxSnapshots);
        if (_first != null)
            result.Add(_first);

        result.AddRange(_snapshots.Where(b => b != null));

        if (_last != null && !_last.Equals(_first))
            result.Add(_last);

        return result;
    }
}
