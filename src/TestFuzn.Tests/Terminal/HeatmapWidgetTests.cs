using System.Globalization;
using System.Text.RegularExpressions;
using Fuzn.TestFuzn.Internals.Terminal;

namespace Fuzn.TestFuzn.Tests.Terminal;

/// <summary>
/// Golden frames for <see cref="HeatmapWidget"/>, derived by hand from the bucket counts: a
/// cell's step is ceil(count × 6 / max(sample total, reference)), the reference being the
/// median of the visible non-empty totals — 1 for a share up to a sixth, 6 for the whole
/// total at a typical volume — drawn as <c>.:=+*#</c> without color, ░▒▓ olive / ▓█ yellow /
/// █ bold white in Colors16, and █ in the six gradient colors in TrueColor. Most samples
/// below have a total of 6, the median, so each share is a whole number of sixths and the
/// frames can be checked by eye against the comments; the two quieter ones show the volume
/// weighting.
/// </summary>
[TestClass]
public class HeatmapWidgetTests : Test
{
    private const string Dim = "2";
    private const string Step1 = "38;2;58;20;0";
    private const string Step2 = "38;2;156;56;0";
    private const string Step3 = "38;2;255;92;0";
    private const string Step4 = "38;2;255;149;54";
    private const string Step5 = "38;2;255;207;107";
    private const string Step6 = "38;2;255;255;255";
    private const string On1 = "48;2;58;20;0";
    private const string On3 = "48;2;255;92;0";
    private const string On5 = "48;2;255;207;107";
    private const string Olive16 = "33";
    private const string Yellow16 = "93";
    private const string BoldWhite16 = "1;97";

    private static readonly HeatmapOptions BodyOnly = new HeatmapOptions { ShowLabels = false };

    /// <summary>The fifteen bucket edges, fastest first; the widest ("≤ 100 ms" and its siblings) is 8 columns.</summary>
    private static readonly string[] StandardLabels =
    {
        "≤ 1 ms", "≤ 2 ms", "≤ 5 ms", "≤ 10 ms", "≤ 20 ms", "≤ 50 ms", "≤ 100 ms", "≤ 200 ms", "≤ 500 ms",
        "≤ 1 s", "≤ 2 s", "≤ 5 s", "≤ 10 s", "≤ 30 s", "> 30 s"
    };

    /// <summary>
    /// Twelve samples, oldest first, each a column of the goldens: A — 6 in bucket 3 (a full
    /// column, #); B — 3 in bucket 3 and 3 in bucket 4 (halves, =); C — 1 in each of buckets
    /// 0-5 (sixths, .); D — 2 in bucket 6 and 4 in bucket 7 (a full merged row); E — all zeros;
    /// F — null; G — three counts only, 5 in bucket 2 (5 of the typical 6: *); H — sixteen
    /// counts, the extra one 99 (a blank column: the extra count is ignored); I — a negative
    /// count and 3 in bucket 1 (3 of 6: =); J — int.MaxValue in buckets 13 and 14 (halves,
    /// summed in 64 bits, far above the reference); K — 5 in bucket 8 and 1 in bucket 9 (*, .);
    /// L — 4 in bucket 10 and 2 in bucket 11 (+, :). The non-empty totals are 6, 6, 6, 6, 5, 3,
    /// 2 × int.MaxValue, 6 and 6, so the median reference is 6.
    /// </summary>
    private static IReadOnlyList<int>[] LatencySeries()
    {
        var sixteen = new int[16];
        sixteen[15] = 99;

        return new IReadOnlyList<int>[]
        {
            Counts((3, 6)),
            Counts((3, 3), (4, 3)),
            Counts((0, 1), (1, 1), (2, 1), (3, 1), (4, 1), (5, 1)),
            Counts((6, 2), (7, 4)),
            new int[15],
            null!,
            new[] { 0, 0, 5 },
            sixteen,
            new[] { -5, 3 },
            Counts((13, int.MaxValue), (14, int.MaxValue)),
            Counts((8, 5), (9, 1)),
            Counts((10, 4), (11, 2))
        };
    }

    /// <summary>
    /// Nine two-bucket samples [a, 6 − a] for a = 1, 2, 3, 4, 5, 6, 0, 6, 3, so the fast bucket's
    /// step is a and the slow bucket's 6 − a.
    /// </summary>
    private static IReadOnlyList<int>[] TwoBucketSeries()
    {
        var series = new IReadOnlyList<int>[9];
        var fastCounts = new[] { 1, 2, 3, 4, 5, 6, 0, 6, 3 };
        for (var index = 0; index < series.Length; index++)
            series[index] = new[] { fastCounts[index], 6 - fastCounts[index] };

        return series;
    }

    private static readonly string[] TwoBucketLabels = { "fast", "slow" };

    private static int[] Counts(params (int Bucket, int Count)[] entries)
    {
        var counts = new int[15];
        foreach (var entry in entries)
            counts[entry.Bucket] = entry.Count;

        return counts;
    }

    private static IReadOnlyList<int>[] OneHot(int bucket)
    {
        return new IReadOnlyList<int>[] { Counts((bucket, 1)) };
    }

