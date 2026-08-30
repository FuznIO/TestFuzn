using Fuzn.TestFuzn.Internals.Terminal;

namespace Fuzn.TestFuzn.Tests.Terminal;

/// <summary>
/// The multi-scenario goldens of <see cref="LiveDashboardLayout"/>: two scenarios side by side
/// at 160 columns and stacked at 100, three abreast at 200, a fourth starting a second row of
/// columns, the 122 and 184 thresholds — the narrowest column at the charts' width — with the
/// leftover column going to the first column, the shape at the threshold itself, the logo on
/// the last column of the first row, the step selection in every column, and a fuzz over one
/// to four scenarios. Derived by hand as the main file's summary describes: a column is the
/// section at the column's width (so the tiles, the timeline, the stacked charts and the
/// tables follow the width rules pinned at those widths in the other files), never with a
/// heatmap, and a row of columns is each column's line padded to the column's width — one
/// column per character in these frames — joined by the two-column gap, a column shorter
/// than its neighbour padding with blank lines.
/// </summary>
public partial class LiveDashboardLayoutTests
{
    /// <summary>The two 79-column columns of two scenarios at 160 columns: (160 − 2) / 2 with nothing over.</summary>
    private static readonly int[] TwoColumnsAt160 = { 79, 79 };

    /// <summary>The two 60-column columns at the 122-column threshold: (122 − 2) / 2 with nothing over.</summary>
    private static readonly int[] TwoColumnsAt122 = { 60, 60 };

    /// <summary>The three columns of three scenarios at 200 columns: (200 − 4) / 3 is 65 with one over, which widens the first.</summary>
    private static readonly int[] ThreeColumnsAt200 = { 66, 65, 65 };

    /// <summary>The requests table's full spread for the rich snapshot, 72 columns: its header and its two rows.</summary>
    private const string RequestsHeaderRow = "        count  rps    min    mean    p50     p75     p95     p99     max";
    private const string OkRequestsRow = "ok      12480  141  12 ms   38 ms  35 ms   48 ms   72 ms   94 ms  312 ms";
    private const string FailedRequestsRow = "failed     32  1.0  88 ms  102 ms  99 ms  110 ms  140 ms  160 ms  201 ms";

    /// <summary>The requests table without min, p75 and p99 — the tier a panel narrower than 72 inner columns takes — 49 columns.</summary>
    private const string NarrowRequestsHeaderRow = "        count  rps    mean    p50     p95     max";
    private const string NarrowOkRequestsRow = "ok      12480  141   38 ms  35 ms   72 ms  312 ms";
    private const string NarrowFailedRequestsRow = "failed     32  1.0  102 ms  99 ms  140 ms  201 ms";

    /// <summary>The requests table of a scenario without a request — every count 0, no rate, every response time N/A — 54 columns in its full spread.</summary>
    private const string EmptyRequestsHeaderRow = "        count  rps  min  mean  p50  p75  p95  p99  max";
    private const string EmptyOkRequestsRow = "ok          0    —  N/A   N/A  N/A  N/A  N/A  N/A  N/A";
    private const string EmptyFailedRequestsRow = "failed      0    —  N/A   N/A  N/A  N/A  N/A  N/A  N/A";

    /// <summary>The rich snapshot's step rows without the trend column (59 columns) and without mean and failed as well (44).</summary>
    private const string StepsHeaderRowWithoutTrend = " step         count  rps   mean    p95  failed  fail%      ";
    private const string CheckoutStepRowWithoutTrend = " Checkout      6260  9.6  58 ms  96 ms      30  █░░░░ 0.5% ";
    private const string AddToCartStepRowWithoutTrend = " Add to cart   6252   71  18 ms  38 ms       2  █░░░░ <0.1%";
    private const string NarrowStepsHeaderRow = " step         count  rps    p95  fail%      ";
    private const string NarrowCheckoutStepRow = " Checkout      6260  9.6  96 ms  █░░░░ 0.5% ";
    private const string NarrowAddToCartStepRow = " Add to cart   6252   71  38 ms  █░░░░ <0.1%";

    /// <summary>The rich snapshot's ticker entries below 80 columns (no ages; 59 and 44 columns) and below 60 (no rates either; 50 and 35).</summary>
    private const string CheckoutErrorEntryWithoutAges = "30× (2.0/s)  Checkout · Connection refused (localhost:7058)";
    private const string AddToCartErrorEntryWithoutAges = " 2× (0.0/s)  Add to cart · Timeout after 30s";
    private const string BareCheckoutErrorEntry = "30× Checkout · Connection refused (localhost:7058)";
    private const string BareAddToCartErrorEntry = " 2× Add to cart · Timeout after 30s";

    /// <summary>The rich snapshot's requests chart body at a 79-column panel, six rows tall, and its latency chart's: see <see cref="Verify_two_scenarios_render_side_by_side_at_160x40"/>.</summary>
    private const string RichTitle = "Checkout flow  ● Running";
    private const string IndeterminateTitle = "Browse catalog  ● Running";
    private const string FailedTitle = "Checkout flow  ● Failed · completed";

