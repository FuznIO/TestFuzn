using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Fuzn.TestFuzn.Internals.Terminal;

namespace Fuzn.TestFuzn.Tests.Terminal;

/// <summary>
/// Golden lines for <see cref="TimelineWidget"/>, derived by hand: a determinate segment's
/// columns are its duration's share of the width (largest remainder), a hatched segment takes
/// eight, the marker sits on the current segment's column floor(share of it elapsed × its
/// columns), and a label sits centred inside its segment between two bare spaces when its
/// width plus two fits, else on the legend line under the segment. The three-phase plan below
/// splits 60 columns 12 / 18 / 30 and 120 columns 24 / 36 / 60 with no remainder, so every
/// frame can be checked by eye against the comments.
/// </summary>
[TestClass]
public class TimelineWidgetTests : Test
{
    private const string Dim = "2";
    private const string Bold = "1";
    private const string Yellow16 = "93";
    private const string BoldYellow16 = "1;93";

    private static readonly ColorMode[] AllColorModes = { ColorMode.None, ColorMode.Monochrome, ColorMode.Colors16, ColorMode.TrueColor };

    private const string WarmupLabel = "warmup 10 rps";
    private const string RampLabel = "ramp 10→80";
    private const string SteadyLabel = "steady 80 rps";
    private const string OneTimeLabel = "One Time Load 500 iterations";

    /// <summary>Warmup 20 s, ramp 30 s, steady 50 s: 100 s, so a fraction is the elapsed seconds over 100.</summary>
    private static TimelineSegment[] ThreePhasePlan()
    {
        return new[]
        {
            new TimelineSegment(WarmupLabel, TimeSpan.FromSeconds(20), isWarmup: true),
            new TimelineSegment(RampLabel, TimeSpan.FromSeconds(30), isWarmup: false),
            new TimelineSegment(SteadyLabel, TimeSpan.FromSeconds(50), isWarmup: false)
        };
    }

    /// <summary>Warmup 20 s, a count-based simulation, steady 50 s: 70 determinate seconds.</summary>
    private static TimelineSegment[] IndeterminatePlan()
    {
        return new[]
        {
            new TimelineSegment(WarmupLabel, TimeSpan.FromSeconds(20), isWarmup: true),
            new TimelineSegment(OneTimeLabel, null, isWarmup: false),
            new TimelineSegment(SteadyLabel, TimeSpan.FromSeconds(50), isWarmup: false)
        };
    }

    private static TimelineSegment Segment(string label, double? seconds, bool isWarmup = false)
    {
        TimeSpan? duration = null;
        if (seconds != null)
            duration = TimeSpan.FromSeconds(seconds.Value);

        return new TimelineSegment(label, duration, isWarmup);
    }

    [Test]
    public async Task Verify_three_phase_timeline_golden_frames_at_width_60()
    {
        // Columns 0-11 warmup, 12-29 ramp, 30-59 steady. Halfway (50 s) is the exact start of
        // steady, so the marker is column 30 and columns 0-29 are filled. "warmup 10 rps" (13)
        // needs 15 columns and has 12: legend. "ramp 10→80" (10) needs 12 of 18: three bar
        // columns, a space, the label on 16-25, a space, three bar columns. "steady 80 rps"
        // (13) needs 15 of 30: seven bar columns (the first is the marker), a space, the label
        // on 38-50, a space, eight bar columns.
        await Scenario()
            .Step("Color mode None draws = for the filled warmup, # for filled measurement, - beyond the marker, and the legend under the warmup segment", context =>
            {
                var lines = TimelineWidget.Render(ThreePhasePlan(), 0.5, 60, ColorMode.None);

                AssertFrame(new[]
                {
                    "============### ramp 10→80 ###▼------ steady 80 rps --------",
                    "warmup 10 rps" + Spaces(47)
                }, 60, lines);

                foreach (var line in lines)
                    Assert.DoesNotContain("\u001b", line.Text);
            })
            .Step("TrueColor colours every filled column with the gradient at that column, the marker bold in its column's colour, the rest dim", context =>
            {
                var lines = TimelineWidget.Render(ThreePhasePlan(), 0.5, 60, ColorMode.TrueColor);

                AssertFrame(new[]
                {
                    GradientRun(new string('▓', 12) + "███", 0, 60) + " " + GradientRun(RampLabel, 16, 60) + " " + GradientRun("███", 27, 60)
                        + Sgr(Bold + ";" + Gradient(30, 60), "▼") + Sgr(Dim, "██████") + " " + Sgr(Dim, SteadyLabel) + " " + Sgr(Dim, "████████"),
                    Sgr(Dim, WarmupLabel) + Spaces(47)
                }, 60, lines);

                // The gradient's deep-orange start on the first column; column 30 of 60 is
                // 30/59 along it: green 92 + 115 × 0.5085 = 150.5 → 150, blue 107 × 0.5085 = 54.4 → 54.
                Assert.StartsWith("\u001b[38;2;255;92;0m▓\u001b[0m", lines[0].Text);
                Assert.Contains("\u001b[1;38;2;255;150;54m▼\u001b[0m", lines[0].Text);
            })
            .Step("Colors16 keeps to the yellow accent: one run per stretch of filled columns, the label in it, the marker bold", context =>
            {
                AssertFrame(new[]
                {
                    Sgr(Yellow16, new string('▓', 12) + "███") + " " + Sgr(Yellow16, RampLabel) + " " + Sgr(Yellow16, "███")
                        + Sgr(BoldYellow16, "▼") + Sgr(Dim, "██████") + " " + Sgr(Dim, SteadyLabel) + " " + Sgr(Dim, "████████"),
                    Sgr(Dim, WarmupLabel) + Spaces(47)
                }, 60, TimelineWidget.Render(ThreePhasePlan(), 0.5, 60, ColorMode.Colors16));
            })
            .Step("Monochrome draws the ASCII fill plain, the beyond columns dim and the marker bold, with no color codes", context =>
            {
                var lines = TimelineWidget.Render(ThreePhasePlan(), 0.5, 60, ColorMode.Monochrome);

                AssertFrame(new[]
                {
                    "============### ramp 10→80 ###" + Sgr(Bold, "▼") + Sgr(Dim, "------") + " " + Sgr(Dim, SteadyLabel) + " " + Sgr(Dim, "--------"),
                    Sgr(Dim, WarmupLabel) + Spaces(47)
                }, 60, lines);

                foreach (var line in lines)
                    AssertNoColorParameters(line.Text);
            })
            .Run();
    }