    [Test]
    public async Task Verify_latency_heatmap_golden_frame_at_80x14()
    {
        // Fifteen buckets over fourteen rows merge only the middle pair (buckets 6 and 7 under
        // the slower one's label, "≤ 200 ms"); the label column is 8 + 1 wide, the body 71, so
        // the twelve samples sit in the rightmost twelve columns after 59 blank ones.
        var blank = new string(' ', 59);

        await Scenario()
            .Step("Color mode None draws the ASCII ramp under the right-aligned labels", context =>
            {
                var lines = HeatmapWidget.Render(LatencySeries(), StandardLabels, 80, 14, ColorMode.None);

                AssertFrame(new[]
                {
                    "  > 30 s " + blank + "         =  ",
                    "  ≤ 30 s " + blank + "         =  ",
                    "  ≤ 10 s " + blank + "            ",
                    "   ≤ 5 s " + blank + "           :",
                    "   ≤ 2 s " + blank + "           +",
                    "   ≤ 1 s " + blank + "          . ",
                    "≤ 500 ms " + blank + "          * ",
                    "≤ 200 ms " + blank + "   #        ",
                    " ≤ 50 ms " + blank + "  .         ",
                    " ≤ 20 ms " + blank + " =.         ",
                    " ≤ 10 ms " + blank + "#=.         ",
                    "  ≤ 5 ms " + blank + "  .   *     ",
                    "  ≤ 2 ms " + blank + "  .     =   ",
                    "  ≤ 1 ms " + blank + "  .         "
                }, 80, lines);

                foreach (var line in lines)
                    Assert.DoesNotContain("\u001b", line.Text);
            })
            .Step("TrueColor draws every cell as a full block in its step's gradient color, the labels dim", context =>
            {
                var lines = HeatmapWidget.Render(LatencySeries(), StandardLabels, 80, 14, ColorMode.TrueColor);

                AssertFrame(new[]
                {
                    "  " + Sgr(Dim, "> 30 s") + " " + blank + "         " + Sgr(Step3, "█") + "  ",
                    "  " + Sgr(Dim, "≤ 30 s") + " " + blank + "         " + Sgr(Step3, "█") + "  ",
                    "  " + Sgr(Dim, "≤ 10 s") + " " + blank + "            ",
                    "   " + Sgr(Dim, "≤ 5 s") + " " + blank + "           " + Sgr(Step2, "█"),
                    "   " + Sgr(Dim, "≤ 2 s") + " " + blank + "           " + Sgr(Step4, "█"),
                    "   " + Sgr(Dim, "≤ 1 s") + " " + blank + "          " + Sgr(Step1, "█") + " ",
                    Sgr(Dim, "≤ 500 ms") + " " + blank + "          " + Sgr(Step5, "█") + " ",
                    Sgr(Dim, "≤ 200 ms") + " " + blank + "   " + Sgr(Step6, "█") + "        ",
                    " " + Sgr(Dim, "≤ 50 ms") + " " + blank + "  " + Sgr(Step1, "█") + "         ",
                    " " + Sgr(Dim, "≤ 20 ms") + " " + blank + " " + Sgr(Step3, "█") + Sgr(Step1, "█") + "         ",
                    " " + Sgr(Dim, "≤ 10 ms") + " " + blank + Sgr(Step6, "█") + Sgr(Step3, "█") + Sgr(Step1, "█") + "         ",
                    "  " + Sgr(Dim, "≤ 5 ms") + " " + blank + "  " + Sgr(Step1, "█") + "   " + Sgr(Step5, "█") + "     ",
                    "  " + Sgr(Dim, "≤ 2 ms") + " " + blank + "  " + Sgr(Step1, "█") + "     " + Sgr(Step3, "█") + "   ",
                    "  " + Sgr(Dim, "≤ 1 ms") + " " + blank + "  " + Sgr(Step1, "█") + "         "
                }, 80, lines);
            })
            .Step("Colors16 keeps to the yellow accent: shade blocks in olive, then bright yellow, bold white on top, equal neighbours in one run", context =>
            {
                var lines = HeatmapWidget.Render(LatencySeries(), StandardLabels, 80, 14, ColorMode.Colors16);

                Assert.HasCount(14, lines);
                AssertLine("  " + Sgr(Dim, "> 30 s") + " " + blank + "         " + Sgr(Olive16, "▓") + "  ", 80, lines[0]);
                AssertLine("   " + Sgr(Dim, "≤ 5 s") + " " + blank + "           " + Sgr(Olive16, "▒"), 80, lines[3]);
                AssertLine("   " + Sgr(Dim, "≤ 2 s") + " " + blank + "           " + Sgr(Yellow16, "▓"), 80, lines[4]);
                AssertLine("   " + Sgr(Dim, "≤ 1 s") + " " + blank + "          " + Sgr(Olive16, "░") + " ", 80, lines[5]);
                AssertLine(Sgr(Dim, "≤ 500 ms") + " " + blank + "          " + Sgr(Yellow16, "█") + " ", 80, lines[6]);
                AssertLine(Sgr(Dim, "≤ 200 ms") + " " + blank + "   " + Sgr(BoldWhite16, "█") + "        ", 80, lines[7]);
                AssertLine(" " + Sgr(Dim, "≤ 20 ms") + " " + blank + " " + Sgr(Olive16, "▓░") + "         ", 80, lines[9]);
                AssertLine(" " + Sgr(Dim, "≤ 10 ms") + " " + blank + Sgr(BoldWhite16, "█") + Sgr(Olive16, "▓░") + "         ", 80, lines[10]);
                AssertLine("  " + Sgr(Dim, "≤ 5 ms") + " " + blank + "  " + Sgr(Olive16, "░") + "   " + Sgr(Yellow16, "█") + "     ", 80, lines[11]);
                AssertLine("  " + Sgr(Dim, "≤ 2 ms") + " " + blank + "  " + Sgr(Olive16, "░") + "     " + Sgr(Olive16, "▓") + "   ", 80, lines[12]);
            })
            .Step("Monochrome keeps the dim labels and the ASCII ramp with no color codes", context =>
            {
                var lines = HeatmapWidget.Render(LatencySeries(), StandardLabels, 80, 14, ColorMode.Monochrome);

                Assert.HasCount(14, lines);
                AssertLine("  " + Sgr(Dim, "> 30 s") + " " + blank + "         =  ", 80, lines[0]);
                AssertLine(" " + Sgr(Dim, "≤ 10 ms") + " " + blank + "#=.         ", 80, lines[10]);
                foreach (var line in lines)
                    AssertNoColorParameters(line.Text);
            })
            .Run();
    }