    [Test]
    public async Task Verify_two_scenarios_render_side_by_side_at_160x40()
    {
        // Two 79-column columns. The rich column: no logo and no tile trends below 80 columns,
        // five boxes of 15 (inner 11) on one row — the warmup unit (17 columns), the planned
        // unit (13) and the elapsed gauge (13) all miss an 11-wide box, so every tile is two
        // lines. The timeline shares 79 columns 16 / 32 / 31 (15.8, 31.6, 31.6 — the two over
        // to the .8 and the first .6); 135 s is 75 s into the ramp, so the marker sits on
        // column 16 + floor(75 / 120 × 32) = 36, the arrow of "10→50" (the label is centred on
        // 21-42), and the warmup label, 17 in 16, goes to the legend. Below 100 columns the
        // charts stack, six rows tall at 40 rows. The requests body is 75 − 4 − 6 = 65 cells =
        // 130 columns with sample i at column 14.33 i; on the 0..141 scale a level is 6.13
        // requests: rows 3-5 are solid, row 2 is ⣶ in its first cell (columns 0 and 1 read 88
        // and 88.6, level 15) and full from column 2 (v ≥ 88.9); row 1 (levels 17-20) starts
        // at column 13 (v ≥ 95.0) as ⢀, climbs at 24 (v ≥ 101.2), 36 (≥ 107.3) and 47
        // (≥ 113.4) — ⣾ in cell 23 — and is full from cell 24; row 0 (21-24) starts at column
        // 57 (v ≥ 119.5), climbs at 68 (≥ 125.7), 83 (≥ 131.8) — ⣴ in cell 41 — and 101
        // (≥ 137.9) — ⣾ in cell 50 — and is full for the last 14 cells. The latency body is
        // 75 − 6 − 8 = 61 cells, solid over the median line's ⣀ bottom row. The 72-column
        // full spread fits the 75-column requests panel, so does the 73-column widest step
        // tier, and the 92-column Checkout entry loses its ages below 80 columns and fits on
        // one line at 59: 8 header rows, 16 chart rows, 6 + 5 + 4 table rows are the 39 above
        // the footer exactly, with no heatmap to drop. The indeterminate column: no data in
        // its tiles, a bar of 79 − 2 − 25 = 52 columns (44 for the warmup, its label centred
        // on 13-29, and the hatched eight) with the phase label after it, the one-time label
        // cut with an ellipsis at the bar's edge on the legend line, tick-only empty charts
        // with a 74-cell body, and the empty spread — 29 rows, padded with ten blank rows to
        // the rich column's 39.
        await Scenario()
            .Step("The frame is 40 rows: 39 joined rows of exactly 160 columns over the one footer", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { RichSnapshot(), IndeterminateSnapshot() }, DefaultView, 160, 40, ColorMode.None);

                Assert.HasCount(40, lines);
                for (var row = 0; row < 39; row++)
                    Assert.AreEqual(160, lines[row].Width, $"Row {row} is not a joined row");

                AssertLine("q quit", 6, lines[39]);
                AssertMaximumWidth(160, lines);

                foreach (var line in lines)
                {
                    Assert.DoesNotContain(HeatmapTitle, line.Text);
                    Assert.DoesNotContain("⚡", line.Text);
                }
            })
            .Step("The header blocks sit side by side: both titles on one row, five two-line tiles in each column, each timeline at its column's width", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { RichSnapshot(), IndeterminateSnapshot() }, DefaultView, 160, 40, ColorMode.None);

                AssertColumns(TwoColumnsAt160, lines[0], RichTitle, IndeterminateTitle);
                AssertColumns(TwoColumnsAt160, lines[1], Row(Top(15), Top(15), Top(15), Top(15), Top(15)), Row(Top(15), Top(15), Top(15), Top(15), Top(15)));
                var labels = Row(Box("rps" + Spaces(8)), Box("p95" + Spaces(8)), Box("errors" + Spaces(5)), Box("requests" + Spaces(3)), Box("elapsed" + Spaces(4)));
                AssertColumns(TwoColumnsAt160, lines[2], labels, labels);
                AssertColumns(TwoColumnsAt160, lines[3],
                    Row(Box("142" + Spaces(8)), Box("38 ms" + Spaces(6)), Box("0.7 %" + Spaces(6)), Box("12512" + Spaces(6)), Box("00:02:15" + Spaces(3))),
                    Row(Box("—" + Spaces(10)), Box("—" + Spaces(10)), Box("—" + Spaces(10)), Box("0" + Spaces(10)), Box("00:00:42" + Spaces(3))));
                AssertColumns(TwoColumnsAt160, lines[4], Row(Bottom(15), Bottom(15), Bottom(15), Bottom(15), Bottom(15)), Row(Bottom(15), Bottom(15), Bottom(15), Bottom(15), Bottom(15)));
                AssertColumns(TwoColumnsAt160, lines[5],
                    Glyphs('=', 16) + "####" + " Gradual Load 10▼50 rps " + "----" + "------" + " Fixed Load 50 rps " + "------",
                    Glyphs('-', 12) + " Fixed Load 50 rps " + Glyphs('-', 13) + Glyphs('▒', 8) + "  warmup: Fixed Load 50 rps");
                AssertColumns(TwoColumnsAt160, lines[6], "Fixed Load 20 rps" + Spaces(62), Spaces(44) + "One Tim…");
                AssertColumns(TwoColumnsAt160, lines[7], string.Empty, string.Empty);
            })
            .Step("The charts stack in each column: the rich ramp and bands at 79 columns beside the indeterminate column's tick-only empty charts", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { RichSnapshot(), IndeterminateSnapshot() }, DefaultView, 160, 40, ColorMode.None);

                AssertColumns(TwoColumnsAt160, lines[8], PanelTop(RequestsChartTitle, 79), PanelTop(RequestsChartTitle, 79));
                AssertColumns(TwoColumnsAt160, lines[9], Box("141┤" + Spaces(28) + "⢀" + Glyphs('⣀', 5) + Glyphs('⣤', 7) + "⣴" + Glyphs('⣶', 8) + "⣾" + Glyphs('⣿', 14) + " ▶ 141"), Box("┤" + Spaces(74)));
                AssertColumns(TwoColumnsAt160, lines[10], Box("   │" + Spaces(6) + "⢀" + Glyphs('⣀', 5) + Glyphs('⣤', 6) + Glyphs('⣶', 5) + "⣾" + Glyphs('⣿', 41) + Spaces(6)), Box("│" + Spaces(74)));
                AssertColumns(TwoColumnsAt160, lines[11], Box(" 71┤⣶" + Glyphs('⣿', 64) + Spaces(6)), Box("┤" + Spaces(74)));
                AssertColumns(TwoColumnsAt160, lines[12], Box("   │" + Glyphs('⣿', 65) + Spaces(6)), Box("│" + Spaces(74)));
                AssertColumns(TwoColumnsAt160, lines[13], Box("   │" + Glyphs('⣿', 65) + Spaces(6)), Box("│" + Spaces(74)));
                AssertColumns(TwoColumnsAt160, lines[14], Box("  0┤" + Glyphs('⣿', 65) + Spaces(6)), Box("┤" + Spaces(74)));
                AssertColumns(TwoColumnsAt160, lines[15], Bottom(79), Bottom(79));
                AssertColumns(TwoColumnsAt160, lines[16], PanelTop(LatencyChartTitle, 79), PanelTop(LatencyChartTitle, 79));
                AssertColumns(TwoColumnsAt160, lines[17], Box("95 ms┤" + Glyphs('⣿', 61) + " ▶ 95 ms"), Box("┤" + Spaces(74)));
                AssertColumns(TwoColumnsAt160, lines[18], Box("     │" + Glyphs('⣿', 61) + Spaces(8)), Box("│" + Spaces(74)));
                AssertColumns(TwoColumnsAt160, lines[19], Box("65 ms┤" + Glyphs('⣿', 61) + Spaces(8)), Box("┤" + Spaces(74)));
                AssertColumns(TwoColumnsAt160, lines[20], Box("     │" + Glyphs('⣿', 61) + Spaces(8)), Box("│" + Spaces(74)));
                AssertColumns(TwoColumnsAt160, lines[21], Box("     │" + Glyphs('⣿', 61) + Spaces(8)), Box("│" + Spaces(74)));
                AssertColumns(TwoColumnsAt160, lines[22], Box("35 ms┤" + Glyphs('⣀', 61) + Spaces(8)), Box("┤" + Spaces(74)));
                AssertColumns(TwoColumnsAt160, lines[23], Bottom(79), Bottom(79));
            })
            .Step("The tables follow: the rich column's full spread, widest step tier and age-less ticker beside the indeterminate column's empty spread, padded blank below it", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { RichSnapshot(), IndeterminateSnapshot() }, DefaultView, 160, 40, ColorMode.None);

                AssertColumns(TwoColumnsAt160, lines[24], PanelTop("Requests", 79), PanelTop("Requests", 79));
                AssertColumns(TwoColumnsAt160, lines[25], Box("warmup 1200 ok · 3 failed" + Spaces(50)), Box(EmptyRequestsHeaderRow + Spaces(21)));
                AssertColumns(TwoColumnsAt160, lines[26], Box(RequestsHeaderRow + Spaces(3)), Box(EmptyOkRequestsRow + Spaces(21)));
                AssertColumns(TwoColumnsAt160, lines[27], Box(OkRequestsRow + Spaces(3)), Box(EmptyFailedRequestsRow + Spaces(21)));
                AssertColumns(TwoColumnsAt160, lines[28], Box(FailedRequestsRow + Spaces(3)), Bottom(79));
                AssertColumns(TwoColumnsAt160, lines[29], Bottom(79), string.Empty);
                AssertColumns(TwoColumnsAt160, lines[30], PanelTop("Steps", 79), string.Empty);
                AssertColumns(TwoColumnsAt160, lines[31], Box(StepsHeaderRow + Spaces(2)), string.Empty);
                AssertColumns(TwoColumnsAt160, lines[32], Box(CheckoutStepRow + Spaces(2)), string.Empty);
                AssertColumns(TwoColumnsAt160, lines[33], Box(AddToCartStepRow + Spaces(2)), string.Empty);
                AssertColumns(TwoColumnsAt160, lines[34], Bottom(79), string.Empty);
                AssertColumns(TwoColumnsAt160, lines[35], PanelTop("Errors", 79), string.Empty);
                AssertColumns(TwoColumnsAt160, lines[36], Box(CheckoutErrorEntryWithoutAges + Spaces(16)), string.Empty);
                AssertColumns(TwoColumnsAt160, lines[37], Box(AddToCartErrorEntryWithoutAges + Spaces(31)), string.Empty);
                AssertColumns(TwoColumnsAt160, lines[38], Bottom(79), string.Empty);
            })
            .Step("TrueColor: each column keeps its own styling — bold names on the shared title row, the accent on both chart headers, the ok annotation, the dim empty axis and the red error count — in rows of exactly 160 columns", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { RichSnapshot(), IndeterminateSnapshot() }, DefaultView, 160, 40, ColorMode.TrueColor);

                Assert.HasCount(40, lines);
                for (var row = 0; row < 39; row++)
                    Assert.AreEqual(160, lines[row].Width, $"Row {row} is not a joined row");

                Assert.Contains(Sgr(Bold, "Checkout flow"), lines[0].Text);
                Assert.Contains(Sgr(Bold, "Browse catalog"), lines[0].Text);
                Assert.AreEqual(2, CountOccurrences(lines[8].Text, Sgr(BoldAccent, "requests")));
                Assert.Contains(Sgr(Green, " ▶ 141"), lines[9].Text);
                Assert.Contains(Sgr(Dim, "┤"), lines[9].Text);
                Assert.Contains(Sgr(Red, "30×"), lines[36].Text);
            })
            .Run();
    }

    [Test]
    public async Task Verify_two_scenarios_stack_at_100x40()
    {
        // Below 122 columns two scenarios stack as one scenario renders, each section fitted
        // into what the ones before it left: the rich section at 100×40 is the single-scenario
        // frame — five boxes of 20, 19, 19, 19, 19, the charts side by side in 49-column
        // panels, the heatmap body two rows short (41 rows against 39) — which takes every row
        // above the footer, so the separator, the second section and everything after fall to
        // the cut. Nothing here is a column: the heatmap is in, the logo too.
        await Scenario()
            .Step("At 100 columns the first scenario fills the frame with its heatmap and the second is cut below the footer", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { RichSnapshot(), IndeterminateSnapshot() }, DefaultView, 100, 40, ColorMode.None);

                Assert.HasCount(40, lines);
                AssertLine(RichTitle + Spaces(63) + "  ⚡ TestFuzn", 100, lines[0]);
                AssertLine(Row(Top(20), Top(19), Top(19), Top(19), Top(19)), 100, lines[1]);
                AssertLine(PanelTop(RequestsChartTitle, 49) + "  " + PanelTop(LatencyChartTitle, 49), 100, lines[8]);
                AssertLine(PanelTop(HeatmapTitle, 100), 100, lines[16]);
                AssertLine(Bottom(100), 100, lines[23]);
                AssertLine(PanelTop("Requests", 100), 100, lines[24]);
                AssertLine(PanelTop("Steps", 100), 100, lines[30]);
                AssertLine(PanelTop("Errors", 100), 100, lines[35]);
                AssertLine(Bottom(100), 100, lines[38]);
                AssertLine("q quit", 6, lines[39]);
                AssertMaximumWidth(100, lines);

                foreach (var line in lines)
                    Assert.DoesNotContain("Browse catalog", line.Text);
            })
            .Step("The stacked frame is byte-identical to the single scenario's", context =>
            {
                var stacked = LiveDashboardLayout.Render(new[] { RichSnapshot(), IndeterminateSnapshot() }, DefaultView, 100, 40, ColorMode.TrueColor);
                var alone = LiveDashboardLayout.Render(new[] { RichSnapshot() }, DefaultView, 100, 40, ColorMode.TrueColor);

                Assert.HasCount(alone.Count, stacked);
                for (var row = 0; row < alone.Count; row++)
                {
                    Assert.AreEqual(alone[row].Text, stacked[row].Text, $"Text mismatch at row {row}");
                    Assert.AreEqual(alone[row].Width, stacked[row].Width, $"Width mismatch at row {row}");
                }
            })
            .Run();
    }

    [Test]
    public async Task Verify_three_scenarios_render_as_three_columns_at_200x45()
    {
        // (200 − 4) / 3 is 65 with one column over, which widens the first: columns of 66, 65
        // and 65. At 66 the five tiles share one row (64 is the least) in boxes of 13, 13, 12,
        // 12, 12 (inner 9, 9, 8, 8, 8) — the eight-column clock and the requests label fit
        // their 8 exactly, no unit and no gauge does — and at 65 in boxes of 13, 12, 12, 12,
        // 12. The rich timeline shares 66 columns 13 / 27 / 26 (13.2, 26.4, 26.4 — the one
        // over to the first .4), the marker on column 13 + floor(75 / 120 × 27) = 29, the "0"
        // of "10" in the ramp's label (centred on 15-36), and the warmup label goes to the
        // legend. The charts stack at 66: the requests body is 62 − 4 − 6 = 52 cells = 104
        // columns with sample i at column 11.44 i, so on the 0..141 scale row 2 is ⣶ in its
        // first cell (columns 0 and 1 read 88 and 88.7) and full from column 2; row 1 starts
        // at column 11 (v ≥ 95.0) as ⢀, climbs at 19 (≥ 101.2) — ⣠ in cell 9 — at 29 (≥ 107.3)
        // — ⣴ in cell 14 — and at 38 (≥ 113.4), full from cell 19; row 0 starts at column 46
        // (v ≥ 119.5) with a whole ⣀ cell, climbs at 54 (≥ 125.7), 66 (≥ 131.8) and 80
        // (≥ 137.9), each on a cell's first column, and is full for the last 12 cells. The
        // latency body is 62 − 6 − 8 = 48 cells. The 62-column inner width takes the requests
        // spread without min, p75 and p99 (49 columns; the full 72 does not fit) and the step
        // tier without the trend (59; the widest 73 does not), and the ticker its age-less
        // entries. The rich column is 39 rows; the indeterminate column 29 — its bar 65 − 27 =
        // 38 columns, 30 for the warmup with the label centred on 6-22, the legend cut at 38 —
        // and the failed column 28: the phase after the badge (no plan, so no timeline), the
        // reason line under the tiles, no data anywhere. Five blank rows pad the 39 to the 44
        // above the footer. No column reaches 80, so no column carries the logo.
        await Scenario()
            .Step("The three header blocks share one row each: titles, tiles, the rich timeline beside the indeterminate bar and the failed reason", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { RichSnapshot(), IndeterminateSnapshot(), FailedSnapshot() }, DefaultView, 200, 45, ColorMode.None);

                Assert.HasCount(45, lines);
                AssertColumns(ThreeColumnsAt200, lines[0], RichTitle, IndeterminateTitle, FailedTitle);
                var narrowTop = Row(Top(13), Top(12), Top(12), Top(12), Top(12));
                AssertColumns(ThreeColumnsAt200, lines[1], Row(Top(13), Top(13), Top(12), Top(12), Top(12)), narrowTop, narrowTop);
                var narrowLabels = Row(Box("rps" + Spaces(6)), Box("p95" + Spaces(5)), Box("errors" + Spaces(2)), Box("requests"), Box("elapsed" + Spaces(1)));
                AssertColumns(ThreeColumnsAt200, lines[2], Row(Box("rps" + Spaces(6)), Box("p95" + Spaces(6)), Box("errors" + Spaces(2)), Box("requests"), Box("elapsed" + Spaces(1))), narrowLabels, narrowLabels);
                AssertColumns(ThreeColumnsAt200, lines[3],
                    Row(Box("142" + Spaces(6)), Box("38 ms" + Spaces(4)), Box("0.7 %" + Spaces(3)), Box("12512" + Spaces(3)), Box("00:02:15")),
                    Row(Box("—" + Spaces(8)), Box("—" + Spaces(7)), Box("—" + Spaces(7)), Box("0" + Spaces(7)), Box("00:00:42")),
                    Row(Box("—" + Spaces(8)), Box("—" + Spaces(7)), Box("—" + Spaces(7)), Box("0" + Spaces(7)), Box("00:05:00")));
                var narrowBottom = Row(Bottom(13), Bottom(12), Bottom(12), Bottom(12), Bottom(12));
                AssertColumns(ThreeColumnsAt200, lines[4], Row(Bottom(13), Bottom(13), Bottom(12), Bottom(12), Bottom(12)), narrowBottom, narrowBottom);
                AssertColumns(ThreeColumnsAt200, lines[5],
                    Glyphs('=', 13) + "#" + " Gradual Load 1▼→50 rps " + "--" + "---" + " Fixed Load 50 rps " + "----",
                    "-----" + " Fixed Load 50 rps " + "------" + Glyphs('▒', 8) + "  warmup: Fixed Load 50 rps",
                    "✗ Assert.IsLessThan failed. p95 too high: 240 ms");
                AssertColumns(ThreeColumnsAt200, lines[6], "Fixed Load 20 rps" + Spaces(49), Spaces(30) + "One Tim…", string.Empty);
                AssertColumns(ThreeColumnsAt200, lines[7], string.Empty, string.Empty, PanelTop(RequestsChartTitle, 65));
            })
            .Step("The stacked charts of the three columns: the rich ramp and bands at 66, the empty tick-only charts of the other two a row apart", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { RichSnapshot(), IndeterminateSnapshot(), FailedSnapshot() }, DefaultView, 200, 45, ColorMode.None);

                var tick = Box("┤" + Spaces(60));
                var line = Box("│" + Spaces(60));
                AssertColumns(ThreeColumnsAt200, lines[8], PanelTop(RequestsChartTitle, 66), PanelTop(RequestsChartTitle, 65), tick);
                AssertColumns(ThreeColumnsAt200, lines[9], Box("141┤" + Spaces(23) + Glyphs('⣀', 4) + Glyphs('⣤', 6) + Glyphs('⣶', 7) + Glyphs('⣿', 12) + " ▶ 141"), tick, line);
                AssertColumns(ThreeColumnsAt200, lines[10], Box("   │" + Spaces(5) + "⢀" + Glyphs('⣀', 3) + "⣠" + Glyphs('⣤', 4) + "⣴" + Glyphs('⣶', 4) + Glyphs('⣿', 33) + Spaces(6)), line, tick);
                AssertColumns(ThreeColumnsAt200, lines[11], Box(" 71┤⣶" + Glyphs('⣿', 51) + Spaces(6)), tick, line);
                AssertColumns(ThreeColumnsAt200, lines[12], Box("   │" + Glyphs('⣿', 52) + Spaces(6)), line, line);
                AssertColumns(ThreeColumnsAt200, lines[13], Box("   │" + Glyphs('⣿', 52) + Spaces(6)), line, tick);
                AssertColumns(ThreeColumnsAt200, lines[14], Box("  0┤" + Glyphs('⣿', 52) + Spaces(6)), tick, Bottom(65));
                AssertColumns(ThreeColumnsAt200, lines[15], Bottom(66), Bottom(65), PanelTop(LatencyChartTitle, 65));
                AssertColumns(ThreeColumnsAt200, lines[16], PanelTop(LatencyChartTitle, 66), PanelTop(LatencyChartTitle, 65), tick);
                AssertColumns(ThreeColumnsAt200, lines[17], Box("95 ms┤" + Glyphs('⣿', 48) + " ▶ 95 ms"), tick, line);
                AssertColumns(ThreeColumnsAt200, lines[18], Box("     │" + Glyphs('⣿', 48) + Spaces(8)), line, tick);
                AssertColumns(ThreeColumnsAt200, lines[19], Box("65 ms┤" + Glyphs('⣿', 48) + Spaces(8)), tick, line);
                AssertColumns(ThreeColumnsAt200, lines[20], Box("     │" + Glyphs('⣿', 48) + Spaces(8)), line, line);
                AssertColumns(ThreeColumnsAt200, lines[21], Box("     │" + Glyphs('⣿', 48) + Spaces(8)), line, tick);
                AssertColumns(ThreeColumnsAt200, lines[22], Box("35 ms┤" + Glyphs('⣀', 48) + Spaces(8)), tick, Bottom(65));
                AssertColumns(ThreeColumnsAt200, lines[23], Bottom(66), Bottom(65), PanelTop("Requests", 65));
            })
            .Step("The tables: the rich column's narrower spread, trend-less step tier and age-less ticker, the empty spreads beside them, then blank rows to the footer", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { RichSnapshot(), IndeterminateSnapshot(), FailedSnapshot() }, DefaultView, 200, 45, ColorMode.None);

                AssertColumns(ThreeColumnsAt200, lines[24], PanelTop("Requests", 66), PanelTop("Requests", 65), Box(EmptyRequestsHeaderRow + Spaces(7)));
                AssertColumns(ThreeColumnsAt200, lines[25], Box("warmup 1200 ok · 3 failed" + Spaces(37)), Box(EmptyRequestsHeaderRow + Spaces(7)), Box(EmptyOkRequestsRow + Spaces(7)));
                AssertColumns(ThreeColumnsAt200, lines[26], Box(NarrowRequestsHeaderRow + Spaces(13)), Box(EmptyOkRequestsRow + Spaces(7)), Box(EmptyFailedRequestsRow + Spaces(7)));
                AssertColumns(ThreeColumnsAt200, lines[27], Box(NarrowOkRequestsRow + Spaces(13)), Box(EmptyFailedRequestsRow + Spaces(7)), Bottom(65));
                AssertColumns(ThreeColumnsAt200, lines[28], Box(NarrowFailedRequestsRow + Spaces(13)), Bottom(65), string.Empty);
                AssertColumns(ThreeColumnsAt200, lines[29], Bottom(66), string.Empty, string.Empty);
                AssertColumns(ThreeColumnsAt200, lines[30], PanelTop("Steps", 66), string.Empty, string.Empty);
                AssertColumns(ThreeColumnsAt200, lines[31], Box(StepsHeaderRowWithoutTrend + Spaces(3)), string.Empty, string.Empty);
                AssertColumns(ThreeColumnsAt200, lines[32], Box(CheckoutStepRowWithoutTrend + Spaces(3)), string.Empty, string.Empty);
                AssertColumns(ThreeColumnsAt200, lines[33], Box(AddToCartStepRowWithoutTrend + Spaces(3)), string.Empty, string.Empty);
                AssertColumns(ThreeColumnsAt200, lines[34], Bottom(66), string.Empty, string.Empty);
                AssertColumns(ThreeColumnsAt200, lines[35], PanelTop("Errors", 66), string.Empty, string.Empty);
                AssertColumns(ThreeColumnsAt200, lines[36], Box(CheckoutErrorEntryWithoutAges + Spaces(3)), string.Empty, string.Empty);
                AssertColumns(ThreeColumnsAt200, lines[37], Box(AddToCartErrorEntryWithoutAges + Spaces(18)), string.Empty, string.Empty);
                AssertColumns(ThreeColumnsAt200, lines[38], Bottom(66), string.Empty, string.Empty);
                AssertLine(string.Empty, 0, lines[39]);
                AssertLine(string.Empty, 0, lines[43]);
                AssertLine("q quit", 6, lines[44]);
                AssertMaximumWidth(200, lines);

                foreach (var line in lines)
                {
                    Assert.DoesNotContain(HeatmapTitle, line.Text);
                    Assert.DoesNotContain("⚡", line.Text);
                }
            })
            .Run();
    }

    [Test]
    public async Task Verify_a_fourth_scenario_starts_a_second_row_of_columns()
    {
        // Three columns is the most: a fourth scenario starts a second row of columns under
        // the first, after a blank line, at the same column widths with the two columns it
        // leaves empty blank. At an unbounded height the first row is the 39 rows of the
        // three-column frame above (nothing there depends on the height past 36 rows, and a
        // column never shows the heatmap), so the fourth — the idle run, its phase after the
        // badge for want of a plan, an rps of 0.0 and no other reading, no timeline — starts
        // on row 40 in the first column: its title, four tile rows, a blank, the two stacked
        // empty-scale charts, the five-row spread and the footer, 68 lines in all.
        await Scenario()
            .Step("Four scenarios at 200 columns: three abreast, the fourth alone on a second row of columns after a blank line", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { RichSnapshot(), IndeterminateSnapshot(), FailedSnapshot(), IdleSnapshot() }, DefaultView, 200, 0, ColorMode.None);

                Assert.HasCount(68, lines);
                AssertColumns(ThreeColumnsAt200, lines[0], RichTitle, IndeterminateTitle, FailedTitle);
                AssertColumns(ThreeColumnsAt200, lines[38], Bottom(66), string.Empty, string.Empty);
                AssertLine(string.Empty, 0, lines[39]);
                AssertColumns(ThreeColumnsAt200, lines[40], "Idle run  ● Running · Fixed Load 100 rps", string.Empty, string.Empty);
                AssertColumns(ThreeColumnsAt200, lines[41], Row(Top(13), Top(13), Top(12), Top(12), Top(12)), string.Empty, string.Empty);
                AssertColumns(ThreeColumnsAt200, lines[43], Row(Box("0.0" + Spaces(6)), Box("—" + Spaces(8)), Box("—" + Spaces(7)), Box("0" + Spaces(7)), Box("00:00:03")), string.Empty, string.Empty);
                AssertColumns(ThreeColumnsAt200, lines[45], string.Empty, string.Empty, string.Empty);
                AssertColumns(ThreeColumnsAt200, lines[46], PanelTop(RequestsChartTitle, 66), string.Empty, string.Empty);
                AssertColumns(ThreeColumnsAt200, lines[54], PanelTop(LatencyChartTitle, 66), string.Empty, string.Empty);
                AssertColumns(ThreeColumnsAt200, lines[62], PanelTop("Requests", 66), string.Empty, string.Empty);
                AssertColumns(ThreeColumnsAt200, lines[66], Bottom(66), string.Empty, string.Empty);
                AssertLine("q quit", 6, lines[67]);
                AssertMaximumWidth(200, lines);
                Assert.DoesNotContain("Idle run", lines[0].Text);
            })
            .Step("A window of 45 rows leaves the second row of columns the four rows after the first row's 39 and the separator, cut at the footer", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { RichSnapshot(), IndeterminateSnapshot(), FailedSnapshot(), IdleSnapshot() }, DefaultView, 200, 45, ColorMode.None);

                Assert.HasCount(45, lines);
                AssertLine(string.Empty, 0, lines[39]);
                AssertColumns(ThreeColumnsAt200, lines[40], "Idle run  ● Running · Fixed Load 100 rps", string.Empty, string.Empty);
                AssertColumns(ThreeColumnsAt200, lines[43], Row(Box("0.0" + Spaces(6)), Box("—" + Spaces(8)), Box("—" + Spaces(7)), Box("0" + Spaces(7)), Box("00:00:03")), string.Empty, string.Empty);
                AssertLine("q quit", 6, lines[44]);
            })
            .Step("Even a window wide enough for four columns of 60 keeps three: at 250 columns three 82-column columns carry the logo on the last and the fourth scenario waits below", context =>
            {
                // (250 − 4) / 3 is 82 with nothing over; the last column of the first row —
                // the frame's top-right, with no neighbour to its right — keeps the logo from
                // 80, its title fitted to 82 − 11 − 2 = 69 columns before it, and the second
                // row of columns carries none.
                var lines = LiveDashboardLayout.Render(new[] { RichSnapshot(), IndeterminateSnapshot(), FailedSnapshot(), IdleSnapshot() }, DefaultView, 250, 0, ColorMode.None);

                AssertColumns(new[] { 82, 82, 82 }, lines[0], RichTitle, IndeterminateTitle, FailedTitle + Spaces(34) + "  ⚡ TestFuzn");
                Assert.DoesNotContain("Idle run", lines[0].Text);
                Assert.AreEqual(1, CountLinesContaining(lines, "⚡"));

                var idle = IndexOfLineStartingWith(lines, "Idle run  ● Running");
                Assert.IsGreaterThan(1, idle);
                AssertLine(string.Empty, 0, lines[idle - 1]);
                Assert.AreEqual(250, lines[idle].Width);
                AssertMaximumWidth(250, lines);
            })
            .Run();
    }

    [Test]
    public async Task Verify_two_columns_from_122_and_three_from_184()
    {
        await Scenario()
            .Step("At 121 columns two scenarios stack — the logo, the heatmap and the second title on a row of its own; at 122 they are two 60-column columns, each with its stacked charts, without a heatmap or a logo", context =>
            {
                // At 122 the rich column's 60 columns wrap its tiles (below 64) and keep the
                // charts (from 60), stacked (below 100) and six rows tall at an unbounded
                // height: title, four and five tile rows, a two-line timeline, a blank, two
                // eight-row chart panels, the six-row requests panel in the spread without
                // min, p75 and p99, the five-row step table in the tier without the trend,
                // mean and failed, and the five-row ticker — its rates back at 60, the
                // Checkout entry wrapping under them — 45 rows; the indeterminate column is
                // 33 (a 12-row header, its two empty charts, the five-row empty spread), so
                // the frame is 45 joined rows and the footer.
                var stacked = LiveDashboardLayout.Render(new[] { RichSnapshot(), IndeterminateSnapshot() }, DefaultView, 121, 0, ColorMode.None);
                AssertLine(RichTitle + Spaces(84) + "  ⚡ TestFuzn", 121, stacked[0]);
                Assert.IsGreaterThan(0, IndexOfPanel(stacked, HeatmapTitle));
                AssertLine(IndeterminateTitle, 25, stacked[IndexOfLineStartingWith(stacked, IndeterminateTitle)]);

                var columns = LiveDashboardLayout.Render(new[] { RichSnapshot(), IndeterminateSnapshot() }, DefaultView, 122, 0, ColorMode.None);
                Assert.HasCount(46, columns);
                AssertColumns(TwoColumnsAt122, columns[0], RichTitle, IndeterminateTitle);
                AssertColumns(TwoColumnsAt122, columns[12], string.Empty, PanelTop(RequestsChartTitle, 60));
                AssertColumns(TwoColumnsAt122, columns[13], PanelTop(RequestsChartTitle, 60), Box("┤" + Spaces(55)));
                AssertColumns(TwoColumnsAt122, columns[21], PanelTop(LatencyChartTitle, 60), Box("┤" + Spaces(55)));
                AssertColumns(TwoColumnsAt122, columns[29], PanelTop("Requests", 60), Box(EmptyRequestsHeaderRow + Spaces(2)));
                AssertColumns(TwoColumnsAt122, columns[35], PanelTop("Steps", 60), string.Empty);
                AssertColumns(TwoColumnsAt122, columns[44], Bottom(60), string.Empty);
                AssertLine("q quit", 6, columns[45]);
                for (var row = 0; row < 45; row++)
                    Assert.AreEqual(122, columns[row].Width, $"Row {row} is not a joined row");

                Assert.AreEqual(2, CountLinesContaining(columns, RequestsChartTitle));
                foreach (var line in columns)
                {
                    Assert.DoesNotContain("⚡", line.Text);
                    Assert.DoesNotContain(HeatmapTitle, line.Text);
                }
            })
            .Step("The column over goes to the first column: at 123 the first column is 61 and the second 60, each with its charts", context =>
            {
                // (123 − 2) / 2 is 60 with one over. At 61 the rich column's first tile row
                // shares 59 columns as boxes of 20, 20 and 19; at 60 the indeterminate
                // column's as 20, 19 and 19. Both keep their stacked charts: the rich
                // column's requests chart tops row 13 under its 13-row header, the
                // indeterminate column's row 12.
                var lines = LiveDashboardLayout.Render(new[] { RichSnapshot(), IndeterminateSnapshot() }, DefaultView, 123, 0, ColorMode.None);

                AssertColumns(new[] { 61, 60 }, lines[0], RichTitle, IndeterminateTitle);
                AssertColumns(new[] { 61, 60 }, lines[1], Row(Top(20), Top(20), Top(19)), Row(Top(20), Top(19), Top(19)));
                AssertColumns(new[] { 61, 60 }, lines[12], string.Empty, PanelTop(RequestsChartTitle, 60));
                AssertColumns(new[] { 61, 60 }, lines[13], PanelTop(RequestsChartTitle, 61), Box("┤" + Spaces(55)));
                Assert.AreEqual(2, CountLinesContaining(lines, RequestsChartTitle));
            })
            .Step("At 183 columns three scenarios are two columns of 91 and 90 with the third on a second row and the logo on the second column; at 184 they are three columns of 60, each with its charts and none with the logo", context =>
            {
                // (183 − 2) / 2 is 90 with one over: the first column is 91 and the second
                // 90 — wide enough for the logo, which rides the last column of the first
                // row, its title fitted to 90 − 13 = 77 columns before it. (184 − 4) / 3 is
                // 60 with nothing over, and no column reaches 80.
                var two = LiveDashboardLayout.Render(new[] { RichSnapshot(), IndeterminateSnapshot(), FailedSnapshot() }, DefaultView, 183, 0, ColorMode.None);
                AssertColumns(new[] { 91, 90 }, two[0], RichTitle, IndeterminateTitle + Spaces(52) + "  ⚡ TestFuzn");
                Assert.DoesNotContain("● Failed", two[0].Text);
                Assert.AreEqual(1, CountLinesContaining(two, "⚡"));
                var failed = IndexOfLineStartingWith(two, FailedTitle);
                AssertLine(string.Empty, 0, two[failed - 1]);
                Assert.AreEqual(183, two[failed].Width);

                var three = LiveDashboardLayout.Render(new[] { RichSnapshot(), IndeterminateSnapshot(), FailedSnapshot() }, DefaultView, 184, 0, ColorMode.None);
                AssertColumns(new[] { 60, 60, 60 }, three[0], RichTitle, IndeterminateTitle, FailedTitle);
                Assert.AreEqual(1, CountLinesContaining(three, FailedTitle));
                Assert.AreEqual(3, CountLinesContaining(three, RequestsChartTitle));
                Assert.AreEqual(0, CountLinesContaining(three, "⚡"));
                AssertMaximumWidth(184, three);
            })
            .Step("Two scenarios never take three columns, however wide the window", context =>
            {
                // (220 − 2) / 2 is 109: two columns wide enough for the logo (on the second,
                // the last), trends and side-by-side charts (53 and 54 columns), each a
                // section as a 109-column window would show it — the rich column's charts
                // top row 8, under a one-line timeline whose 22-column warmup segment holds
                // its label, the indeterminate column's row 9, under its two-line one.
                var lines = LiveDashboardLayout.Render(new[] { RichSnapshot(), IndeterminateSnapshot() }, DefaultView, 220, 0, ColorMode.None);

                AssertColumns(new[] { 109, 109 }, lines[0], RichTitle, IndeterminateTitle + Spaces(71) + "  ⚡ TestFuzn");
                Assert.StartsWith(PanelTop(RequestsChartTitle, 53) + "  " + PanelTop(LatencyChartTitle, 54) + "  ", lines[8].Text);
                Assert.EndsWith(PanelTop(RequestsChartTitle, 53) + "  " + PanelTop(LatencyChartTitle, 54), lines[9].Text);
                foreach (var line in lines)
                    Assert.DoesNotContain(HeatmapTitle, line.Text);
            })
            .Run();
    }

    [Test]
    public async Task Verify_two_scenarios_at_the_columns_threshold_at_122x30()
    {
        // Two 60-column columns, both exactly the 29 rows above the footer — the frame the
        // stacked layout could only show the first scenario of. The rich column's header is
        // the 60×20 golden's: rps, p95 and errors in boxes of 20, 19 and 19 (inner 16, 15, 15)
        // over requests and elapsed in boxes of 30 and 29 (inner 26 and 25) — wide enough for
        // both units and the elapsed gauge, 25 − 9 = 16 cells at 0.45: seven full cells and a
        // two-eighths cell, with "2m 45s" after it — and a timeline of 12 / 24 / 24 columns
        // with the marker on column 12 + floor(75 / 120 × 24) = 27, the "0" of "10" in the
        // ramp's label, which fits its 24 columns exactly, and the warmup label on the legend:
        // 13 rows. Its 56-column inner width takes the requests spread without min, p75 and
        // p99 (49, not the full 72), the step tier without trend, mean and failed (44; the 59
        // without the trend alone misses), and the ticker with its rates back from 60 but no
        // ages: the 59-column Checkout entry wraps at the last space that fits 56 and
        // "(localhost:7058)" goes under the step name, 13 columns in — five rows. With its
        // two stacked six-row charts the column would be 45 rows, 16 over: the chart bodies
        // compact for four, neither table can give a row (two rows cannot become one and a
        // more line), the latency chart goes for six and the requests chart for the last six
        // — so no chart shows and the column is 13 + 6 + 5 + 5 = 29 rows. The indeterminate
        // column: no data in its wrapped tiles and no gauge (12 rows to the blank), a bar of
        // 60 − 27 = 33 columns (25 for the warmup, its label centred on 3-21, and the hatched
        // eight) with the phase after it, the legend cut at the bar's edge, and the empty
        // spread — 12 + 16 + 5 = 33 rows, four over, so its chart bodies compact and both
        // empty charts stay, their axes ticked on rows 0, 1 and 3 of a four-row body:
        // 12 + 6 + 6 + 5 = 29 rows too.
        await Scenario()
            .Step("Both scenarios fill the rows above the footer side by side: the rich column's tables under its header beside the indeterminate column's compacted empty charts", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { RichSnapshot(), IndeterminateSnapshot() }, DefaultView, 122, 30, ColorMode.None);

                Assert.HasCount(30, lines);
                AssertColumns(TwoColumnsAt122, lines[0], RichTitle, IndeterminateTitle);
                AssertColumns(TwoColumnsAt122, lines[1], Row(Top(20), Top(19), Top(19)), Row(Top(20), Top(19), Top(19)));
                var labels = Row(Box("rps" + Spaces(13)), Box("p95" + Spaces(12)), Box("errors" + Spaces(9)));
                AssertColumns(TwoColumnsAt122, lines[2], labels, labels);
                AssertColumns(TwoColumnsAt122, lines[3], Row(Box("142" + Spaces(13)), Box("38 ms" + Spaces(10)), Box("0.7 %" + Spaces(10))), Row(Box("—" + Spaces(15)), Box("—" + Spaces(14)), Box("—" + Spaces(14))));
                AssertColumns(TwoColumnsAt122, lines[4], Row(Bottom(20), Bottom(19), Bottom(19)), Row(Bottom(20), Bottom(19), Bottom(19)));
                AssertColumns(TwoColumnsAt122, lines[5], Row(Top(30), Top(29)), Row(Top(30), Top(29)));
                var secondLabels = Row(Box("requests" + Spaces(18)), Box("elapsed" + Spaces(18)));
                AssertColumns(TwoColumnsAt122, lines[6], secondLabels, secondLabels);
                AssertColumns(TwoColumnsAt122, lines[7], Row(Box("12512 warmup 1203" + Spaces(9)), Box("00:02:15 / 5m" + Spaces(12))), Row(Box("0" + Spaces(25)), Box("00:00:42" + Spaces(17))));
                AssertColumns(TwoColumnsAt122, lines[8], Row(Box(Spaces(26)), Box("▕███████▎········▏ 2m 45s")), Row(Bottom(30), Bottom(29)));
                AssertColumns(TwoColumnsAt122, lines[9], Row(Bottom(30), Bottom(29)), "---" + " Fixed Load 50 rps " + "---" + Glyphs('▒', 8) + "  warmup: Fixed Load 50 rps");
                AssertColumns(TwoColumnsAt122, lines[10], Glyphs('=', 12) + " Gradual Load 1▼→50 rps " + "-- Fixed Load 50 rps ---", Spaces(25) + "One Tim…");
                AssertColumns(TwoColumnsAt122, lines[11], "Fixed Load 20 rps" + Spaces(43), string.Empty);
                var axisTick = Box("┤" + Spaces(55));
                var axisLine = Box("│" + Spaces(55));
                AssertColumns(TwoColumnsAt122, lines[12], string.Empty, PanelTop(RequestsChartTitle, 60));
                AssertColumns(TwoColumnsAt122, lines[13], PanelTop("Requests", 60), axisTick);
                AssertColumns(TwoColumnsAt122, lines[14], Box("warmup 1200 ok · 3 failed" + Spaces(31)), axisTick);
                AssertColumns(TwoColumnsAt122, lines[15], Box(NarrowRequestsHeaderRow + Spaces(7)), axisLine);
                AssertColumns(TwoColumnsAt122, lines[16], Box(NarrowOkRequestsRow + Spaces(7)), axisTick);
                AssertColumns(TwoColumnsAt122, lines[17], Box(NarrowFailedRequestsRow + Spaces(7)), Bottom(60));
                AssertColumns(TwoColumnsAt122, lines[18], Bottom(60), PanelTop(LatencyChartTitle, 60));
                AssertColumns(TwoColumnsAt122, lines[19], PanelTop("Steps", 60), axisTick);
                AssertColumns(TwoColumnsAt122, lines[20], Box(NarrowStepsHeaderRow + Spaces(12)), axisTick);
                AssertColumns(TwoColumnsAt122, lines[21], Box(NarrowCheckoutStepRow + Spaces(12)), axisLine);
                AssertColumns(TwoColumnsAt122, lines[22], Box(NarrowAddToCartStepRow + Spaces(12)), axisTick);
                AssertColumns(TwoColumnsAt122, lines[23], Bottom(60), Bottom(60));
                AssertColumns(TwoColumnsAt122, lines[24], PanelTop("Errors", 60), PanelTop("Requests", 60));
                AssertColumns(TwoColumnsAt122, lines[25], Box("30× (2.0/s)  Checkout · Connection refused" + Spaces(14)), Box(EmptyRequestsHeaderRow + Spaces(2)));
                AssertColumns(TwoColumnsAt122, lines[26], Box(Spaces(13) + "(localhost:7058)" + Spaces(27)), Box(EmptyOkRequestsRow + Spaces(2)));
                AssertColumns(TwoColumnsAt122, lines[27], Box(AddToCartErrorEntryWithoutAges + Spaces(12)), Box(EmptyFailedRequestsRow + Spaces(2)));
                AssertColumns(TwoColumnsAt122, lines[28], Bottom(60), Bottom(60));
                AssertLine("q quit", 6, lines[29]);
                AssertMaximumWidth(122, lines);
                Assert.AreEqual(1, CountLinesContaining(lines, RequestsChartTitle));
                Assert.AreEqual(1, CountLinesContaining(lines, LatencyChartTitle));

                foreach (var line in lines)
                {
                    Assert.DoesNotContain(HeatmapTitle, line.Text);
                    Assert.DoesNotContain("⚡", line.Text);
                }
            })
            .Step("Color mode None emits zero escape bytes across a column frame", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { RichSnapshot(), IndeterminateSnapshot() }, DefaultView, 122, 30, ColorMode.None);

                foreach (var line in lines)
                    Assert.DoesNotContain("\u001b", line.Text);
            })
            .Run();
    }

    [Test]
    public async Task Verify_the_step_selection_applies_in_every_column()
    {
        // The rich scenario twice at 160 columns: both columns are the 160×40 golden's rich
        // column, their Steps panels on rows 30-34 with Checkout, the most painful step, on
        // row 32. Index 0 marks that row in both columns with the pointer in place of the
        // blank pointer column and, in colour, paints each as its own reverse-video span over
        // the row padded to the column's 75 inner columns.
        await Scenario()
            .Step("Index 0 points at the most painful row of both columns' Steps tables and reverses each row whole", context =>
            {
                var viewState = new LiveDashboardViewState { SelectedStepIndex = 0 };
                var selectedRow = LiveDashboardLayout.Pointer + CheckoutStepRow.Substring(1);

                var plain = LiveDashboardLayout.Render(new[] { RichSnapshot(), RichSnapshot() }, viewState, 160, 40, ColorMode.None);
                AssertColumns(TwoColumnsAt160, plain[31], Box(StepsHeaderRow + Spaces(2)), Box(StepsHeaderRow + Spaces(2)));
                AssertColumns(TwoColumnsAt160, plain[32], Box(selectedRow + Spaces(2)), Box(selectedRow + Spaces(2)));
                AssertColumns(TwoColumnsAt160, plain[33], Box(AddToCartStepRow + Spaces(2)), Box(AddToCartStepRow + Spaces(2)));
                Assert.AreEqual(1, CountLinesContaining(plain, LiveDashboardLayout.Pointer));
                Assert.AreEqual(2, CountOccurrences(plain[32].Text, LiveDashboardLayout.Pointer));

                var styled = LiveDashboardLayout.Render(new[] { RichSnapshot(), RichSnapshot() }, viewState, 160, 40, ColorMode.TrueColor);
                var reversed = "│ " + Sgr(Reverse, selectedRow + Spaces(2)) + " │";
                AssertLine(reversed + Spaces(2) + reversed, 160, styled[32]);
                Assert.DoesNotContain(AnsiCodes.Reverse, styled[31].Text);
                Assert.DoesNotContain(AnsiCodes.Reverse, styled[33].Text);
            })
            .Run();
    }

    [Test]
    public async Task Verify_column_frames_at_every_size()
    {
        // One to four scenarios over every other width from 40 to 220 — the 121/123 and
        // 183/185 boundaries added — and every other height from 10 to 60: the full grid
        // would take four times the single-scenario sweep, and the rules change only at the
        // widths named.
        await Scenario()
            .Step("With one to four scenarios the frame is exactly the height, the footer owns the last row and no line exceeds the width", context =>
            {
                var sets = new[]
                {
                    new[] { RichSnapshot() },
                    new[] { RichSnapshot(), IndeterminateSnapshot() },
                    new[] { RichSnapshot(), ThresholdSnapshot(), IndeterminateSnapshot() },
                    new[] { RichSnapshot(), ThresholdSnapshot(), IndeterminateSnapshot(), FailedSnapshot() }
                };

                foreach (var snapshots in sets)
                {
                    foreach (var width in SampledWidths())
                    {
                        for (var height = 10; height <= 60; height += 2)
                        {
                            var lines = LiveDashboardLayout.Render(snapshots, DefaultView, width, height, ColorMode.TrueColor);

                            Assert.HasCount(height, lines, $"Row count for {snapshots.Length} scenarios at {width}×{height}");
                            Assert.AreEqual(6, lines[height - 1].Width, $"Footer for {snapshots.Length} scenarios at {width}×{height}");
                            AssertMaximumWidth(width, lines);
                        }
                    }
                }
            })
            .Step("Color mode None renders every line's text at exactly its declared width — a column per character, the logo's lightning two — with zero escape bytes", context =>
            {
                // A declared width is what the joins pad by, so a line whose text is wider
                // than it declares would push its neighbours past the frame: with no escape
                // to skip, the text's length is its width, but for the compact logo's
                // lightning, which the logo widget declares two columns wide.
                var sets = new[]
                {
                    new[] { RichSnapshot() },
                    new[] { RichSnapshot(), IndeterminateSnapshot() },
                    new[] { RichSnapshot(), ThresholdSnapshot(), IndeterminateSnapshot() },
                    new[] { RichSnapshot(), ThresholdSnapshot(), IndeterminateSnapshot(), FailedSnapshot() }
                };

                foreach (var snapshots in sets)
                {
                    foreach (var width in SampledWidths())
                    {
                        for (var height = 10; height <= 60; height += 2)
                        {
                            var lines = LiveDashboardLayout.Render(snapshots, DefaultView, width, height, ColorMode.None);
                            for (var row = 0; row < lines.Count; row++)
                            {
                                Assert.DoesNotContain("\u001b", lines[row].Text, $"Escape on row {row} for {snapshots.Length} scenarios at {width}×{height}");
                                Assert.AreEqual(lines[row].Width, lines[row].Text.Length + CountOccurrences(lines[row].Text, "⚡"), $"Declared width of row {row} for {snapshots.Length} scenarios at {width}×{height}");
                            }
                        }
                    }
                }
            })
            .Step("From 122 columns every content row of a multi-scenario frame is a joined row of exactly the width or a blank separator", context =>
            {
                var sets = new[]
                {
                    new[] { RichSnapshot(), IndeterminateSnapshot() },
                    new[] { RichSnapshot(), ThresholdSnapshot(), IndeterminateSnapshot() },
                    new[] { RichSnapshot(), ThresholdSnapshot(), IndeterminateSnapshot(), FailedSnapshot() }
                };

                foreach (var snapshots in sets)
                {
                    foreach (var width in SampledWidths())
                    {
                        if (width < LiveDashboardLayout.MinimumWidthForColumns)
                            continue;

                        var lines = LiveDashboardLayout.Render(snapshots, DefaultView, width, 0, ColorMode.TrueColor);
                        for (var row = 0; row < lines.Count - 1; row++)
                        {
                            if (lines[row].Width != 0)
                                Assert.AreEqual(width, lines[row].Width, $"Row {row} for {snapshots.Length} scenarios at {width} columns");
                        }
                    }
                }
            })
            .Step("Identical inputs render an identical column frame", context =>
            {
                var first = LiveDashboardLayout.Render(new[] { RichSnapshot(), ThresholdSnapshot(), IndeterminateSnapshot() }, DefaultView, 200, 45, ColorMode.TrueColor);
                var second = LiveDashboardLayout.Render(new[] { RichSnapshot(), ThresholdSnapshot(), IndeterminateSnapshot() }, DefaultView, 200, 45, ColorMode.TrueColor);

                Assert.HasCount(first.Count, second);
                for (var index = 0; index < first.Count; index++)
                {
                    Assert.AreEqual(first[index].Text, second[index].Text, $"Text mismatch at row {index}");
                    Assert.AreEqual(first[index].Width, second[index].Width, $"Width mismatch at row {index}");
                }
            })
            .Run();
    }

    /// <summary>Every other width from 40 to 220 and the column-threshold boundaries the fuzz must not skip, ascending.</summary>
    private static IEnumerable<int> SampledWidths()
    {
        var boundaries = new[] { 121, 123, 183, 185 };
        for (var width = 40; width <= 220; width++)
        {
            if (width % 2 == 0 || Array.IndexOf(boundaries, width) >= 0)
                yield return width;
        }
    }

    /// <summary>
    /// A row of a column frame: each column's text padded to its width by display columns —
    /// one per character in these frames but the compact logo's lightning, which the logo
    /// widget declares two wide — joined by the two-column gap.
    /// </summary>
    private static string Columns(int[] widths, params string[] texts)
    {
        var parts = new string[widths.Length];
        for (var index = 0; index < widths.Length; index++)
            parts[index] = texts[index] + Spaces(widths[index] - texts[index].Length - CountOccurrences(texts[index], "⚡"));

        return string.Join(Spaces(2), parts);
    }

    private static void AssertColumns(int[] widths, RenderedLine actualLine, params string[] texts)
    {
        var width = (widths.Length - 1) * 2;
        foreach (var columnWidth in widths)
            width += columnWidth;

        AssertLine(Columns(widths, texts), width, actualLine);
    }

    private static int IndexOfLineStartingWith(IReadOnlyList<RenderedLine> lines, string prefix)
    {
        for (var index = 0; index < lines.Count; index++)
        {
            if (lines[index].Text.StartsWith(prefix, StringComparison.Ordinal))
                return index;
        }

        Assert.Fail($"No line starts with {prefix}.");
        return -1;
    }

    private static int CountLinesContaining(IReadOnlyList<RenderedLine> lines, string text)
    {
        var count = 0;
        foreach (var line in lines)
        {
            if (line.Text.Contains(text, StringComparison.Ordinal))
                count++;
        }

        return count;
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var position = text.IndexOf(value, StringComparison.Ordinal);
        while (position >= 0)
        {
            count++;
            position = text.IndexOf(value, position + value.Length, StringComparison.Ordinal);
        }

        return count;
    }
}