    [Test]
    public async Task Verify_three_phase_timeline_golden_frame_at_width_120()
    {
        // Columns 0-23 warmup, 24-59 ramp, 60-119 steady; halfway is column 60. Every label
        // fits: warmup centred with four = on the left and five on the right (the spare column
        // goes right), ramp with twelve # each side, steady with twenty-two columns on the left
        // (the marker and twenty-one dashes) and twenty-three on the right — one line only.
        await Scenario()
            .Step("Color mode None fits every label inside its segment and renders no legend", context =>
            {
                var lines = TimelineWidget.Render(ThreePhasePlan(), 0.5, 120, ColorMode.None);

                AssertFrame(new[]
                {
                    "==== warmup 10 rps =====" + "############ ramp 10→80 ############" + "▼" + new string('-', 21) + " steady 80 rps " + new string('-', 23)
                }, 120, lines);
            })
            .Step("TrueColor lights the warmup label in the gradient and the steady label dim", context =>
            {
                var lines = TimelineWidget.Render(ThreePhasePlan(), 0.5, 120, ColorMode.TrueColor);

                AssertFrame(new[]
                {
                    GradientRun("▓▓▓▓", 0, 120) + " " + GradientRun(WarmupLabel, 5, 120) + " " + GradientRun("▓▓▓▓▓", 19, 120)
                        + GradientRun(new string('█', 12), 24, 120) + " " + GradientRun(RampLabel, 37, 120) + " " + GradientRun(new string('█', 12), 48, 120)
                        + Sgr(Bold + ";" + Gradient(60, 120), "▼") + Sgr(Dim, new string('█', 21)) + " " + Sgr(Dim, SteadyLabel) + " " + Sgr(Dim, new string('█', 23))
                }, 120, lines);
            })
            .Step("Colors16 renders the same frame in the accent", context =>
            {
                AssertFrame(new[]
                {
                    Sgr(Yellow16, "▓▓▓▓") + " " + Sgr(Yellow16, WarmupLabel) + " " + Sgr(Yellow16, "▓▓▓▓▓" + new string('█', 12)) + " " + Sgr(Yellow16, RampLabel) + " " + Sgr(Yellow16, new string('█', 12))
                        + Sgr(BoldYellow16, "▼") + Sgr(Dim, new string('█', 21)) + " " + Sgr(Dim, SteadyLabel) + " " + Sgr(Dim, new string('█', 23))
                }, 120, TimelineWidget.Render(ThreePhasePlan(), 0.5, 120, ColorMode.Colors16));
            })
            .Run();
    }

