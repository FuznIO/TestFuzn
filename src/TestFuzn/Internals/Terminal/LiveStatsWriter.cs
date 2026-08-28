using System.Text;
using Fuzn.TestFuzn.Internals.Utils;

namespace Fuzn.TestFuzn.Internals.Terminal;

/// <summary>
/// The standalone runner's live view for a terminal that cannot show the dashboard — an output
/// redirected to a file or a pipe (docker logs, CI), a terminal without ANSI support, or an
/// ANSI terminal whose input is redirected (stdin from /dev/null, as several CI runners do),
/// where the dashboard's keys could not be polled: plain text lines and nothing else. No escape
/// sequence, cursor movement or carriage-return trick is ever emitted, and the writer's
/// dimensions are never read (a redirected output has none: Unix fabricates a size, a
/// console-less Windows process throws), so the output reads correctly wherever it lands. Every
/// line leads with its scenario name in brackets and is written in one call ending in a line
/// break, so it stands on its own in a log and never mixes with another scenario's line or an
/// interleaved logger's. Three kinds of line. On every sample, one stats line per scenario:
/// elapsed and, when the plan is determinate, the planned duration; the measurement request
/// totals (total, ok, failed); the current-interval rate — the newest sample's, the same number
/// the dashboard heads its rps sparkline with, N/A before the first interval has closed; and
/// the cumulative Ok p95, N/A before the first Ok request. The stats line is preceded by a
/// phase line whenever the scenario's phase label differs from the last one written for it
/// (init, each warmup and measurement simulation, cleanup), and by a failure line the first
/// time an assert failure's reason appears. Transitions are seen at the sampling cadence only:
/// a phase shorter than the sampling interval — a count-based simulation that finishes inside
/// one second, a sub-second cleanup — is never observed and gets no line. After the run's final
/// sample, one final line per scenario with the outcome in the writer's own vocabulary — failed
/// (with its reason), skipped, stopped (the run was stopped, or ended before the scenario's
/// measurement completed), else completed. That is deliberately not the dashboard's badge set
/// (Failed, Skipped, Passed, Running): a badge describes a scenario at a glance while the final
/// line reports how the run ended, so it says completed where the badge says Passed and has
/// stopped, which no badge has. The outcome is followed by the frozen run duration, the totals
/// and the cumulative p95 (the final sample's interval p95 is empty: the reports drained the
/// interval histogram first, which is also why no rate is on the final line).
/// Numbers are formatted by <see cref="LiveDashboardLayout"/>'s formatters, so the lines and
/// the dashboard agree. Text from outside — scenario names, assert messages — is sanitized as
/// the dashboard sanitizes it (every control character becomes a space), so a line can neither
/// break nor carry an escape sequence. One thread at a time: the sampling loop writes the
/// samples and the stop that joined it writes the final lines.
/// </summary>
internal sealed class LiveStatsWriter
{
    /// <summary>What a number that is not there yet reads as — the same text the response-time formatter uses for none.</summary>
    private const string NoDataText = "N/A";

    private const string FieldSeparator = "  ";

    private readonly ITerminalWriter _writer;
    private readonly string?[] _lastPhaseLabels;
    private readonly string?[] _lastStatusDetails;

    /// <param name="writer">The output to write the lines to; only ever written to, never measured.</param>
    /// <param name="scenarioCount">How many scenarios the samples carry — the transitions are tracked per scenario position.</param>
    public LiveStatsWriter(ITerminalWriter writer, int scenarioCount)
    {
        if (writer == null)
            throw new ArgumentNullException(nameof(writer), "Writer cannot be null.");
        if (scenarioCount < 0)
            throw new ArgumentOutOfRangeException(nameof(scenarioCount), scenarioCount, "Scenario count cannot be negative.");

        _writer = writer;
        _lastPhaseLabels = new string?[scenarioCount];
        _lastStatusDetails = new string?[scenarioCount];
    }

