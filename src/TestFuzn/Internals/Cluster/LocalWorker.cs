using Fuzn.TestFuzn.Contracts.Results.Load;
using Fuzn.TestFuzn.Internals.Cluster.Protocol;
using Fuzn.TestFuzn.Internals.State;

namespace Fuzn.TestFuzn.Internals.Cluster;

// The machine of a run without a cluster, shown like a cluster worker in the live view, the sinks and the reports.
internal class LocalWorker
{
    private readonly object _lock = new();
    private readonly HealthSampler _sampler = new();
    private double _cpuSum;
    private int _cpuSamples;
    private double _cpuMax;
    private double _memoryMax;
    private double _lagMax;

    public List<WorkerSummary> Sample(TestExecutionState state, ScenarioLoadResult scenario)
    {
        lock (_lock)
        {
            var health = _sampler.Sample(state);
            _cpuSum += health.CpuPercent;
            _cpuSamples++;
            _cpuMax = Math.Max(_cpuMax, health.CpuPercent);
            _memoryMax = Math.Max(_memoryMax, health.MemoryMb);
            _lagMax = Math.Max(_lagMax, health.IterationStartLagMs);

            long iterations = 0;
            long errors = 0;
            foreach (var collector in state.LoadCollectors.Values)
            {
                var result = collector.GetCurrentResult();
                iterations += result.Count;
                errors += result.Failed.Count;
            }

            return
            [
                new WorkerSummary
                {
                    Id = Environment.MachineName,
                    State = Phase(scenario),
                    Os = ClusterProtocol.OsFamily(),
                    CpuCores = Environment.ProcessorCount,
                    CpuPercent = health.CpuPercent,
                    MemoryMb = health.MemoryMb,
                    IterationStartLagMs = health.IterationStartLagMs,
                    Iterations = iterations,
                    Errors = errors,
                    CpuAveragePercent = Math.Round(_cpuSum / _cpuSamples, 1),
                    CpuMaxPercent = _cpuMax,
                    MemoryMaxMb = _memoryMax,
                    IterationStartLagMaxMs = _lagMax
                }
            ];
        }
    }

    private static string Phase(ScenarioLoadResult scenario)
    {
        if (scenario.IsCompleted)
            return ScenarioPhase.Completed;
        if (scenario.MeasurementStartTime != default)
            return ScenarioPhase.Measurement;
        if (scenario.WarmupStartTime != default)
            return ScenarioPhase.Warmup;
        return ScenarioPhase.Init;
    }
}