    [Test]
    public async Task Verify_position_endpoints_boundaries_and_null()
    {
        await Scenario()
            .Step("Position 0 puts the marker on the first column with nothing filled, and a negative fraction clamps to it", context =>
            {
                var expected = "▼" + new string('-', 14) + " ramp 10→80 " + new string('-', 10) + " steady 80 rps " + new string('-', 8);

                AssertFrame(new[] { expected, WarmupLabel + Spaces(47) }, 60, TimelineWidget.Render(ThreePhasePlan(), 0, 60, ColorMode.None));
                AssertLine(expected, 60, TimelineWidget.Render(ThreePhasePlan(), -1, 60, ColorMode.None)[0]);
                AssertLine(expected, 60, TimelineWidget.Render(ThreePhasePlan(), -0.0, 60, ColorMode.None)[0]);
            })
            .Step("Position 1 puts the marker on the last column with everything before it filled, and a fraction above 1 clamps to it", context =>
            {
                var expected = "============### ramp 10→80 ##########" + " steady 80 rps " + "#######▼";

                AssertLine(expected, 60, TimelineWidget.Render(ThreePhasePlan(), 1, 60, ColorMode.None)[0]);
                AssertLine(expected, 60, TimelineWidget.Render(ThreePhasePlan(), 2, 60, ColorMode.None)[0]);
            })
            .Step("A null, NaN or infinite position draws no marker and fills nothing", context =>
            {
                var expected = new string('-', 15) + " ramp 10→80 " + new string('-', 10) + " steady 80 rps " + new string('-', 8);

                AssertLine(expected, 60, TimelineWidget.Render(ThreePhasePlan(), null, 60, ColorMode.None)[0]);
                AssertLine(expected, 60, TimelineWidget.Render(ThreePhasePlan(), double.NaN, 60, ColorMode.None)[0]);
                AssertLine(expected, 60, TimelineWidget.Render(ThreePhasePlan(), double.PositiveInfinity, 60, ColorMode.None)[0]);
                AssertLine(expected, 60, TimelineWidget.Render(ThreePhasePlan(), double.NegativeInfinity, 60, ColorMode.None)[0]);

                // Without a position every determinate column is beyond: one dim run per stretch.
                AssertLine(Sgr(Dim, new string('▓', 12) + "███") + " " + Sgr(Dim, RampLabel) + " " + Sgr(Dim, new string('█', 10)) + " " + Sgr(Dim, SteadyLabel) + " " + Sgr(Dim, "████████"), 60, TimelineWidget.Render(ThreePhasePlan(), null, 60, ColorMode.TrueColor)[0]);
            })
            .Step("Halfway through the ramp the marker lands on a label character and replaces it, the label lit up to the marker", context =>
            {
                // 35 s: 15 of the ramp's 30 s is half its 18 columns, column 12 + 9 = 21 — the
                // "1" of "ramp 10→80", which sits on columns 16-25.
                AssertLine("============### ramp ▼0→80 " + new string('-', 10) + " steady 80 rps " + new string('-', 8), 60, TimelineWidget.Render(ThreePhasePlan(), 0.35, 60, ColorMode.None)[0]);
                AssertLine(Sgr(Yellow16, new string('▓', 12) + "███") + " " + Sgr(Yellow16, "ramp ") + Sgr(BoldYellow16, "▼") + Sgr(Dim, "0→80") + " " + Sgr(Dim, new string('█', 10)) + " " + Sgr(Dim, SteadyLabel) + " " + Sgr(Dim, "████████"), 60, TimelineWidget.Render(ThreePhasePlan(), 0.35, 60, ColorMode.Colors16)[0]);
            })
            .Step("At an exact segment boundary the next segment is current, and just before it the last column of the previous one", context =>
            {
                AssertLine("============▼-- ramp 10→80 " + new string('-', 10) + " steady 80 rps " + new string('-', 8), 60, TimelineWidget.Render(ThreePhasePlan(), 0.2, 60, ColorMode.None)[0]);
                AssertLine("===========▼--- ramp 10→80 " + new string('-', 10) + " steady 80 rps " + new string('-', 8), 60, TimelineWidget.Render(ThreePhasePlan(), 0.199, 60, ColorMode.None)[0]);
            })
            .Run();
    }

