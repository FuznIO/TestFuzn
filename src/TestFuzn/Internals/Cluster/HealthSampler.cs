using System.Diagnostics;
using Fuzn.TestFuzn.Internals.State;

namespace Fuzn.TestFuzn.Internals.Cluster;

// CPU is the share of the whole machine this process used since the last sample, which works the same on
// every OS. An overloaded load machine makes durations look slow while the system under test is fine.
internal class HealthSampler
{
    private readonly Process _process = Process.GetCurrentProcess();
    private TimeSpan _lastCpuTime;
    private long _lastTimestamp;

    public HealthSampler()
    {
        _lastCpuTime = _process.TotalProcessorTime;
        _lastTimestamp = Stopwatch.GetTimestamp();
    }

    public HealthState Sample(TestExecutionState? state)
    {
        _process.Refresh();
        var cpuTime = _process.TotalProcessorTime;
        var timestamp = Stopwatch.GetTimestamp();
        var elapsed = Stopwatch.GetElapsedTime(_lastTimestamp, timestamp);

        var cpuPercent = 0.0;
        if (elapsed.TotalMilliseconds > 0)
            cpuPercent = (cpuTime - _lastCpuTime).TotalMilliseconds / (elapsed.TotalMilliseconds * Environment.ProcessorCount) * 100;

        _lastCpuTime = cpuTime;
        _lastTimestamp = timestamp;

        var health = new HealthState();
        health.CpuPercent = Math.Round(Math.Clamp(cpuPercent, 0, 100), 1);
        health.MemoryMb = Math.Round(_process.WorkingSet64 / (1024.0 * 1024.0), 1);
        if (state != null)
            health.IterationStartLagMs = Math.Round(state.TakeMaxIterationStartLag().TotalMilliseconds, 1);

        return health;
    }
}