    /// <summary>
    /// Writes the lines for one sample, scenario after scenario in the snapshots' order: a phase
    /// line when the scenario's phase label differs from the last one written for it (always on
    /// its first sample; an empty label is written never), a failure line when its status detail
    /// has appeared or changed, then its stats line. The snapshots must come in the same order
    /// on every call — transitions are tracked by position — and there can be no more of them
    /// than the scenario count given at construction.
    /// </summary>
    public void WriteSample(IReadOnlyList<LiveMetricsSnapshot> snapshots)
    {
        if (snapshots == null)
            throw new ArgumentNullException(nameof(snapshots), "Snapshots cannot be null.");
        if (snapshots.Count > _lastPhaseLabels.Length)
            throw new ArgumentException("More snapshots than the scenario count the log was created for.", nameof(snapshots));

        for (var index = 0; index < snapshots.Count; index++)
        {
            var snapshot = snapshots[index];

            if (snapshot.PhaseLabel != _lastPhaseLabels[index])
            {
                _lastPhaseLabels[index] = snapshot.PhaseLabel;
                if (snapshot.PhaseLabel.Length > 0)
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

    /// <summary>
    /// Writes the final line for every scenario, in the snapshots' order, from the run's final
    /// sample. <paramref name="isStopped"/> is whether the run was stopped (Ctrl+C, the quit
    /// key, an assert that stops the run) — the run's status, which the snapshots do not carry.
    /// </summary>
    public void WriteFinal(IReadOnlyList<LiveMetricsSnapshot> snapshots, bool isStopped)
    {
        if (snapshots == null)
            throw new ArgumentNullException(nameof(snapshots), "Snapshots cannot be null.");

        foreach (var snapshot in snapshots)
            WriteLine(FormatFinalLine(snapshot, isStopped));
    }

    /// <summary>"[scenario] phase: {label}" — the scenario's current phase label.</summary>
    internal static string FormatPhaseLine(LiveMetricsSnapshot snapshot)
    {
        if (snapshot == null)
            throw new ArgumentNullException(nameof(snapshot), "Snapshot cannot be null.");

        return Prefix(snapshot) + "phase: " + snapshot.PhaseLabel;
    }

    /// <summary>"[scenario] failed: {reason}" — the assert failure's reason, from the snapshot's status detail.</summary>
    internal static string FormatFailureLine(LiveMetricsSnapshot snapshot)
    {
        if (snapshot == null)
            throw new ArgumentNullException(nameof(snapshot), "Snapshot cannot be null.");
        if (snapshot.StatusDetail == null)
            throw new ArgumentException("The snapshot carries no status detail to write a failure line from.", nameof(snapshot));

        return Prefix(snapshot) + "failed: " + snapshot.StatusDetail;
    }

    /// <summary>
    /// "[scenario] elapsed {hh:mm:ss}[ / {planned}]  total {n}  ok {n}  failed {n}  rps {rate}  p95 {ms}":
    /// the planned duration only when the plan is determinate, the rate from the newest sample
    /// (N/A before the first interval has closed, or for a rate that is not finite), the
    /// cumulative Ok p95 in the summary's response-time format (N/A before the first Ok request).
    /// </summary>
    internal static string FormatStatsLine(LiveMetricsSnapshot snapshot)
    {
        if (snapshot == null)
            throw new ArgumentNullException(nameof(snapshot), "Snapshot cannot be null.");

        var line = new StringBuilder(Prefix(snapshot));
        line.Append("elapsed ").Append(LiveDashboardLayout.FormatClock(snapshot.Duration));
        if (snapshot.PlannedDuration != null)
            line.Append(" / ").Append(LiveDashboardLayout.FormatClock(snapshot.PlannedDuration.Value));

        AppendTotals(line, snapshot);
        line.Append(FieldSeparator).Append("rps ").Append(CurrentRateText(snapshot));
        AppendResponseTimePercentile95(line, snapshot);
        return line.ToString();
    }

    /// <summary>
    /// "[scenario] {status}  elapsed {hh:mm:ss}  total {n}  ok {n}  failed {n}  p95 {ms}[  reason: {detail}]":
    /// the status is failed when the scenario's status is Failed, skipped when Skipped, stopped
    /// when the run was stopped or the scenario's measurement never completed (the run ended
    /// before it — the exception follows), else completed; the reason is the status detail,
    /// when there is one.
    /// </summary>
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
        line.Append(FieldSeparator).Append("total ").Append(LiveDashboardLayout.FormatCount((long)snapshot.RequestCountOk + snapshot.RequestCountFailed));
        line.Append(FieldSeparator).Append("ok ").Append(LiveDashboardLayout.FormatCount(snapshot.RequestCountOk));
        line.Append(FieldSeparator).Append("failed ").Append(LiveDashboardLayout.FormatCount(snapshot.RequestCountFailed));
    }

    private static void AppendResponseTimePercentile95(StringBuilder line, LiveMetricsSnapshot snapshot)
    {
        line.Append(FieldSeparator).Append("p95 ").Append(snapshot.Ok.ResponseTimePercentile95.ToTestFuznResponseTime());
    }

    // The newest sample's current-interval rate, as the dashboard shows it: no data before the
    // first interval has closed, and for a rate that is not finite — never a fake zero.
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

    // One write per line: the sanitized text — no control character survives, so the line
    // cannot break early or carry an escape — and the line break, together.
    private void WriteLine(string line)
    {
        _writer.Write(MarkupText.SanitizeControlCharacters(line) + Environment.NewLine);
    }
}