    [Test]
    public async Task Verify_indeterminate_plan_golden_frames()
    {
        // The hatched segment takes eight columns; the 52 left split 20 : 50 as 14.86 and
        // 37.14 — 14 and 37 plus the one left over to the larger remainder: 15 / 8 / 37, on
        // columns 0-14, 15-22 and 23-59. "warmup 10 rps" needs exactly 15: the label between
        // two spaces and no bar column at all. "One Time Load 500 iterations" (28) never fits
        // eight: legend, under column 15. "steady 80 rps" needs 15 of 37: eleven bar columns
        // each side, the label on 35-47 between spaces on 34 and 48.
        await Scenario()
            .Step("Halfway through the 70 determinate seconds is 15 s into steady: 0.3 of its 37 columns is column 23 + 11 = 34, the label's leading space, which the marker takes", context =>
            {
                var lines = TimelineWidget.Render(IndeterminatePlan(), 0.5, 60, ColorMode.None);

                AssertFrame(new[]
                {
                    " warmup 10 rps ▒▒▒▒▒▒▒▒###########▼steady 80 rps -----------",
                    Spaces(15) + OneTimeLabel + Spaces(17)
                }, 60, lines);
            })
            .Step("TrueColor keeps the hatched segment dim between the gradient-lit warmup label and the filled steady columns", context =>
            {
                var lines = TimelineWidget.Render(IndeterminatePlan(), 0.5, 60, ColorMode.TrueColor);

                AssertFrame(new[]
                {
                    " " + GradientRun(WarmupLabel, 1, 60) + " " + Sgr(Dim, "▒▒▒▒▒▒▒▒") + GradientRun(new string('█', 11), 23, 60)
                        + Sgr(Bold + ";" + Gradient(34, 60), "▼") + Sgr(Dim, SteadyLabel) + " " + Sgr(Dim, new string('█', 11)),
                    Spaces(15) + Sgr(Dim, OneTimeLabel) + Spaces(17)
                }, 60, lines);
            })
            .Step("Colors16 renders the same frame in the accent", context =>
            {
                AssertFrame(new[]
                {
                    " " + Sgr(Yellow16, WarmupLabel) + " " + Sgr(Dim, "▒▒▒▒▒▒▒▒") + Sgr(Yellow16, new string('█', 11)) + Sgr(BoldYellow16, "▼") + Sgr(Dim, SteadyLabel) + " " + Sgr(Dim, new string('█', 11)),
                    Spaces(15) + Sgr(Dim, OneTimeLabel) + Spaces(17)
                }, 60, TimelineWidget.Render(IndeterminatePlan(), 0.5, 60, ColorMode.Colors16));
            })
            .Step("The marker skips the hatched segment: position 0 is the warmup's first column, position 1 the last steady column, null none", context =>
            {
                AssertLine("▼warmup 10 rps ▒▒▒▒▒▒▒▒----------- steady 80 rps -----------", 60, TimelineWidget.Render(IndeterminatePlan(), 0, 60, ColorMode.None)[0]);
                AssertLine(" warmup 10 rps ▒▒▒▒▒▒▒▒########### steady 80 rps ##########▼", 60, TimelineWidget.Render(IndeterminatePlan(), 1, 60, ColorMode.None)[0]);
                AssertLine(" warmup 10 rps ▒▒▒▒▒▒▒▒----------- steady 80 rps -----------", 60, TimelineWidget.Render(IndeterminatePlan(), null, 60, ColorMode.None)[0]);
            })
            .Step("A plan with no determinate segment is one hatched bar, its label inside, and never a marker", context =>
            {
                var oneTime = new[] { new TimelineSegment(OneTimeLabel, null, isWarmup: false) };
                var hatch = new string('▒', 15);

                AssertFrame(new[] { hatch + " " + OneTimeLabel + " " + hatch }, 60, TimelineWidget.Render(oneTime, 0.5, 60, ColorMode.None));
                AssertFrame(new[] { Sgr(Dim, hatch) + " " + Sgr(Dim, OneTimeLabel) + " " + Sgr(Dim, hatch) }, 60, TimelineWidget.Render(oneTime, 1, 60, ColorMode.TrueColor));
                AssertFrame(new[] { Sgr(Dim, hatch) + " " + Sgr(Dim, OneTimeLabel) + " " + Sgr(Dim, hatch) }, 60, TimelineWidget.Render(oneTime, null, 60, ColorMode.Colors16));

                // Two of them share the width equally, and an indeterminate warmup is hatched too.
                var two = new[] { new TimelineSegment(string.Empty, null, isWarmup: true), new TimelineSegment(string.Empty, null, isWarmup: false) };
                AssertFrame(new[] { new string('▒', 10) }, 10, TimelineWidget.Render(two, 0.5, 10, ColorMode.None));
            })
            .Run();
    }