    [Test]
    public async Task Verify_merged_rows_golden_frame_at_50x8()
    {
        // Eight rows for fifteen buckets: every pair merges from the middle outward and the
        // slowest bucket keeps its own row — [0,1] [2,3] [4,5] [6,7] [8,9] [10,11] [12,13] [14],
        // each under its slower member's label. The body is 41 columns, 29 of them blank.
        var blank = new string(' ', 29);

        await Scenario()
            .Step("Color mode None sums each merged row's counts: D fills [6,7], K fills [8,9], L fills [10,11], C's sixths pair into thirds, G and I stay volume-weighted", context =>
            {
                AssertFrame(new[]
                {
                    "  > 30 s " + blank + "         =  ",
                    "  ≤ 30 s " + blank + "         =  ",
                    "   ≤ 5 s " + blank + "           #",
                    "   ≤ 1 s " + blank + "          # ",
                    "≤ 200 ms " + blank + "   #        ",
                    " ≤ 50 ms " + blank + " =:         ",
                    " ≤ 10 ms " + blank + "#=:   *     ",
                    "  ≤ 2 ms " + blank + "  :     =   "
                }, 50, HeatmapWidget.Render(LatencySeries(), StandardLabels, 50, 8, ColorMode.None));
            })
            .Step("TrueColor renders the same rows in the gradient", context =>
            {
                AssertFrame(new[]
                {
                    "  " + Sgr(Dim, "> 30 s") + " " + blank + "         " + Sgr(Step3, "█") + "  ",
                    "  " + Sgr(Dim, "≤ 30 s") + " " + blank + "         " + Sgr(Step3, "█") + "  ",
                    "   " + Sgr(Dim, "≤ 5 s") + " " + blank + "           " + Sgr(Step6, "█"),
                    "   " + Sgr(Dim, "≤ 1 s") + " " + blank + "          " + Sgr(Step6, "█") + " ",
                    Sgr(Dim, "≤ 200 ms") + " " + blank + "   " + Sgr(Step6, "█") + "        ",
                    " " + Sgr(Dim, "≤ 50 ms") + " " + blank + " " + Sgr(Step3, "█") + Sgr(Step2, "█") + "         ",
                    " " + Sgr(Dim, "≤ 10 ms") + " " + blank + Sgr(Step6, "█") + Sgr(Step3, "█") + Sgr(Step2, "█") + "   " + Sgr(Step5, "█") + "     ",
                    "  " + Sgr(Dim, "≤ 2 ms") + " " + blank + "  " + Sgr(Step2, "█") + "     " + Sgr(Step3, "█") + "   "
                }, 50, HeatmapWidget.Render(LatencySeries(), StandardLabels, 50, 8, ColorMode.TrueColor));
            })
            .Step("Colors16 renders the same rows in the accent", context =>
            {
                var lines = HeatmapWidget.Render(LatencySeries(), StandardLabels, 50, 8, ColorMode.Colors16);

                Assert.HasCount(8, lines);
                AssertLine("   " + Sgr(Dim, "≤ 5 s") + " " + blank + "           " + Sgr(BoldWhite16, "█"), 50, lines[2]);
                AssertLine(" " + Sgr(Dim, "≤ 50 ms") + " " + blank + " " + Sgr(Olive16, "▓▒") + "         ", 50, lines[5]);
                AssertLine(" " + Sgr(Dim, "≤ 10 ms") + " " + blank + Sgr(BoldWhite16, "█") + Sgr(Olive16, "▓▒") + "   " + Sgr(Yellow16, "█") + "     ", 50, lines[6]);
                AssertLine("  " + Sgr(Dim, "≤ 2 ms") + " " + blank + "  " + Sgr(Olive16, "▒") + "     " + Sgr(Olive16, "▓") + "   ", 50, lines[7]);
            })
            .Run();
    }

    /// <summary>
    /// The row groups fifteen buckets merge into at every height, as bucket counts per row
    /// from the fastest: each merge joins the smallest adjacent pair nearest the middle.
    /// </summary>
    private static IEnumerable<(int Height, int[] GroupSizes)> MergedGroupSizes()
    {
        yield return (15, new[] { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 });
        yield return (14, new[] { 1, 1, 1, 1, 1, 1, 2, 1, 1, 1, 1, 1, 1, 1 });
        yield return (13, new[] { 1, 1, 1, 1, 1, 1, 2, 2, 1, 1, 1, 1, 1 });
        yield return (12, new[] { 1, 1, 1, 1, 2, 2, 2, 1, 1, 1, 1, 1 });
        yield return (11, new[] { 1, 1, 1, 1, 2, 2, 2, 2, 1, 1, 1 });
        yield return (10, new[] { 1, 1, 2, 2, 2, 2, 2, 1, 1, 1 });
        yield return (9, new[] { 1, 1, 2, 2, 2, 2, 2, 2, 1 });
        yield return (8, new[] { 2, 2, 2, 2, 2, 2, 2, 1 });
        yield return (7, new[] { 2, 2, 2, 2, 2, 2, 3 });
        yield return (6, new[] { 2, 2, 4, 2, 2, 3 });
        yield return (5, new[] { 2, 2, 4, 4, 3 });
        yield return (4, new[] { 4, 4, 4, 3 });
        yield return (3, new[] { 4, 4, 7 });
        yield return (2, new[] { 8, 7 });
        yield return (1, new[] { 15 });
    }

