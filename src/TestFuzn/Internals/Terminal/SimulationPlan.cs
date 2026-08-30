using System.Globalization;
using Fuzn.TestFuzn.Internals.Execution.Producers.Simulations;

namespace Fuzn.TestFuzn.Internals.Terminal;

/// <summary>
/// The planned shape of a scenario's load run, computed once from the ordered typed simulation
/// configs (`Scenario.SimulationsInternal`, warmup simulations always first). Provides the
/// planned durations behind the dashboard's elapsed tile — its planned total, progress gauge and
/// time remaining — the entries the timeline lays out as its segments, and the current phase
/// label for a given elapsed time within a segment. Durations are the configured values —
/// actual wall time can run slightly longer (producer scheduling, warmup queue drain), which
/// callers absorb by clamping progress. A count-based simulation (OneTimeLoad, count-based
/// FixedConcurrentLoad) has no planned duration: any such entry makes the affected planned
/// totals null (indeterminate — no fake numbers), and once elapsed time reaches it the phase
/// label sticks there, because time alone cannot tell when it ends. Immutable after
/// construction and safe to read from any thread.
/// </summary>
internal sealed class SimulationPlan
{
    private readonly List<SimulationPlanEntry> _warmupEntries = new();
    private readonly List<SimulationPlanEntry> _measurementEntries = new();

    /// <summary>All simulations in producer order: the warmup segment followed by the measurement segment.</summary>
    public IReadOnlyList<SimulationPlanEntry> Entries { get; }

    /// <summary>Planned duration of the warmup segment; Zero when there are no warmup simulations, null when any is count-based.</summary>
    public TimeSpan? PlannedWarmupDuration { get; }

    /// <summary>Planned duration of the measurement segment; null when any of its simulations is count-based.</summary>
    public TimeSpan? PlannedMeasurementDuration { get; }

    /// <summary>Planned duration of the whole run (warmup + measurement); null when any simulation is count-based.</summary>
    public TimeSpan? PlannedDuration { get; }

    public SimulationPlan(IReadOnlyList<ILoadConfiguration> simulations)
    {
        if (simulations == null)
            throw new ArgumentNullException(nameof(simulations), "Simulations cannot be null.");

        var entries = new List<SimulationPlanEntry>();
        foreach (var simulation in simulations)
        {
            var entry = new SimulationPlanEntry(CompactLabel(simulation), PlannedDurationOf(simulation), simulation.IsWarmup);
            entries.Add(entry);
            if (entry.IsWarmup)
                _warmupEntries.Add(entry);
            else
                _measurementEntries.Add(entry);
        }

        Entries = entries;
        PlannedWarmupDuration = SumDurations(_warmupEntries);
        PlannedMeasurementDuration = SumDurations(_measurementEntries);
        if (PlannedWarmupDuration != null && PlannedMeasurementDuration != null)
            PlannedDuration = PlannedWarmupDuration.Value + PlannedMeasurementDuration.Value;
        else
            PlannedDuration = null;
    }

    /// <summary>
    /// The phase label while the warmup segment runs, for the given elapsed time since warmup
    /// started: "warmup: {label}" for a single warmup simulation, "warmup {i}/{n}: {label}"
    /// for several. Falls back to "warmup" if the segment is empty (cannot happen in practice —
    /// the warmup phase only starts when a warmup simulation exists).
    /// </summary>
    public string WarmupPhaseLabel(TimeSpan elapsedInWarmup)
    {
        if (_warmupEntries.Count == 0)
            return "warmup";

        var index = CurrentEntryIndex(_warmupEntries, elapsedInWarmup);
        if (_warmupEntries.Count == 1)
            return "warmup: " + _warmupEntries[index].Label;

        return "warmup " + (index + 1) + "/" + _warmupEntries.Count + ": " + _warmupEntries[index].Label;
    }

    /// <summary>
    /// The phase label while the measurement segment runs, for the given elapsed time since
    /// measurement started: the bare simulation label for a single simulation,
    /// "sim {i}/{n}: {label}" for several. Falls back to "measurement" if the segment is empty.
    /// </summary>
    public string MeasurementPhaseLabel(TimeSpan elapsedInMeasurement)
    {
        if (_measurementEntries.Count == 0)
            return "measurement";

        var index = CurrentEntryIndex(_measurementEntries, elapsedInMeasurement);
        if (_measurementEntries.Count == 1)
            return _measurementEntries[index].Label;

        return "sim " + (index + 1) + "/" + _measurementEntries.Count + ": " + _measurementEntries[index].Label;
    }