    [Test]
    public async Task Verify_column_allocation_rules()
    {
        await Scenario()
            .Step("Durations share the width by the largest remainder, an earlier segment first on a tie", context =>
            {
                // 10 / 30 / 60 s over 10 columns is exactly 1 / 3 / 6; three equal segments over
                // 10 are 3.33 each, the column left over going to the first.
                var proportional = new[] { Segment("", 10, isWarmup: true), Segment("", 30), Segment("", 60, isWarmup: true) };
                AssertFrame(new[] { "=###=====▼" }, 10, TimelineWidget.Render(proportional, 1, 10, ColorMode.None));

                var thirds = new[] { Segment("", 1, isWarmup: true), Segment("", 1), Segment("", 1, isWarmup: true) };
                AssertFrame(new[] { "====###==▼" }, 10, TimelineWidget.Render(thirds, 1, 10, ColorMode.None));
            })
            .Step("A zero or negative duration still keeps one column, taken from the widest segment, and is never current", context =>
            {
                AssertFrame(new[] { "=########▼" }, 10, TimelineWidget.Render(new[] { Segment("", 0, isWarmup: true), Segment("", 10) }, 1, 10, ColorMode.None));
                AssertFrame(new[] { "=########▼" }, 10, TimelineWidget.Render(new[] { Segment("", -5, isWarmup: true), Segment("", 10) }, 1, 10, ColorMode.None));

                // At position 0 the marker skips the zero-duration segment, whose column is
                // then before the marker: filled.
                AssertFrame(new[] { "=▼--------" }, 10, TimelineWidget.Render(new[] { Segment("", 0, isWarmup: true), Segment("", 10) }, 0, 10, ColorMode.None));
            })
            .Step("All-zero durations share equally, and with no determinate duration there is no marker", context =>
            {
                var zeros = new[] { Segment("", 0, isWarmup: true), Segment("", 0) };

                AssertFrame(new[] { Sgr(Dim, "▓▓▓▓▓█████") }, 10, TimelineWidget.Render(zeros, 1, 10, ColorMode.Colors16));
                AssertFrame(new[] { "----------" }, 10, TimelineWidget.Render(zeros, 0.5, 10, ColorMode.None));
            })
            .Step("Hatched segments shrink to leave every determinate segment a column when the fixed width does not fit", context =>
            {
                // Two hatched and three determinate segments in 12 columns: (12 − 3) / 2 = 4
                // each, and the 4 left split 1.33 each — 2 / 1 / 1.
                var mixed = new[] { Segment("", 1), Segment("", null), Segment("", 1, isWarmup: true), Segment("", null), Segment("", 1) };

                AssertFrame(new[] { "##▒▒▒▒=▒▒▒▒▼" }, 12, TimelineWidget.Render(mixed, 1, 12, ColorMode.None));

                // With room the hatched segments take their full eight columns.
                AssertFrame(new[] { "####▒▒▒▒▒▒▒▒===▒▒▒▒▒▒▒▒##▼" }, 26, TimelineWidget.Render(mixed, 1, 26, ColorMode.None));
            })
            .Step("A width below the segment count draws the first width segments one column each and leaves the rest out, legend included", context =>
            {
                var five = new[] { Segment("a", 1), Segment("b", null), Segment("c", 1, isWarmup: true), Segment("d", 1), Segment("e", 1) };
                var lines = TimelineWidget.Render(five, 1, 3, ColorMode.None);

                // The drawn labels never fit one column: "a" under column 0, "b" two columns
                // after it, "c" two after that — cut to the width.
                AssertFrame(new[] { "#▒▼", "a …" }, 3, lines);
            })
            .Step("Width 1 is a single column (the legend cut to an ellipsis), width 0 renders nothing, and a null list is rejected", context =>
            {
                AssertFrame(new[] { "▼", "…" }, 1, TimelineWidget.Render(ThreePhasePlan(), 0.5, 1, ColorMode.None));
                AssertFrame(new[] { "-", "…" }, 1, TimelineWidget.Render(ThreePhasePlan(), null, 1, ColorMode.None));
                AssertFrame(new[] { "▼" }, 1, TimelineWidget.Render(new[] { Segment("", 1) }, 0.5, 1, ColorMode.None));
                Assert.IsEmpty(TimelineWidget.Render(ThreePhasePlan(), 0.5, 0, ColorMode.None));
                Assert.IsEmpty(TimelineWidget.Render(ThreePhasePlan(), 0.5, -1, ColorMode.TrueColor));
                Assert.ThrowsExactly<ArgumentNullException>(() => TimelineWidget.Render(null!, 0.5, 60, ColorMode.None));
            })
            .Step("No segments is one blank line of the width", context =>
            {
                AssertFrame(new[] { Spaces(20) }, 20, TimelineWidget.Render(Array.Empty<TimelineSegment>(), 0.5, 20, ColorMode.TrueColor));
            })
            .Run();
    }