    [Test]
    public async Task Verify_rows_merge_from_the_middle_outward()
    {
        await Scenario()
            .Step("A count in one bucket lights exactly the row of the group that bucket merged into, under the group's slowest label", context =>
            {
                foreach (var (height, groupSizes) in MergedGroupSizes())
                {
                    var firstBucket = 0;
                    for (var group = 0; group < groupSizes.Length; group++)
                    {
                        var lastBucket = firstBucket + groupSizes[group] - 1;
                        var expectedRow = height - 1 - group;
                        for (var bucket = firstBucket; bucket <= lastBucket; bucket++)
                        {
                            var lines = HeatmapWidget.Render(OneHot(bucket), StandardLabels, 20, height, ColorMode.None);

                            Assert.HasCount(height, lines, $"Height {height}");
                            for (var row = 0; row < height; row++)
                            {
                                if (row == expectedRow)
                                {
                                    Assert.EndsWith("#", lines[row].Text, $"Bucket {bucket} at height {height} should light row {row}");
                                    Assert.StartsWith(StandardLabels[lastBucket] + " ", lines[row].Text.TrimStart(), $"Bucket {bucket} at height {height}");
                                }
                                else
                                {
                                    Assert.DoesNotContain("#", lines[row].Text, $"Bucket {bucket} at height {height} lit row {row}");
                                }
                            }
                        }

                        firstBucket = lastBucket + 1;
                    }

                    Assert.AreEqual(15, firstBucket, $"The groups at height {height} must cover every bucket");
                }
            })
            .Step("A merged row's share is the sum of its members' shares", context =>
            {
                // 1 in each of buckets 0-14 (total 15): at height 1 the single row holds it all.
                var flat = new IReadOnlyList<int>[] { Counts((0, 1), (1, 1), (2, 1), (3, 1), (4, 1), (5, 1), (6, 1), (7, 1), (8, 1), (9, 1), (10, 1), (11, 1), (12, 1), (13, 1), (14, 1)) };

                AssertFrame(new[] { "> 30 s    #" }, 11, HeatmapWidget.Render(flat, StandardLabels, 11, 1, ColorMode.None));

                // At height 2 the rows hold 8/15 (ceil(3.2) = 4, +) and 7/15 (ceil(2.8) = 3, =).
                AssertFrame(new[] { "  > 30 s    =", "≤ 200 ms    +" }, 13, HeatmapWidget.Render(flat, StandardLabels, 13, 2, ColorMode.None));
            })
            .Step("Rows past the last bucket stay blank at the bottom, with no label", context =>
            {
                var lines = HeatmapWidget.Render(OneHot(0), StandardLabels, 20, 17, ColorMode.None);

                Assert.HasCount(17, lines);
                Assert.StartsWith("  > 30 s ", lines[0].Text);
                AssertLine("  ≤ 1 ms " + new string(' ', 10) + "#", 20, lines[14]);
                AssertLine(new string(' ', 20), 20, lines[15]);
                AssertLine(new string(' ', 20), 20, lines[16]);
            })
            .Run();
    }

    [Test]
    public async Task Verify_half_cells_double_the_visible_span_in_true_color()
    {
        await Scenario()
            .Step("Nine samples over six columns pack two per column: the older half as the foreground of ▌, the newer as its background, a blank half without color", context =>
            {
                // Twelve slots, the nine samples in the last nine: column 0 is blank, column 1
                // holds only the oldest sample in its right half, columns 2-5 hold two each.
                // Slow row (6 − a): 5 | 4 on 3 | 2 on 1 | blank, 6 | blank, 3.
                // Fast row (a):     1 | 2 on 3 | 4 on 5 | 6, blank | 6 on 3.
                AssertFrame(new[]
                {
                    " " + Sgr(Step5, "▐") + Sgr(Step4 + ";" + On3, "▌") + Sgr(Step2 + ";" + On1, "▌") + Sgr(Step6, "▐") + Sgr(Step3, "▐"),
                    " " + Sgr(Step1, "▐") + Sgr(Step2 + ";" + On3, "▌") + Sgr(Step4 + ";" + On5, "▌") + Sgr(Step6, "▌") + Sgr(Step6 + ";" + On3, "▌")
                }, 6, HeatmapWidget.Render(TwoBucketSeries(), TwoBucketLabels, 6, 2, ColorMode.TrueColor, BodyOnly));
            })
            .Step("Without colors the same series scrolls to the newest six samples, one per column", context =>
            {
                AssertFrame(new[] { ":. # =", "+*# #=" }, 6, HeatmapWidget.Render(TwoBucketSeries(), TwoBucketLabels, 6, 2, ColorMode.None, BodyOnly));
                AssertFrame(new[] { ":. # =", "+*# #=" }, 6, HeatmapWidget.Render(TwoBucketSeries(), TwoBucketLabels, 6, 2, ColorMode.Monochrome, BodyOnly));
            })
            .Step("Colors16 scrolls too, its steps being glyphs rather than colors a half cell could carry", context =>
            {
                AssertFrame(new[]
                {
                    Sgr(Olive16, "▒░") + " " + Sgr(BoldWhite16, "█") + " " + Sgr(Olive16, "▓"),
                    Sgr(Yellow16, "▓█") + Sgr(BoldWhite16, "█") + " " + Sgr(BoldWhite16, "█") + Sgr(Olive16, "▓")
                }, 6, HeatmapWidget.Render(TwoBucketSeries(), TwoBucketLabels, 6, 2, ColorMode.Colors16, BodyOnly));
            })
            .Step("A window that fits draws one full block per column, and the time window is what decides", context =>
            {
                var newestSix = TwoBucketSeries().Skip(3).ToArray();
                var fitted = new[]
                {
                    Sgr(Step2, "█") + Sgr(Step1, "█") + " " + Sgr(Step6, "█") + " " + Sgr(Step3, "█"),
                    Sgr(Step4, "█") + Sgr(Step5, "█") + Sgr(Step6, "█") + " " + Sgr(Step6, "█") + Sgr(Step3, "█")
                };

                AssertFrame(fitted, 6, HeatmapWidget.Render(newestSix, TwoBucketLabels, 6, 2, ColorMode.TrueColor, BodyOnly));
                AssertFrame(fitted, 6, HeatmapWidget.Render(TwoBucketSeries(), TwoBucketLabels, 6, 2, ColorMode.TrueColor, new HeatmapOptions { ShowLabels = false, TimeWindow = 6 }));

                // A window below 1 means no limit: back to half cells.
                var unlimited = HeatmapWidget.Render(TwoBucketSeries(), TwoBucketLabels, 6, 2, ColorMode.TrueColor, new HeatmapOptions { ShowLabels = false, TimeWindow = 0 });
                Assert.Contains("▌", unlimited[0].Text);
                Assert.AreEqual(6, unlimited[0].Width);
            })
            .Step("One sample more than the body switches to half cells, the newest sample still at the right edge", context =>
            {
                var lines = HeatmapWidget.Render(TwoBucketSeries().Skip(2).ToArray(), TwoBucketLabels, 6, 2, ColorMode.TrueColor, BodyOnly);

                // Seven samples in twelve slots: columns 0-1 blank, column 2 the oldest alone in
                // its right half, the newest (a = 3) as the background of the last column.
                AssertLine("  " + Sgr(Step3, "▐") + Sgr(Step2 + ";" + On1, "▌") + Sgr(Step6, "▐") + Sgr(Step3, "▐"), 6, lines[0]);
                AssertLine("  " + Sgr(Step3, "▐") + Sgr(Step4 + ";" + On5, "▌") + Sgr(Step6, "▌") + Sgr(Step6 + ";" + On3, "▌"), 6, lines[1]);
            })
            .Step("A shorter series leaves its unused columns blank on the left", context =>
            {
                AssertFrame(new[] { "    *+", "    .:" }, 6, HeatmapWidget.Render(TwoBucketSeries().Take(2).ToArray(), TwoBucketLabels, 6, 2, ColorMode.None, BodyOnly));
            })
            .Run();
    }

