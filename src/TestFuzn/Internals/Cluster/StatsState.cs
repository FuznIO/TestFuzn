namespace Fuzn.TestFuzn.Internals.Cluster;

// Cumulative raw stats that one process sends so another can merge them. Field names are the wire format
// between a load child and its controller, which always come from the same build.

internal class StatsState
{
    public int Count { get; set; }
    public long TotalTicks { get; set; }
    public long MinTicks { get; set; }
    public long MaxTicks { get; set; }
    public string? Histogram { get; set; }
}

internal class ErrorState
{
    public string Message { get; set; } = "";
    public string Details { get; set; } = "";
    public int Count { get; set; }
}

internal class StepStatsState
{
    public string Name { get; set; } = "";
    public string? Id { get; set; }
    public StatsState Ok { get; set; } = new();
    public StatsState Failed { get; set; } = new();
    public int Skipped { get; set; }
    public List<ErrorState> Errors { get; set; } = new();
    public int OtherErrors { get; set; }
    public Dictionary<int, long> StatusCodes { get; set; } = new();
    public List<StepStatsState> Steps { get; set; } = new();
}

internal class SimulationState
{
    public string Description { get; set; } = "";
    public bool IsWarmup { get; set; }
}

internal class ScenarioStatsState
{
    public string Name { get; set; } = "";
    public string Phase { get; set; } = ScenarioPhase.Init;
    public List<SimulationState> Simulations { get; set; } = new();
    public long MeasurementDurationTicks { get; set; }
    public int WarmupOk { get; set; }
    public int WarmupFailed { get; set; }
    public StatsState Ok { get; set; } = new();
    public StatsState Failed { get; set; } = new();
    public List<StepStatsState> Steps { get; set; } = new();
    public List<StepStatsState> Hooks { get; set; } = new();
    public int? InputDataCount { get; set; }
    public string? InputDataHash { get; set; }
}

internal static class ScenarioPhase
{
    public const string Init = "Init";
    public const string Warmup = "Warmup";
    public const string Measurement = "Measurement";
    public const string Completed = "Completed";
}

internal class HealthState
{
    public double CpuPercent { get; set; }
    public double MemoryMb { get; set; }
    public double IterationStartLagMs { get; set; }
}

internal class LoadStatsState
{
    public HealthState Health { get; set; } = new();
    public List<ScenarioStatsState> Scenarios { get; set; } = new();
}

internal class ScenarioTimesState
{
    public DateTime InitStartTime { get; set; }
    public DateTime InitEndTime { get; set; }
    public DateTime WarmupStartTime { get; set; }
    public DateTime WarmupEndTime { get; set; }
    public DateTime MeasurementStartTime { get; set; }
    public DateTime MeasurementEndTime { get; set; }
    public DateTime CleanupStartTime { get; set; }
    public DateTime CleanupEndTime { get; set; }
    public bool IsCompleted { get; set; }
    public string Status { get; set; } = "";
    public string? AssertWhileWarmingUpError { get; set; }
    public string? AssertWhileRunningError { get; set; }
    public string? AssertWhenDoneError { get; set; }
}