    [Test]
    public async Task Verify_labels_are_escaped_sanitized_and_listed_on_the_legend()
    {
        await Scenario()
            .Step("Brackets in a label render literally inside the bar and never style it", context =>
            {
                var bracketed = new[] { Segment("[red]x[/]", 10) };

                AssertFrame(new[] { new string('#', 24) + " [red]x[/] " + new string('#', 24) + "▼" }, 60, TimelineWidget.Render(bracketed, 1, 60, ColorMode.None));
                AssertFrame(new[] { Sgr(Yellow16, new string('█', 24)) + " " + Sgr(Yellow16, "[red]x[/]") + " " + Sgr(Yellow16, new string('█', 24)) + Sgr(BoldYellow16, "▼") }, 60, TimelineWidget.Render(bracketed, 1, 60, ColorMode.Colors16));
            })
            .Step("A label that does not fit goes to the legend, escaped and dim", context =>
            {
                AssertFrame(new[] { "#########▼", "[red]x[/] " }, 10, TimelineWidget.Render(new[] { Segment("[red]x[/]", 10) }, 1, 10, ColorMode.None));
                AssertFrame(new[] { Sgr(Yellow16, new string('█', 9)) + Sgr(BoldYellow16, "▼"), Sgr(Dim, "[red]x[/]") + " " }, 10, TimelineWidget.Render(new[] { Segment("[red]x[/]", 10) }, 1, 10, ColorMode.Colors16));
            })
            .Step("Control characters sanitize to one space each, a \\r\\n pair to one, before the label is measured", context =>
            {
                var lines = TimelineWidget.Render(new[] { Segment("\u001b[2J\tx", 10) }, null, 20, ColorMode.None);

                // ESC and TAB each become a space: " [2J x" is six columns, centred with
                // padding in twenty as six dashes each side.
                AssertFrame(new[] { "------  [2J x ------" }, 20, lines);
                Assert.DoesNotContain("\u001b", lines[0].Text);
                Assert.DoesNotContain("\t", lines[0].Text);

                AssertFrame(new[] { "---- a b ----" }, 13, TimelineWidget.Render(new[] { Segment("a\r\nb", 10) }, null, 13, ColorMode.None));
            })
            .Step("A legend label starts under its segment's first column", context =>
            {
                // Two 30-column segments: "x" fits the first; the 29-character label needs 31
                // columns and goes under column 30 of the legend.
                var plan = new[] { Segment("x", 10), Segment("the measurement phase label x", 10) };

                AssertFrame(new[]
                {
                    new string('-', 13) + " x " + new string('-', 44),
                    Spaces(30) + "the measurement phase label x" + " "
                }, 60, TimelineWidget.Render(plan, null, 60, ColorMode.None));
            })
            .Step("Legend labels whose segments start too close flow two columns apart, and a legend wider than the width is cut", context =>
            {
                // 14 s and 13 s: at 27 columns the segments are 14 and 13 wide, one short of
                // holding their 13- and 12-character labels, and the legend holds both exactly —
                // "beta label x" two columns after "alpha label x" rather than under column 14.
                var plan = new[] { Segment("alpha label x", 14), Segment("beta label x", 13) };

                AssertFrame(new[] { new string('-', 27), "alpha label x  beta label x" }, 27, TimelineWidget.Render(plan, null, 27, ColorMode.None));

                // At 10 columns the legend overruns and is cut, in the label's style.
                AssertFrame(new[] { new string('-', 10), "alpha lab…" }, 10, TimelineWidget.Render(plan, null, 10, ColorMode.None));
                AssertFrame(new[] { Sgr(Dim, new string('█', 10)), Sgr(Dim, "alpha lab…") }, 10, TimelineWidget.Render(plan, null, 10, ColorMode.TrueColor));
            })
            .Step("An empty label is neither drawn nor listed, and a default segment is an unlabelled hatched one", context =>
            {
                AssertFrame(new[] { "------ x -" }, 10, TimelineWidget.Render(new[] { Segment("", 1), Segment("x", 1) }, null, 10, ColorMode.None));
                AssertFrame(new[] { "▒▒▒▒▒▒▒▒#▼", Spaces(8) + "x " }, 10, TimelineWidget.Render(new[] { default, Segment("x", 1) }, 1, 10, ColorMode.None));
            })
            .Step("The constructor rejects a null label", context =>
            {
                Assert.ThrowsExactly<ArgumentNullException>(() => new TimelineSegment(null!, TimeSpan.FromSeconds(1), isWarmup: false));
            })
            .Run();
    }

    [Test]
    public async Task Verify_logo_gradient_interpolation_on_the_palette()
    {
        await Scenario()
            .Step("The endpoints are the palette's logo colours and the midpoint rounds half away from zero", context =>
            {
                Assert.AreEqual("#FF5C00", TerminalPalette.LogoGradientStyle(0));
                Assert.AreEqual("#FFCF6B", TerminalPalette.LogoGradientStyle(1));

                // Green 92 + 115 / 2 = 149.5 → 150, blue 107 / 2 = 53.5 → 54.
                Assert.AreEqual("#FF9636", TerminalPalette.LogoGradientStyle(0.5));
                var midpoint = TerminalPalette.LogoGradientColor(0.5);
                Assert.AreEqual(255, midpoint.Red);
                Assert.AreEqual(150, midpoint.Green);
                Assert.AreEqual(54, midpoint.Blue);
                Assert.IsNull(midpoint.PaletteIndex);
            })
            .Step("Positions outside 0..1 clamp to the nearer end and NaN reads as the start", context =>
            {
                Assert.AreEqual("#FF5C00", TerminalPalette.LogoGradientStyle(-1));
                Assert.AreEqual("#FF5C00", TerminalPalette.LogoGradientStyle(double.NegativeInfinity));
                Assert.AreEqual("#FF5C00", TerminalPalette.LogoGradientStyle(double.NaN));
                Assert.AreEqual("#FFCF6B", TerminalPalette.LogoGradientStyle(2));
                Assert.AreEqual("#FFCF6B", TerminalPalette.LogoGradientStyle(double.PositiveInfinity));
            })
            .Run();
    }