    [Test]
    public async Task Verify_label_column_degradation()
    {
        await Scenario()
            .Step("The label column keeps exactly MinimumBodyWidth body columns before it goes", context =>
            {
                var width = 9 + HeatmapWidget.MinimumBodyWidth;
                var lines = HeatmapWidget.Render(OneHot(3), StandardLabels, width, 15, ColorMode.None);

                Assert.HasCount(15, lines);
                AssertLine("  > 30 s     ", width, lines[0]);
                AssertLine(" ≤ 10 ms    #", width, lines[11]);
                AssertLine("  ≤ 1 ms     ", width, lines[14]);
            })
            .Step("Then the column goes, labels included, leaving the body the whole width", context =>
            {
                var width = 9 + HeatmapWidget.MinimumBodyWidth - 1;
                var lines = HeatmapWidget.Render(OneHot(3), StandardLabels, width, 15, ColorMode.None);

                Assert.HasCount(15, lines);
                AssertLine(new string(' ', width - 1) + "#", width, lines[11]);
                foreach (var line in lines)
                {
                    Assert.AreEqual(width, line.Width);
                    Assert.DoesNotContain("≤", line.Text);
                    Assert.DoesNotContain(">", line.Text);
                }
            })
            .Step("Labels can be turned off, and a set of empty labels has no column either", context =>
            {
                AssertLine(new string(' ', 12) + "#", 13, HeatmapWidget.Render(OneHot(3), StandardLabels, 13, 15, ColorMode.None, BodyOnly)[11]);

                var empty = new string[15];
                Array.Fill(empty, string.Empty);
                AssertLine(new string(' ', 12) + "#", 13, HeatmapWidget.Render(OneHot(3), empty, 13, 15, ColorMode.None)[11]);
            })
            .Step("A wide label grows the column rather than truncating", context =>
            {
                var labels = (string[])StandardLabels.Clone();
                labels[14] = "> 30 s (open-ended)";

                var lines = HeatmapWidget.Render(OneHot(3), labels, 30, 15, ColorMode.None);

                AssertLine("> 30 s (open-ended) " + new string(' ', 10), 30, lines[0]);
                AssertLine(new string(' ', 12) + "≤ 10 ms " + new string(' ', 9) + "#", 30, lines[11]);
                foreach (var line in lines)
                    Assert.DoesNotContain(MarkupText.Ellipsis.ToString(), line.Text);
            })
            .Step("Only the labels of the rows shown set the column width", context =>
            {
                // At height 1 the one row is the slowest bucket's: "> 30 s" is 6 wide, so the
                // column is 7 and the body 6.
                AssertFrame(new[] { "> 30 s      #" }, 13, HeatmapWidget.Render(OneHot(3), StandardLabels, 13, 1, ColorMode.None));
            })
            .Run();
    }

