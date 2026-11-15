namespace Fuzn.TestFuzn.Internals.Cluster;

// One load worker as the live view, the report and the sinks show it.
internal class WorkerSummary
{
    public const double CpuWarningPercent = 80;

    public string Id { get; set; } = "";
    public string State { get; set; } = "";
    public string? Os { get; set; }
    public int CpuCores { get; set; }
    public string? Version { get; set; }
    public double CpuPercent { get; set; }
    public double MemoryMb { get; set; }
    public double IterationStartLagMs { get; set; }
    public long Iterations { get; set; }
    public long Errors { get; set; }
    public double CpuAveragePercent { get; set; }
    public double CpuMaxPercent { get; set; }
    public double MemoryMaxMb { get; set; }
    public double IterationStartLagMaxMs { get; set; }
    public string? LogFolder { get; set; }

    public bool HasCpuWarning => CpuMaxPercent > CpuWarningPercent;
}