    /// <summary>
    /// The entry the given elapsed time falls in: a simulation is current while elapsed is
    /// below its cumulative end, so at an exact boundary the next simulation is current. A
    /// count-based entry becomes current once elapsed reaches its start and stays current
    /// (nothing after it can be inferred from time). Past the end of a fully duration-based
    /// segment the last entry stays current.
    /// </summary>
    private static int CurrentEntryIndex(List<SimulationPlanEntry> segment, TimeSpan elapsed)
    {
        var cumulativeEnd = TimeSpan.Zero;
        for (var index = 0; index < segment.Count; index++)
        {
            if (segment[index].Duration == null)
                return index;

            cumulativeEnd += segment[index].Duration.Value;
            if (elapsed < cumulativeEnd)
                return index;
        }

        return segment.Count - 1;
    }

    private static TimeSpan? SumDurations(List<SimulationPlanEntry> entries)
    {
        var total = TimeSpan.Zero;
        foreach (var entry in entries)
        {
            if (entry.Duration == null)
                return null;

            total += entry.Duration.Value;
        }

        return total;
    }

    /// <summary>
    /// A compact one-line label for a simulation, using the same simulation names the summary
    /// tables print (see the configs' GetDescription): "Fixed Load 100 rps" (or
    /// "Fixed Load 100 per 30s" for a non-second interval), "Gradual Load 10→100 rps",
    /// "Random Load 5-50 rps", "One Time Load 500 iterations", "Fixed Concurrent Load 10"
    /// (", total 500" when count-based), "Pause Load 30s". An unknown configuration type falls
    /// back to its full GetDescription and counts as indeterminate.
    /// </summary>
    private static string CompactLabel(ILoadConfiguration simulation)
    {
        switch (simulation)
        {
            case FixedLoadConfiguration fixedLoad:
                if (fixedLoad.Interval == TimeSpan.FromSeconds(1))
                    return "Fixed Load " + fixedLoad.Rate + " rps";
                return "Fixed Load " + fixedLoad.Rate + " per " + FormatDuration(fixedLoad.Interval);
            case GradualLoadIncreaseConfiguration gradual:
                return "Gradual Load " + gradual.StartRate + "→" + gradual.EndRate + " rps";
            case RandomLoadPerSecondConfiguration random:
                return "Random Load " + random.MinRate + "-" + random.MaxRate + " rps";
            case OneTimeLoadConfiguration oneTime:
                return "One Time Load " + oneTime.Count + " iterations";
            case FixedConcurrentLoadConfiguration fixedConcurrent:
                if (fixedConcurrent.TotalCount > 0)
                    return "Fixed Concurrent Load " + fixedConcurrent.FixedCount + ", total " + fixedConcurrent.TotalCount;
                return "Fixed Concurrent Load " + fixedConcurrent.FixedCount;
            case PauseLoadConfiguration pause:
                return "Pause Load " + FormatDuration(pause.Duration);
            default:
                return simulation.GetDescription();
        }
    }

    private static TimeSpan? PlannedDurationOf(ILoadConfiguration simulation)
    {
        switch (simulation)
        {
            case FixedLoadConfiguration fixedLoad:
                return fixedLoad.Duration;
            case GradualLoadIncreaseConfiguration gradual:
                return gradual.Duration;
            case RandomLoadPerSecondConfiguration random:
                return random.Duration;
            case OneTimeLoadConfiguration:
                return null;
            case FixedConcurrentLoadConfiguration fixedConcurrent:
                if (fixedConcurrent.TotalCount > 0)
                    return null;
                return fixedConcurrent.Duration;
            case PauseLoadConfiguration pause:
                return pause.Duration;
            default:
                return null;
        }
    }

    /// <summary>
    /// Formats a duration compactly for labels: whole h/m/s parts with zero parts omitted
    /// ("30s", "1m 30s", "2h", "1h 5m"), a sub-second value as fractional seconds ("0.5s"),
    /// and zero as "0s" (a negative duration too). Invariant culture, so labels are
    /// deterministic. Shared with the dashboard's elapsed tile, whose planned total and time
    /// remaining read in the same units as the plan's labels.
    /// </summary>
    internal static string FormatDuration(TimeSpan duration)
    {
        if (duration > TimeSpan.Zero && duration < TimeSpan.FromSeconds(1))
            return duration.TotalSeconds.ToString("0.##", CultureInfo.InvariantCulture) + "s";

        var totalSeconds = (long)Math.Round(duration.TotalSeconds);
        if (totalSeconds <= 0)
            return "0s";

        var hours = totalSeconds / 3600;
        var minutes = (totalSeconds % 3600) / 60;
        var seconds = totalSeconds % 60;

        var parts = new List<string>();
        if (hours > 0)
            parts.Add(hours + "h");
        if (minutes > 0)
            parts.Add(minutes + "m");
        if (seconds > 0)
            parts.Add(seconds + "s");

        return string.Join(" ", parts);
    }
}