    [Test]
    public async Task Verify_empty_and_hostile_samples()
    {
        await Scenario()
            .Step("No samples at all renders the labels over a blank body", context =>
            {
                var blank = new string(' ', 41);

                AssertFrame(new[]
                {
                    "  > 30 s " + blank,
                    "  ≤ 30 s " + blank,
                    "   ≤ 5 s " + blank,
                    "   ≤ 1 s " + blank,
                    "≤ 200 ms " + blank,
                    " ≤ 50 ms " + blank,
                    " ≤ 10 ms " + blank,
                    "  ≤ 2 ms " + blank
                }, 50, HeatmapWidget.Render(Array.Empty<IReadOnlyList<int>>(), StandardLabels, 50, 8, ColorMode.None));
            })
            .Step("All-zero and null samples are blank columns, as is a sample with only negative counts", context =>
            {
                var samples = new IReadOnlyList<int>[] { new int[15], null!, new[] { -1, -2, -3 }, new int[15] };
                var lines = HeatmapWidget.Render(samples, StandardLabels, 20, 15, ColorMode.TrueColor);

                Assert.HasCount(15, lines);
                foreach (var line in lines)
                {
                    Assert.AreEqual(20, line.Width);
                    Assert.DoesNotContain("█", line.Text);
                    Assert.DoesNotContain("38;2", line.Text);
                }
            })
            .Step("Neither samples nor labels renders blank rows of the width", context =>
            {
                AssertFrame(new[] { "          ", "          ", "          " }, 10, HeatmapWidget.Render(Array.Empty<IReadOnlyList<int>>(), Array.Empty<string>(), 10, 3, ColorMode.None));
            })
            .Step("Without labels the longest sample sets the bucket count, so a body-only render still draws every bucket", context =>
            {
                // Sixteen buckets, from the sixteen-count sample H — whose extra 99 now counts,
                // so H is a full column of 99 rather than blank. One row at height 1: A-D, H,
                // J, K, L fill it, G is 5 of the median 6 and I is 3 of 6, E and F stay blank.
                AssertFrame(new[] { "####  *#=###" }, 12, HeatmapWidget.Render(LatencySeries(), Array.Empty<string>(), 12, 1, ColorMode.None));

                // At full height the sixteenth bucket is the top row, lit only in H's column (the
                // eighth of twelve in a 40-wide body), and the fifteen below match the labelled
                // buckets rendered body-only (H's total does not move the median).
                var unlabelled = HeatmapWidget.Render(LatencySeries(), Array.Empty<string>(), 40, 16, ColorMode.TrueColor);
                var bodyOnly = HeatmapWidget.Render(LatencySeries(), StandardLabels, 40, 16, ColorMode.TrueColor, BodyOnly);
                Assert.HasCount(16, unlabelled);
                AssertLine(new string(' ', 35) + Sgr(Step6, "█") + "    ", 40, unlabelled[0]);
                for (var row = 1; row < 16; row++)
                    Assert.AreEqual(bodyOnly[row - 1].Text, unlabelled[row].Text, $"Row {row} of the sixteen-bucket frame should be row {row - 1} of the labelled fifteen");

                // Ragged samples: the longest decides, shorter ones are padded — and the lone
                // request (total 1 against the median 4) is a quarter step, 2.
                AssertFrame(new[] { "  #", "  .", " : " }, 3, HeatmapWidget.Render(new IReadOnlyList<int>[] { new[] { 1 }, new[] { 0, 1, 6 } }, Array.Empty<string>(), 3, 3, ColorMode.None));
            })
            .Step("A short sample is padded with zeros, a long one truncated to the labels, a negative count reads as zero", context =>
            {
                // Totals 5, 0 and 3: the reference is the median 4, so G's 5 is a full step and
                // I's 3 is three quarters — step 5.
                var samples = new IReadOnlyList<int>[] { new[] { 0, 0, 5 }, new[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 99 }, new[] { -5, 3 } };
                var lines = HeatmapWidget.Render(samples, StandardLabels, 12, 15, ColorMode.None, BodyOnly);

                AssertLine("         #  ", 12, lines[12]);
                AssertLine("           *", 12, lines[13]);
                for (var row = 0; row < 15; row++)
                {
                    if (row != 12 && row != 13)
                        AssertLine("            ", 12, lines[row]);
                }
            })
            .Step("Huge counts sum in 64 bits: int.MaxValue in every bucket is fifteen equal fifteenths", context =>
            {
                var huge = new int[15];
                Array.Fill(huge, int.MaxValue);
                var lines = HeatmapWidget.Render(new IReadOnlyList<int>[] { huge }, StandardLabels, 4, 15, ColorMode.None, BodyOnly);

                // 1/15 is under a sixth: step 1 in every row.
                foreach (var line in lines)
                    AssertLine("   .", 4, line);

                // Two buckets of int.MaxValue and thirteen of zero: exact halves, step 3.
                var halves = HeatmapWidget.Render(new IReadOnlyList<int>[] { Counts((13, int.MaxValue), (14, int.MaxValue)) }, StandardLabels, 4, 15, ColorMode.None, BodyOnly);
                AssertLine("   =", 4, halves[0]);
                AssertLine("   =", 4, halves[1]);
                AssertLine("    ", 4, halves[2]);
            })
            .Step("A width or height below 1 renders nothing", context =>
            {
                Assert.IsEmpty(HeatmapWidget.Render(LatencySeries(), StandardLabels, 0, 8, ColorMode.None));
                Assert.IsEmpty(HeatmapWidget.Render(LatencySeries(), StandardLabels, 50, 0, ColorMode.None));
                Assert.IsEmpty(HeatmapWidget.Render(LatencySeries(), StandardLabels, -1, -1, ColorMode.TrueColor));
            })
            .Step("Null lists are rejected", context =>
            {
                Assert.ThrowsExactly<ArgumentNullException>(() => HeatmapWidget.Render(null!, StandardLabels, 50, 8, ColorMode.None));
                Assert.ThrowsExactly<ArgumentNullException>(() => HeatmapWidget.Render(LatencySeries(), null!, 50, 8, ColorMode.None));
            })
            .Run();
    }

