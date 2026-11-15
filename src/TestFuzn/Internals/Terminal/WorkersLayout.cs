using System.Globalization;
using Fuzn.TestFuzn.Internals.Cluster;

namespace Fuzn.TestFuzn.Internals.Terminal;

// The load workers of a cluster run: first their start-up state, then their health while the load runs.
internal static class WorkersLayout
{
    internal const string Header = "Workers";

    private static readonly TableColumn[] Columns =
    {
        MetricsTableLayout.HeaderColumn("Worker"),
        MetricsTableLayout.HeaderColumn("State"),
        MetricsTableLayout.HeaderColumn("OS"),
        MetricsTableLayout.NumberColumn("Cores"),
        MetricsTableLayout.NumberColumn("CPU"),
        MetricsTableLayout.NumberColumn("Memory"),
        MetricsTableLayout.NumberColumn("Start lag"),
        MetricsTableLayout.NumberColumn("Iterations")
    };

    private static readonly int[][] ColumnTiers =
    {
        new[] { 0, 1, 2, 3, 4, 5, 6, 7 },
        new[] { 0, 1, 4, 5, 6, 7 },
        new[] { 0, 1, 4 }
    };

    public static IReadOnlyList<RenderedLine> Render(IReadOnlyList<WorkerSummary> workers, int width, ColorMode colorMode)
    {
        var rows = new List<IReadOnlyList<string?>>(workers.Count);
        foreach (var worker in workers)
        {
            var cpu = Percent(worker.CpuPercent);
            if (worker.CpuPercent > WorkerSummary.CpuWarningPercent)
                cpu = MetricsTableLayout.Styled(LiveDashboardLayout.WarningStyle, cpu + " !");

            var os = worker.Os;
            if (os == null)
                os = "";

            rows.Add(new[]
            {
                MarkupParser.Escape(worker.Id),
                MarkupParser.Escape(worker.State),
                MarkupParser.Escape(os),
                LiveDashboardLayout.FormatCount(worker.CpuCores),
                cpu,
                worker.MemoryMb.ToString("0", CultureInfo.InvariantCulture) + " MB",
                worker.IterationStartLagMs.ToString("0", CultureInfo.InvariantCulture) + " ms",
                LiveDashboardLayout.FormatCount(worker.Iterations)
            });
        }

        foreach (var tier in ColumnTiers)
        {
            var columns = tier.Select(index => Columns[index]).ToList();
            var tierRows = rows.Select(row => (IReadOnlyList<string?>) tier.Select(index => row[index]).ToList()).ToList();
            if (tier == ColumnTiers[^1] || TableWidget.MeasureNaturalWidth(columns, tierRows) <= width)
                return TableWidget.Render(columns, tierRows, width, colorMode);
        }

        return Array.Empty<RenderedLine>();
    }

    // One plain line for terminals without the live view.
    public static string Line(IReadOnlyList<WorkerSummary> workers)
    {
        return "[workers] " + string.Join("; ", workers.Select(worker =>
            $"{worker.Id} {worker.State} cpu {Percent(worker.CpuPercent)}{(worker.CpuPercent > WorkerSummary.CpuWarningPercent ? " (high)" : "")} " +
            $"memory {worker.MemoryMb.ToString("0", CultureInfo.InvariantCulture)} MB start lag {worker.IterationStartLagMs.ToString("0", CultureInfo.InvariantCulture)} ms"));
    }

    private static string Percent(double value) => value.ToString("0", CultureInfo.InvariantCulture) + "%";
}
