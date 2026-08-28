using Fuzn.TestFuzn.Internals.Terminal;

namespace Fuzn.TestFuzn.Tests.Terminal;

[TestClass]
public class SparklineWidgetTests : Test
{
    [Test]
    public async Task Verify_block_sparkline_golden_frames()
    {
        await Scenario()
            .Step("Width 8 renders one sample per character across all eight levels", context =>
            {
                var line = Assert.ContainsSingle(SparklineWidget.Render(
                    new double[] { 0, 1, 2, 3, 4, 5, 6, 7 }, 8, ColorMode.TrueColor, SparklineGlyphSet.Blocks));

                Assert.AreEqual("▁▂▃▄▅▆▇█", line.Text);
                Assert.AreEqual(8, line.Width);
                Assert.DoesNotContain("\u001b", line.Text);
            })
            .Step("Width 5 left-pads missing samples and renders a flat window at the middle level", context =>
            {
                Assert.AreEqual("  ▄▄▄", Assert.ContainsSingle(SparklineWidget.Render(
                    new double[] { 5, 5, 5 }, 5, ColorMode.None, SparklineGlyphSet.Blocks)).Text);
                Assert.AreEqual("  ▄", Assert.ContainsSingle(SparklineWidget.Render(
                    new double[] { 42 }, 3, ColorMode.None, SparklineGlyphSet.Blocks)).Text);
            })
            .Step("Width 4 windows to the newest samples and scales to their range", context =>
            {
                Assert.AreEqual("▁▃▆█", Assert.ContainsSingle(SparklineWidget.Render(
                    new double[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, 4, ColorMode.None, SparklineGlyphSet.Blocks)).Text);
            })
            .Step("Empty input renders blank columns and negative values scale fine", context =>
            {
                Assert.AreEqual("    ", Assert.ContainsSingle(SparklineWidget.Render(
                    Array.Empty<double>(), 4, ColorMode.None, SparklineGlyphSet.Blocks)).Text);
                Assert.AreEqual("▁▅█", Assert.ContainsSingle(SparklineWidget.Render(
                    new double[] { -2, -1, 0 }, 3, ColorMode.None, SparklineGlyphSet.Blocks)).Text);
            })
            .Run();
    }

    [Test]
    public async Task Verify_sparkline_non_finite_samples()
    {
        await Scenario()
            .Step("NaN renders a blank column and stays out of the scale", context =>
            {
                Assert.AreEqual("▁ █", Assert.ContainsSingle(SparklineWidget.Render(
                    new[] { 1, double.NaN, 3 }, 3, ColorMode.None, SparklineGlyphSet.Blocks)).Text);
            })
            .Step("Positive infinity clamps to the top level, negative infinity to the bottom", context =>
            {
                Assert.AreEqual("▁██", Assert.ContainsSingle(SparklineWidget.Render(
                    new[] { 1, double.PositiveInfinity, 3 }, 3, ColorMode.None, SparklineGlyphSet.Blocks)).Text);
                Assert.AreEqual("▁▁█", Assert.ContainsSingle(SparklineWidget.Render(
                    new[] { 1, double.NegativeInfinity, 3 }, 3, ColorMode.None, SparklineGlyphSet.Blocks)).Text);
            })
            .Step("Windows without any finite sample still render", context =>
            {
                Assert.AreEqual("    ", Assert.ContainsSingle(SparklineWidget.Render(
                    new[] { double.NaN, double.NaN }, 4, ColorMode.None, SparklineGlyphSet.Blocks)).Text);
                Assert.AreEqual(" █", Assert.ContainsSingle(SparklineWidget.Render(
                    new[] { double.NaN, double.PositiveInfinity }, 2, ColorMode.None, SparklineGlyphSet.Blocks)).Text);
            })
            .Step("A finite spread wider than double.MaxValue still maps min to bottom and max to top", context =>
            {
                Assert.AreEqual("▁█", Assert.ContainsSingle(SparklineWidget.Render(
                    new[] { -1e308, 1e308 }, 2, ColorMode.None, SparklineGlyphSet.Blocks)).Text);
                Assert.AreEqual("▁█", Assert.ContainsSingle(SparklineWidget.Render(
                    new[] { double.MinValue, double.MaxValue }, 2, ColorMode.None, SparklineGlyphSet.Blocks)).Text);
                Assert.AreEqual(" ⣸", Assert.ContainsSingle(SparklineWidget.Render(
                    new[] { -1e308, 1e308 }, 2, ColorMode.None)).Text);
            })
            .Run();
    }

    [Test]
    public async Task Verify_braille_sparkline_golden_frames()
    {
        await Scenario()
            .Step("Width 2 packs two samples per character at four levels", context =>
            {
                Assert.AreEqual("⣠⣾", Assert.ContainsSingle(SparklineWidget.Render(
                    new double[] { 0, 1, 2, 3 }, 2, ColorMode.None)).Text);
            })
            .Step("An odd sample count leaves the leading half-character column blank", context =>
            {
                Assert.AreEqual("⢀⣾", Assert.ContainsSingle(SparklineWidget.Render(
                    new double[] { 0, 1, 2 }, 2, ColorMode.None)).Text);
            })
            .Step("A flat window renders two-dot columns and short input left-pads with spaces", context =>
            {
                Assert.AreEqual("⣤⣤", Assert.ContainsSingle(SparklineWidget.Render(
                    new double[] { 7, 7, 7, 7 }, 2, ColorMode.None)).Text);
                Assert.AreEqual("  ⣸", Assert.ContainsSingle(SparklineWidget.Render(
                    new double[] { 1, 2 }, 3, ColorMode.None)).Text);
            })
            .Step("NaN gaps blank single braille columns", context =>
            {
                Assert.AreEqual("⡀⢸", Assert.ContainsSingle(SparklineWidget.Render(
                    new[] { 1, double.NaN, double.NaN, 3 }, 2, ColorMode.None)).Text);
            })
            .Run();
    }

    [Test]
    public async Task Verify_sparkline_styling_and_edges()
    {
        await Scenario()
            .Step("A style wraps the whole line and follows the color mode", context =>
            {
                var line = Assert.ContainsSingle(SparklineWidget.Render(
                    new double[] { 0, 3 }, 1, ColorMode.TrueColor, style: "green"));

                Assert.AreEqual("\u001b[38;5;2m⣸\u001b[0m", line.Text);
                Assert.AreEqual(1, line.Width);
                Assert.AreEqual("⣸", Assert.ContainsSingle(SparklineWidget.Render(
                    new double[] { 0, 3 }, 1, ColorMode.None, style: "green")).Text);
            })
            .Step("Without a style the line is plain text in every color mode", context =>
            {
                var line = Assert.ContainsSingle(SparklineWidget.Render(
                    new double[] { 1, 2, 3 }, 5, ColorMode.TrueColor, SparklineGlyphSet.Blocks));

                Assert.AreEqual("  ▁▅█", line.Text);
                Assert.AreEqual(5, line.Width);
                Assert.DoesNotContain("\u001b", line.Text);
            })
            .Step("A width below 1 renders nothing and null values are rejected", context =>
            {
                Assert.IsEmpty(SparklineWidget.Render(new double[] { 1 }, 0, ColorMode.None));
                Assert.ThrowsExactly<ArgumentNullException>(() => SparklineWidget.Render(null!, 5, ColorMode.None));
            })
            .Run();
    }
}