    [Test]
    public async Task Verify_intensity_is_weighted_by_volume()
    {
        await Scenario()
            .Step("A ramp-up brightens as each column's total approaches the median of the visible totals", context =>
            {
                // Totals 1, 2, 5, 10, 20, 50, 100, 100 all in the one bucket: the median of the
                // eight is (10 + 20) / 2 = 15, so the factors are 1/15, 2/15, 1/3, 2/3, 1, 1, 1, 1
                // and the steps ceil(6 × factor) = 1, 1, 2, 4, 6, 6, 6, 6.
                var ramp = new IReadOnlyList<int>[] { new[] { 1 }, new[] { 2 }, new[] { 5 }, new[] { 10 }, new[] { 20 }, new[] { 50 }, new[] { 100 }, new[] { 100 } };

                AssertFrame(new[] { "all ..:+####" }, 12, HeatmapWidget.Render(ramp, new[] { "all" }, 12, 1, ColorMode.None));
                AssertFrame(new[] { Sgr(Dim, "all") + " " + Sgr(Step1, "██") + Sgr(Step2, "█") + Sgr(Step4, "█") + Sgr(Step6, "████") }, 12, HeatmapWidget.Render(ramp, new[] { "all" }, 12, 1, ColorMode.TrueColor));
            })
            .Step("One request and a thousand no longer look alike", context =>
            {
                // Totals 1 and 1000: the reference is 500.5, so the lone request is a faint
                // 1/500.5 (step 1) while the busy second is a full column. Width 8 keeps the
                // label column its four body columns, two of them blank.
                var pair = new IReadOnlyList<int>[] { new[] { 1 }, new[] { 1000 } };

                AssertFrame(new[] { "all   .#" }, 8, HeatmapWidget.Render(pair, new[] { "all" }, 8, 1, ColorMode.None));
                AssertFrame(new[] { Sgr(Dim, "all") + "   " + Sgr(Step1, "█") + Sgr(Step6, "█") }, 8, HeatmapWidget.Render(pair, new[] { "all" }, 8, 1, ColorMode.TrueColor));
            })
            .Step("A steady load is unweighted whatever its volume, and a single non-empty column has factor 1", context =>
            {
                // Halves at 6 and at 600 per second render the same; a lone sample is its own reference.
                AssertFrame(new[] { "==", "==" }, 2, HeatmapWidget.Render(new IReadOnlyList<int>[] { new[] { 3, 3 }, new[] { 3, 3 } }, TwoBucketLabels, 2, 2, ColorMode.None, BodyOnly));
                AssertFrame(new[] { "==", "==" }, 2, HeatmapWidget.Render(new IReadOnlyList<int>[] { new[] { 300, 300 }, new[] { 300, 300 } }, TwoBucketLabels, 2, 2, ColorMode.None, BodyOnly));
                AssertFrame(new[] { " ", "#" }, 1, HeatmapWidget.Render(new IReadOnlyList<int>[] { new[] { 1, 0 } }, TwoBucketLabels, 1, 2, ColorMode.None, BodyOnly));
            })
            .Step("The reference is a median, so one burst cannot dim the rest, and an odd count takes the middle total", context =>
            {
                // Totals 6, 6, 6, 6, 6000: the median stays 6 and the four steady columns keep
                // the top step; totals 1, 4, 100: the median 4 makes the 1 a quarter (step 2).
                AssertFrame(new[] { "#####" }, 5, HeatmapWidget.Render(new IReadOnlyList<int>[] { new[] { 6 }, new[] { 6 }, new[] { 6 }, new[] { 6 }, new[] { 6000 } }, new[] { "all" }, 5, 1, ColorMode.None, BodyOnly));
                AssertFrame(new[] { ":##" }, 3, HeatmapWidget.Render(new IReadOnlyList<int>[] { new[] { 1 }, new[] { 4 }, new[] { 100 } }, new[] { "all" }, 3, 1, ColorMode.None, BodyOnly));
            })
            .Step("Merged rows weight their summed counts the same way", context =>
            {
                // [1, 1] and [10, 10] merged into one row: totals 2 and 20, reference 11, so
                // the quiet column is 2/11 (step 2) and the busy one full.
                AssertFrame(new[] { ":#" }, 2, HeatmapWidget.Render(new IReadOnlyList<int>[] { new[] { 1, 1 }, new[] { 10, 10 } }, TwoBucketLabels, 2, 1, ColorMode.None, BodyOnly));
            })
            .Step("Only the visible samples set the reference", context =>
            {
                // A 1000-request second scrolled off the left does not dim the two visible ones.
                AssertFrame(new[] { "##" }, 2, HeatmapWidget.Render(new IReadOnlyList<int>[] { new[] { 1000 }, new[] { 1 }, new[] { 1 } }, new[] { "all" }, 2, 1, ColorMode.None, BodyOnly));
                AssertFrame(new[] { "##" }, 2, HeatmapWidget.Render(new IReadOnlyList<int>[] { new[] { 1000 }, new[] { 1 }, new[] { 1 } }, new[] { "all" }, 2, 1, ColorMode.None, new HeatmapOptions { ShowLabels = false, TimeWindow = 2 }));
            })
            .Run();
    }

    [Test]
    public async Task Verify_labels_are_sanitized_and_styled()
    {
        await Scenario()
            .Step("Control characters in a label sanitize to spaces and brackets render literally", context =>
            {
                var labels = new[] { "\u001b[31m1 ms\t", "x" };
                var lines = HeatmapWidget.Render(OneHot(0), labels, 15, 2, ColorMode.None);

                // ESC and TAB each become a space: the label is " [31m1 ms " (10 columns), then
                // the separating space and the four body columns.
                AssertFrame(new[] { "         x     ", " [31m1 ms     #" }, 15, lines);
                foreach (var line in lines)
                {
                    Assert.DoesNotContain("\u001b", line.Text);
                    Assert.DoesNotContain("\t", line.Text);
                }

                AssertFrame(new[] { "[2]     ", "[1]    #" }, 8, HeatmapWidget.Render(OneHot(0), new[] { "[1]", "[2]" }, 8, 2, ColorMode.None));
            })
            .Step("A null label is an empty one", context =>
            {
                AssertFrame(new[] { "x     ", "     #" }, 6, HeatmapWidget.Render(OneHot(0), new[] { null!, "x" }, 6, 2, ColorMode.None));
            })
            .Step("The label style follows the options and an unresolvable one renders plain", context =>
            {
                var samples = OneHot(0);

                AssertLine(Sgr("1", "fast") + " " + "   " + Sgr(Step6, "█"), 9, HeatmapWidget.Render(samples, TwoBucketLabels, 9, 2, ColorMode.TrueColor, new HeatmapOptions { LabelStyle = "bold" })[1]);
                AssertLine("fast " + "   " + Sgr(Step6, "█"), 9, HeatmapWidget.Render(samples, TwoBucketLabels, 9, 2, ColorMode.TrueColor, new HeatmapOptions { LabelStyle = null })[1]);
                AssertLine("fast " + "   " + Sgr(Step6, "█"), 9, HeatmapWidget.Render(samples, TwoBucketLabels, 9, 2, ColorMode.TrueColor, new HeatmapOptions { LabelStyle = "nonsense" })[1]);
                AssertLine("fast " + "   " + Sgr(Step6, "█"), 9, HeatmapWidget.Render(samples, TwoBucketLabels, 9, 2, ColorMode.TrueColor, new HeatmapOptions { LabelStyle = "dim][red" })[1]);
                AssertLine("fast    #", 9, HeatmapWidget.Render(samples, TwoBucketLabels, 9, 2, ColorMode.None, new HeatmapOptions { LabelStyle = "bold" })[1]);
            })
            .Step("Identical inputs render an identical frame", context =>
            {
                var first = HeatmapWidget.Render(LatencySeries(), StandardLabels, 120, 12, ColorMode.TrueColor);
                var second = HeatmapWidget.Render(LatencySeries(), StandardLabels, 120, 12, ColorMode.TrueColor);

                Assert.HasCount(first.Count, second);
                for (var index = 0; index < first.Count; index++)
                {
                    Assert.AreEqual(first[index].Text, second[index].Text, $"Text mismatch at row {index}");
                    Assert.AreEqual(first[index].Width, second[index].Width, $"Width mismatch at row {index}");
                }
            })
            .Run();
    }

