using Fuzn.TestFuzn.Internals.Terminal;

namespace Fuzn.TestFuzn.Tests.Terminal;

[TestClass]
public class ProgressBarWidgetTests : Test
{
    [Test]
    public async Task Verify_progress_bar_golden_frames_at_fixed_widths()
    {
        await Scenario()
            .Step("Width 10 splits into a 5-cell bar and the percent label", context =>
            {
                Assert.AreEqual("███░░  50%", Assert.ContainsSingle(ProgressBarWidget.Render(0.5, 10, ColorMode.None)).Text);
                Assert.AreEqual("░░░░░   0%", Assert.ContainsSingle(ProgressBarWidget.Render(0.0, 10, ColorMode.None)).Text);
                Assert.AreEqual("█████ 100%", Assert.ContainsSingle(ProgressBarWidget.Render(1.0, 10, ColorMode.None)).Text);
            })
            .Step("Width 20 with a bar style renders only the filled cells styled", context =>
            {
                var line = Assert.ContainsSingle(ProgressBarWidget.Render(0.25, 20, ColorMode.TrueColor, barStyle: "green"));

                Assert.AreEqual("\u001b[38;5;2m████\u001b[0m░░░░░░░░░░░  25%", line.Text);
                Assert.AreEqual(20, line.Width);
            })
            .Step("The bar style follows the color mode", context =>
            {
                Assert.AreEqual(
                    "\u001b[32m████\u001b[0m░  70%",
                    Assert.ContainsSingle(ProgressBarWidget.Render(0.7, 10, ColorMode.Colors16, barStyle: "green")).Text);
                Assert.AreEqual(
                    "███░░  50%",
                    Assert.ContainsSingle(ProgressBarWidget.Render(0.5, 10, ColorMode.None, barStyle: "green")).Text);
            })
            .Run();
    }

    [Test]
    public async Task Verify_progress_bar_clamps_out_of_range_fractions()
    {
        await Scenario()
            .Step("Fractions below 0, NaN, and negative infinity clamp to an empty bar", context =>
            {
                Assert.AreEqual("░░░░░   0%", Assert.ContainsSingle(ProgressBarWidget.Render(-0.5, 10, ColorMode.None)).Text);
                Assert.AreEqual("░░░░░   0%", Assert.ContainsSingle(ProgressBarWidget.Render(double.NaN, 10, ColorMode.None)).Text);
                Assert.AreEqual("░░░░░   0%", Assert.ContainsSingle(ProgressBarWidget.Render(double.NegativeInfinity, 10, ColorMode.None)).Text);
            })
            .Step("Fractions above 1 and positive infinity clamp to a full bar", context =>
            {
                Assert.AreEqual("█████ 100%", Assert.ContainsSingle(ProgressBarWidget.Render(1.5, 10, ColorMode.None)).Text);
                Assert.AreEqual("█████ 100%", Assert.ContainsSingle(ProgressBarWidget.Render(double.PositiveInfinity, 10, ColorMode.None)).Text);
            })
            .Run();
    }

    [Test]
    public async Task Verify_progress_bar_label_omission_and_narrow_widths()
    {
        await Scenario()
            .Step("Without the label the bar takes the full width", context =>
            {
                Assert.AreEqual("████░░░░", Assert.ContainsSingle(ProgressBarWidget.Render(0.5, 8, ColorMode.None, showPercentLabel: false)).Text);
            })
            .Step("Width 6 keeps a single bar cell beside the label", context =>
            {
                Assert.AreEqual("█  50%", Assert.ContainsSingle(ProgressBarWidget.Render(0.5, 6, ColorMode.None)).Text);
            })
            .Step("The label is omitted when it would leave no bar cell", context =>
            {
                Assert.AreEqual("███░░", Assert.ContainsSingle(ProgressBarWidget.Render(0.5, 5, ColorMode.None)).Text);
            })
            .Step("A width below 1 renders nothing", context =>
            {
                Assert.IsEmpty(ProgressBarWidget.Render(0.5, 0, ColorMode.None));
            })
            .Run();
    }
}
