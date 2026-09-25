using System.Text;
using Fuzn.TestFuzn.Internals.Utils;

namespace Fuzn.TestFuzn.Internals.Terminal;

internal sealed class LiveStatsWriter
{
    private const string NoDataText = "N/A";

    private const string FieldSeparator = "  ";

    private readonly ITerminalWriter _writer;
    private readonly string?[] _lastPhases;
    private readonly string?[] _lastStatusDetails;

    public LiveStatsWriter(ITerminalWriter writer, int scenarioCount)
    {
        if (writer == null)
            throw new ArgumentNullException(nameof(writer), "Writer cannot be null.");
        if (scenarioCount < 0)
            throw new ArgumentOutOfRangeException(nameof(scenarioCount), scenarioCount, "Scenario count cannot be negative.");

        _writer = writer;
        _lastPhases = new string?[scenarioCount];
        _lastStatusDetails = new string?[scenarioCount];
    }

    public void WriteSample(IReadOnlyList<LiveMetricsSnapshot> snapshots)
    {
        if (snapshots == null)
            throw new ArgumentNullException(nameof(snapshots), "Snapshots cannot be null.");
        if (snapshots.Count > _lastPhases.Length)
            throw new ArgumentException("More snapshots than the scenario count the log was created for.", nameof(snapshots));

        for (var index = 0; index < snapshots.Count; index++)
        {
            var snapshot = snapshots[index];

            var phase = LiveDashboardLayout.PhaseName(snapshot);
            if (phase != _lastPhases[index])
            {
                _lastPhases[index] = phase;
                WriteLine(FormatPhaseLine(snapshot));
            }

            if (snapshot.StatusDetail != null && snapshot.StatusDetail != _lastStatusDetails[index])
            {
                _lastStatusDetails[index] = snapshot.StatusDetail;
                WriteLine(FormatFailureLine(snapshot));
            }

            WriteLine(FormatStatsLine(snapshot));
        }
    }

    public void WriteFinal(IReadOnlyList<LiveMetricsSnapshot> snapshots, bool isStopped)
    {
        if (snapshots == null)
            throw new ArgumentNullException(nameof(snapshots), "Snapshots cannot be null.");

        foreach (var snapshot in snapshots)
            WriteLine(FormatFinalLine(snapshot, isStopped));
    }

    internal static string FormatPhaseLine(LiveMetricsSnapshot snapshot)
    {
        if (snapshot == null)
            throw new ArgumentNullException(nameof(snapshot), "Snapshot cannot be null.");

        return Prefix(snapshot) + "phase: " + LiveDashboardLayout.PhaseName(snapshot);
    }

    internal static string FormatFailureLine(LiveMetricsSnapshot snapshot)
    {
        if (snapshot == null)
            throw new ArgumentNullException(nameof(snapshot), "Snapshot cannot be null.");
        if (snapshot.StatusDetail == null)
            throw new ArgumentException("The snapshot carries no status detail to write a failure line from.", nameof(snapshot));

        return Prefix(snapshot) + "failed: " + snapshot.StatusDetail;
    }

    internal static string FormatStatsLine(LiveMetricsSnapshot snapshot)
    {
        if (snapshot == null)
            throw new ArgumentNullException(nameof(snapshot), "Snapshot cannot be null.");

        var line = new StringBuilder(Prefix(snapshot));
        line.Append("elapsed ").Append(LiveDashboardLayout.FormatClock(snapshot.Duration));
        AppendTotals(line, snapshot);
        line.Append(FieldSeparator).Append("requests/sec ").Append(CurrentRateText(snapshot));
        AppendResponseTimePercentile95(line, snapshot);
        return line.ToString();
    }

    internal static string FormatFinalLine(LiveMetricsSnapshot snapshot, bool isStopped)
    {
        if (snapshot == null)
            throw new ArgumentNullException(nameof(snapshot), "Snapshot cannot be null.");

        var line = new StringBuilder(Prefix(snapshot));
        line.Append(FinalStatus(snapshot, isStopped));
        line.Append(FieldSeparator).Append("elapsed ").Append(LiveDashboardLayout.FormatClock(snapshot.Duration));
        AppendTotals(line, snapshot);
        AppendResponseTimePercentile95(line, snapshot);
        if (snapshot.StatusDetail != null)
            line.Append(FieldSeparator).Append("reason: ").Append(snapshot.StatusDetail);

        return line.ToString();
    }

    private static string FinalStatus(LiveMetricsSnapshot snapshot, bool isStopped)
    {
        if (snapshot.Status == TestStatus.Failed)
            return "failed";
        if (snapshot.Status == TestStatus.Skipped)
            return "skipped";
        if (isStopped || !snapshot.IsCompleted)
            return "stopped";

        return "completed";
    }

    private static string Prefix(LiveMetricsSnapshot snapshot)
    {
        return "[" + snapshot.ScenarioName + "] ";
    }

    private static void AppendTotals(StringBuilder line, LiveMetricsSnapshot snapshot)
    {
        line.Append(FieldSeparator).Append("requests ").Append(LiveDashboardLayout.FormatCount((long)snapshot.RequestCountOk + snapshot.RequestCountFailed));
        line.Append(FieldSeparator).Append("successful ").Append(LiveDashboardLayout.FormatCount(snapshot.RequestCountOk));
        line.Append(FieldSeparator).Append("failed ").Append(LiveDashboardLayout.FormatCount(snapshot.RequestCountFailed));
    }

    private static void AppendResponseTimePercentile95(StringBuilder line, LiveMetricsSnapshot snapshot)
    {
        line.Append(FieldSeparator).Append("p95 ").Append(snapshot.Ok.ResponseTimePercentile95.ToTestFuznResponseTime());
    }

    private static string CurrentRateText(LiveMetricsSnapshot snapshot)
    {
        var series = snapshot.RequestsPerSecondSeries;
        if (series.Count == 0)
            return NoDataText;

        var rate = series[series.Count - 1];
        if (!double.IsFinite(rate))
            return NoDataText;

        return LiveDashboardLayout.FormatFiniteRate(rate);
    }

    private void WriteLine(string line)
    {
        _writer.Write(MarkupText.SanitizeControlCharacters(line) + Environment.NewLine);
    }
}