    [Test]
    public async Task Verify_timeline_never_throws_and_keeps_its_width_for_hostile_inputs()
    {
        await Scenario()
            .Step("Every width 1-200 in every color mode at every position renders one or two lines of exactly the width, the bar never truncated, with no escape bytes in None and no color parameters in Monochrome", context =>
            {
                var ellipsis = MarkupText.Ellipsis.ToString();
                foreach (var input in HostileInputs())
                {
                    foreach (var colorMode in AllColorModes)
                    {
                        foreach (var position in HostilePositions())
                        {
                            for (var width = 1; width <= 200; width++)
                            {
                                var lines = TimelineWidget.Render(input.Segments, position, width, colorMode);

                                Assert.IsGreaterThanOrEqualTo(1, lines.Count, $"{input.Name} at {width} ({colorMode}, {position})");
                                Assert.IsLessThanOrEqualTo(2, lines.Count, $"{input.Name} at {width} ({colorMode}, {position})");
                                Assert.DoesNotContain(ellipsis, lines[0].Text, $"{input.Name} bar truncated at {width} ({colorMode}, {position})");
                                for (var index = 0; index < lines.Count; index++)
                                {
                                    var line = lines[index];
                                    Assert.AreEqual(width, line.Width, $"{input.Name} row {index} width at {width} ({colorMode}, {position})");

                                    if (colorMode == ColorMode.None)
                                    {
                                        Assert.AreEqual(width, line.Text.Length, $"{input.Name} row {index} text length at {width} ({position})");
                                        Assert.DoesNotContain("\u001b", line.Text, $"{input.Name} row {index} escape at {width} ({position})");
                                    }
                                    else if (colorMode == ColorMode.Monochrome)
                                    {
                                        AssertNoColorParameters(line.Text);
                                    }
                                }
                            }
                        }
                    }
                }
            })
            .Step("A width below 1 renders nothing for every input", context =>
            {
                foreach (var input in HostileInputs())
                {
                    Assert.IsEmpty(TimelineWidget.Render(input.Segments, 0.5, 0, ColorMode.TrueColor), input.Name);
                    Assert.IsEmpty(TimelineWidget.Render(input.Segments, null, -7, ColorMode.None), input.Name);
                }
            })
            .Step("Identical inputs render an identical frame", context =>
            {
                var first = TimelineWidget.Render(IndeterminatePlan(), 0.37, 120, ColorMode.TrueColor);
                var second = TimelineWidget.Render(IndeterminatePlan(), 0.37, 120, ColorMode.TrueColor);

                Assert.HasCount(first.Count, second);
                for (var index = 0; index < first.Count; index++)
                {
                    Assert.AreEqual(first[index].Text, second[index].Text, $"Text mismatch at row {index}");
                    Assert.AreEqual(first[index].Width, second[index].Width, $"Width mismatch at row {index}");
                }
            })
            .Run();
    }

    private static IEnumerable<double?> HostilePositions()
    {
        yield return null;
        yield return 0;
        yield return 0.5;
        yield return 1;
        yield return -1;
        yield return 2;
        yield return double.NaN;
        yield return double.PositiveInfinity;
        yield return double.NegativeInfinity;
        yield return double.Epsilon;
        yield return 0.9999999999999999;
    }

