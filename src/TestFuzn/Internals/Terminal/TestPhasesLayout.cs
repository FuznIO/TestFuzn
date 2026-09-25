using System.Globalization;
using Fuzn.TestFuzn.Internals.Utils;

namespace Fuzn.TestFuzn.Internals.Terminal;

internal static class TestPhasesLayout
{
    internal const string Header = "Test Phases";
    internal const string InitPhase = "Init";
    internal const string WarmupPhase = "Warmup";
    internal const string ExecutionPhase = "Execution";
    internal const string CleanupPhase = "Cleanup";
    internal const string TotalPhase = "Total Test Run";
    internal const string UnsetValueText = "—";

    private const string TimeFormat = "HH:mm:ss";
    private const string SimulationIndent = "  ";

    private static readonly TableColumn[] Columns =
    {
        MetricsTableLayout.HeaderColumn("Phase"),
        MetricsTableLayout.HeaderColumn("Duration"),
        MetricsTableLayout.HeaderColumn("Started"),
        MetricsTableLayout.HeaderColumn("Ended")
    };

    public static IReadOnlyList<RenderedLine> Render(IReadOnlyList<IReadOnlyList<string?>> rows, int innerWidth, ColorMode colorMode,
        IReadOnlyList<SimulationInfo>? simulations = null)
    {
        if (rows == null)
            throw new ArgumentNullException(nameof(rows), "Rows cannot be null.");

        var tableLines = TableWidget.Render(Columns, rows, innerWidth, colorMode);
        if (simulations == null || simulations.Count == 0)
            return tableLines;

        var lines = new List<RenderedLine>(tableLines.Count + simulations.Count);
        for (var index = 0; index < tableLines.Count; index++)
        {
            lines.Add(tableLines[index]);
            if (index == 0)
                continue;

            var phase = rows[index - 1][0];
            foreach (var simulation in simulations)
            {
                if (PhaseOf(simulation) != phase)
                    continue;

                foreach (var wrapped in LoadSummaryLayout.WrapText(MarkupText.SanitizeControlCharacters(simulation.Description).TrimEnd(), innerWidth - SimulationIndent.Length))
                    lines.Add(MarkupText.RenderTruncated("[" + LiveDashboardLayout.SecondaryStyle + "]" + SimulationIndent + MarkupParser.Escape(wrapped) + "[/]", innerWidth, colorMode));
            }
        }

        return lines;
    }

    private static string PhaseOf(SimulationInfo simulation)
    {
        return simulation.IsWarmup ? WarmupPhase : ExecutionPhase;
    }

    public static int MeasureNaturalWidth(IReadOnlyList<IReadOnlyList<string?>> rows)
    {
        return TableWidget.MeasureNaturalWidth(Columns, rows);
    }

    public static List<IReadOnlyList<string?>> Rows(PhaseTimes times, bool hasWarmup, DateTime now)
    {
        var rows = new List<IReadOnlyList<string?>>();
        rows.Add(Row(InitPhase, times.InitStart, times.InitEnd, now));
        if (hasWarmup)
            rows.Add(Row(WarmupPhase, times.WarmupStart, times.WarmupEnd, now));

        rows.Add(Row(ExecutionPhase, times.MeasurementStart, times.MeasurementEnd, now));
        rows.Add(Row(CleanupPhase, times.CleanupStart, times.CleanupEnd, now));
        rows.Add(Row(TotalPhase, times.InitStart, times.CleanupEnd, now));
        return rows;
    }

    private static string?[] Row(string phase, DateTime start, DateTime end, DateTime now)
    {
        return new[]
        {
            phase,
            FormatDuration(start, end, now),
            FormatTime(start),
            FormatTime(end)
        };
    }

    private static string FormatDuration(DateTime start, DateTime end, DateTime now)
    {
        if (start == default)
            return UnsetValueText;

        var until = end != default ? end : now;
        var duration = until - start;
        if (duration < TimeSpan.Zero)
            duration = TimeSpan.Zero;

        return duration.ToTestFuznReadableString();
    }

    private static string FormatTime(DateTime value)
    {
        if (value == default)
            return UnsetValueText;

        return value.ToLocalTime().ToString(TimeFormat, CultureInfo.InvariantCulture);
    }

    internal readonly struct PhaseTimes
    {
        public DateTime InitStart { get; init; }
        public DateTime InitEnd { get; init; }
        public DateTime WarmupStart { get; init; }
        public DateTime WarmupEnd { get; init; }
        public DateTime MeasurementStart { get; init; }
        public DateTime MeasurementEnd { get; init; }
        public DateTime CleanupStart { get; init; }
        public DateTime CleanupEnd { get; init; }
    }
}