    [Test]
    public async Task Verify_heatmap_never_throws_and_keeps_its_width_for_hostile_inputs()
    {
        await Scenario()
            .Step("Every width 1-200 and height 0-20 in every color mode renders height lines of exactly the width, never truncated, with no escape bytes in None and no color parameters in Monochrome", context =>
            {
                var ellipsis = MarkupText.Ellipsis.ToString();
                foreach (var input in HostileInputs())
                {
                    foreach (var colorMode in new[] { ColorMode.None, ColorMode.Monochrome, ColorMode.Colors16, ColorMode.TrueColor })
                    {
                        for (var width = 1; width <= 200; width++)
                        {
                            for (var height = 0; height <= 20; height++)
                            {
                                var lines = HeatmapWidget.Render(input.Samples, input.Labels, width, height, colorMode, input.Options);

                                Assert.HasCount(height, lines, $"{input.Name} at {width}x{height} ({colorMode})");
                                for (var index = 0; index < lines.Count; index++)
                                {
                                    var line = lines[index];
                                    Assert.AreEqual(width, line.Width, $"{input.Name} row {index} width at {width}x{height} ({colorMode})");
                                    Assert.DoesNotContain(ellipsis, line.Text, $"{input.Name} row {index} truncated at {width}x{height} ({colorMode})");

                                    if (colorMode == ColorMode.None)
                                    {
                                        Assert.AreEqual(width, line.Text.Length, $"{input.Name} row {index} text length at {width}x{height}");
                                        Assert.DoesNotContain("\u001b", line.Text, $"{input.Name} row {index} escape at {width}x{height}");
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
            .Run();
    }

    private static IEnumerable<(string Name, IReadOnlyList<int>[] Samples, string[] Labels, HeatmapOptions? Options)> HostileInputs()
    {
        var zeros = new IReadOnlyList<int>[5];
        for (var index = 0; index < zeros.Length; index++)
            zeros[index] = new int[15];

        var huge = new int[15];
        Array.Fill(huge, int.MaxValue);
        var hugeSeries = new IReadOnlyList<int>[30];
        Array.Fill(hugeSeries, huge);

        var longSeries = new IReadOnlyList<int>[500];
        for (var index = 0; index < longSeries.Length; index++)
            longSeries[index] = Counts((index % 15, index), ((index * 7) % 15, 3));

        var wideLabels = new string[15];
        for (var index = 0; index < wideLabels.Length; index++)
            wideLabels[index] = "bucket " + index + " " + new string('x', 52);

        var controlLabels = new[] { "\u001b[2J", "\t\t", "[[", "]]", "[/]", "a\r\nb", "\u007f", "[bold]", "on", "", "x", "≤ 1 s", " ", "\0", "> 30 s" };

        var manyLabels = new string[30];
        for (var index = 0; index < manyLabels.Length; index++)
            manyLabels[index] = "b" + index;

        var emptyLabels = new string[15];
        Array.Fill(emptyLabels, string.Empty);

        yield return ("no samples", Array.Empty<IReadOnlyList<int>>(), StandardLabels, null);
        yield return ("no samples, no labels", Array.Empty<IReadOnlyList<int>>(), Array.Empty<string>(), null);
        yield return ("no labels", LatencySeries(), Array.Empty<string>(), null);
        yield return ("no labels, long series", longSeries, Array.Empty<string>(), null);
        yield return ("all zeros", zeros, StandardLabels, null);
        yield return ("null samples", new IReadOnlyList<int>[] { null!, null!, null! }, StandardLabels, null);
        yield return ("single sample", OneHot(7), StandardLabels, null);
        yield return ("ragged samples", new IReadOnlyList<int>[] { new[] { 1 }, Array.Empty<int>(), new int[20], new[] { 5, 5, 5 }, null! }, StandardLabels, null);
        yield return ("negative counts", new IReadOnlyList<int>[] { new[] { -1, -2, -3, -4, -5, -6, -7, -8, -9, -10, -11, -12, -13, -14, -15 }, new[] { -5, 3, -1 }, new[] { int.MinValue, int.MaxValue } }, StandardLabels, null);
        yield return ("huge counts", hugeSeries, StandardLabels, null);
        yield return ("long series", longSeries, StandardLabels, null);
        yield return ("wide labels", LatencySeries(), wideLabels, null);
        yield return ("control labels", LatencySeries(), controlLabels, new HeatmapOptions { LabelStyle = "bold" });
        yield return ("more labels than counts", LatencySeries(), manyLabels, null);
        yield return ("one bucket", new IReadOnlyList<int>[] { new[] { 1 }, new[] { 2 }, new[] { 0 }, new[] { -1 }, new[] { int.MaxValue } }, new[] { "all" }, null);
        yield return ("empty labels", LatencySeries(), emptyLabels, null);
        yield return ("windowed", longSeries, StandardLabels, new HeatmapOptions { TimeWindow = 5 });
        yield return ("labels off", LatencySeries(), StandardLabels, BodyOnly);
        yield return ("bad label style", LatencySeries(), StandardLabels, new HeatmapOptions { LabelStyle = "dim][red" });
        yield return ("null labels", LatencySeries(), new[] { null!, "x", null!, "≤ 1 s" }, null);
    }

    private static string Sgr(string parameters, string text)
    {
        return "\u001b[" + parameters + "m" + text + "\u001b[0m";
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