    private static IEnumerable<(string Name, TimelineSegment[] Segments)> HostileInputs()
    {
        var many = new TimelineSegment[50];
        for (var index = 0; index < many.Length; index++)
            many[index] = new TimelineSegment("s" + index, index % 7 == 3 ? null : TimeSpan.FromSeconds(index + 1), index % 2 == 0);

        var longLabel = new string('x', 300);

        yield return ("no segments", Array.Empty<TimelineSegment>());
        yield return ("one determinate", new[] { Segment("only", 30) });
        yield return ("one indeterminate", new[] { Segment(OneTimeLabel, null) });
        yield return ("all indeterminate", new[] { Segment("a", null, isWarmup: true), Segment("b", null), Segment("c", null) });
        yield return ("three phase", ThreePhasePlan());
        yield return ("indeterminate plan", IndeterminatePlan());
        yield return ("zero durations", new[] { Segment("a", 0, isWarmup: true), Segment("b", 0), Segment("c", 0) });
        yield return ("negative durations", new[] { Segment("a", -1, isWarmup: true), Segment("b", -1e9), Segment("c", 5) });
        yield return ("huge durations", new[] { new TimelineSegment("a", TimeSpan.MaxValue, isWarmup: true), new TimelineSegment("b", TimeSpan.MaxValue, isWarmup: false), new TimelineSegment("c", TimeSpan.FromTicks(1), isWarmup: false) });
        yield return ("tiny and huge", new[] { new TimelineSegment("tiny", TimeSpan.FromTicks(1), isWarmup: false), new TimelineSegment("huge", TimeSpan.MaxValue, isWarmup: false), new TimelineSegment("none", null, isWarmup: false) });
        yield return ("many segments", many);
        yield return ("long labels", new[] { Segment(longLabel, 1, isWarmup: true), Segment(longLabel, null), Segment(longLabel, 2) });
        yield return ("bracket labels", new[] { Segment("[red]", 1), Segment("]]", 1), Segment("[[", null), Segment("[/]", 1), Segment("[", 1), Segment("]", 1) });
        yield return ("control labels", new[] { Segment("\u001b[2J", 1), Segment("\t\t", 1), Segment("a\r\nb", null), Segment("\0", 1), Segment("\u007f", 1), Segment(" ", 1) });
        yield return ("default segments", new TimelineSegment[3]);
        yield return ("empty labels", new[] { Segment("", 1, isWarmup: true), Segment("", null), Segment("", 1) });
        yield return ("whitespace labels", new[] { Segment("   ", 1), Segment(" ", 1) });
        yield return ("surrogate label", new[] { Segment("😀 smile", 1), Segment("plain", 1) });
    }

    private static string Spaces(int count)
    {
        return new string(' ', count);
    }

    private static string Sgr(string parameters, string text)
    {
        return "\u001b[" + parameters + "m" + text + "\u001b[0m";
    }

    // The gradient's SGR foreground parameters at a column: red stays 255 while green runs
    // 92 → 207 and blue 0 → 107 from the first column to the last, rounded half away from zero.
    private static string Gradient(int column, int width)
    {
        var position = (double)column / (width - 1);
        return "38;2;255;" + InterpolateChannel(92, 207, position) + ";" + InterpolateChannel(0, 107, position);
    }

    private static byte InterpolateChannel(byte start, byte end, double position)
    {
        return (byte)Math.Round(start + ((end - start) * position), MidpointRounding.AwayFromZero);
    }

    // Each glyph styled in the gradient of its own column, starting at firstColumn; neighbours
    // whose colours round to the same value (at 120 columns the step is under one unit per
    // column) share one run, as the widget renders them.
    private static string GradientRun(string glyphs, int firstColumn, int width)
    {
        var expected = new StringBuilder();
        var index = 0;
        while (index < glyphs.Length)
        {
            var parameters = Gradient(firstColumn + index, width);
            var run = new StringBuilder();
            while (index < glyphs.Length && Gradient(firstColumn + index, width) == parameters)
                run.Append(glyphs[index++]);

            expected.Append(Sgr(parameters, run.ToString()));
        }

        return expected.ToString();
    }

    // Monochrome may carry any decoration and reset, never a color: no SGR parameter in the
    // foreground and background ranges (30-49, which also catches the extended 38/48 forms, and
    // the bright 90-107), and every escape is an SGR sequence.
    private static void AssertNoColorParameters(string text)
    {
        var sequences = Regex.Matches(text, AnsiCodes.Escape + "\\[([0-9;]*)m");
        foreach (Match match in sequences)
        {
            foreach (var parameter in match.Groups[1].Value.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var code = int.Parse(parameter, CultureInfo.InvariantCulture);
                Assert.IsFalse((code >= 30 && code <= 49) || (code >= 90 && code <= 107), $"Color parameter {code} in Monochrome output: {match.Value}");
            }
        }

        Assert.HasCount(text.Count(character => character == AnsiCodes.Escape[0]), sequences, "Every escape must be an SGR sequence");
    }

    private static void AssertFrame(string[] expectedRows, int expectedWidth, IReadOnlyList<RenderedLine> actualLines)
    {
        Assert.HasCount(expectedRows.Length, actualLines);
        for (var row = 0; row < expectedRows.Length; row++)
        {
            Assert.AreEqual(expectedRows[row], actualLines[row].Text, $"Text mismatch at row {row}");
            Assert.AreEqual(expectedWidth, actualLines[row].Width, $"Width mismatch at row {row}");
        }
    }

    private static void AssertLine(string expectedText, int expectedWidth, RenderedLine actualLine)
    {
        Assert.AreEqual(expectedText, actualLine.Text);
        Assert.AreEqual(expectedWidth, actualLine.Width);
    }
}
