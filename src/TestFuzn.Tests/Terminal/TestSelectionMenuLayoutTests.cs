using Fuzn.TestFuzn.Internals.Terminal;

namespace Fuzn.TestFuzn.Tests.Terminal;

/// <summary>
/// Golden frames for the test selection menu at three terminal sizes — 120×40 with the full
/// banner, 80×24 with the compact wordmark on the title row, 60×15 with no logo and a
/// scrolling window — every expected line hand-written from the layout rules, plus the status
/// line's variants, the highlighted row's and the matched text's styling, name escaping and
/// truncation, the logo and row-count rules at their boundaries, and a size sweep pinning
/// that no line is ever wider than the width and the frame is always exactly the height.
/// </summary>
[TestClass]
public class TestSelectionMenuLayoutTests : Test
{
    private const string BannerFirstRow = "████████╗███████╗███████╗████████╗███████╗██╗   ██╗███████╗███╗   ██╗";
    private const string BannerLastRow = "   ╚═╝   ╚══════╝╚══════╝   ╚═╝   ╚═╝      ╚═════╝ ╚══════╝╚═╝  ╚═══╝";
    private const string Title = "Select a test to run";
    private const string StatusForTwelveTests = "Filter: ▏  12 tests";
    private const string FullFooter = "↑↓ move · enter run · type to filter · esc quit · pgup/pgdn page · home/end first/last";
    private const string FooterWithoutHomeEnd = "↑↓ move · enter run · type to filter · esc quit · pgup/pgdn page";
    private const string FooterWithoutPaging = "↑↓ move · enter run · type to filter · esc quit";

    private static readonly string[] TestNames =
    {
        "Fuzn.Shop.Tests.CartTests.Verify_add_to_cart",
        "Fuzn.Shop.Tests.CatalogTests.Verify_browse_products",
        "Fuzn.Shop.Tests.CatalogTests.Verify_search_products",
        "Fuzn.Shop.Tests.CheckoutTests.Verify_checkout",
        "Fuzn.Shop.Tests.CheckoutTests.Verify_payment_declined",
        "Fuzn.Shop.Tests.LoginTests.Verify_login",
        "Fuzn.Shop.Tests.LoginTests.Verify_logout",
        "Fuzn.Shop.Tests.OrderTests.Verify_order_history",
        "Fuzn.Shop.Tests.OrderTests.Verify_order_tracking",
        "Fuzn.Shop.Tests.ProductTests.Verify_product_details",
        "Fuzn.Shop.Tests.ProductTests.Verify_product_reviews",
        "Fuzn.Shop.Tests.SearchLoadTests.Verify_search_under_load"
    };

    // The unfiltered list with the first test highlighted: a pointer column, the number
    // right-aligned to two digits, two spaces, the name.
    private static readonly string[] UnfilteredRows =
    {
        "▸  1  Fuzn.Shop.Tests.CartTests.Verify_add_to_cart",
        "   2  Fuzn.Shop.Tests.CatalogTests.Verify_browse_products",
        "   3  Fuzn.Shop.Tests.CatalogTests.Verify_search_products",
        "   4  Fuzn.Shop.Tests.CheckoutTests.Verify_checkout",
        "   5  Fuzn.Shop.Tests.CheckoutTests.Verify_payment_declined",
        "   6  Fuzn.Shop.Tests.LoginTests.Verify_login",
        "   7  Fuzn.Shop.Tests.LoginTests.Verify_logout",
        "   8  Fuzn.Shop.Tests.OrderTests.Verify_order_history",
        "   9  Fuzn.Shop.Tests.OrderTests.Verify_order_tracking",
        "  10  Fuzn.Shop.Tests.ProductTests.Verify_product_details",
        "  11  Fuzn.Shop.Tests.ProductTests.Verify_product_reviews",
        "  12  Fuzn.Shop.Tests.SearchLoadTests.Verify_search_under_load"
    };

    private static TestSelectionMenuState NewState(int width, int height)
    {
        return new TestSelectionMenuState(TestNames) { VisibleRowCount = TestSelectionMenuLayout.MeasureListRowCount(width, height) };
    }

    private static List<string> RenderText(TestSelectionMenuState state, int width, int height, ColorMode colorMode = ColorMode.None)
    {
        return TestSelectionMenuLayout.Render(state, width, height, colorMode).Select(line => line.Text).ToList();
    }

    private static void Type(TestSelectionMenuState state, string text)
    {
        foreach (var character in text)
            state.AppendToFilter(character);
    }

    [Test]
    public async Task Verify_golden_frame_at_120_by_40_with_the_full_banner()
    {
        await Scenario()
            .Step("The banner, a blank, the title, a blank, 28 list rows, a blank, the status line and the footer on the last row", context =>
            {
                var state = NewState(120, 40);
                Assert.AreEqual(28, state.VisibleRowCount);

                var lines = RenderText(state, 120, 40);

                Assert.HasCount(40, lines);
                Assert.AreEqual(BannerFirstRow, lines[0]);
                Assert.AreEqual(BannerLastRow, lines[5]);
                var banner = LogoWidget.Render(LogoWidget.BannerWidth, LogoWidget.BannerHeight, ColorMode.None);
                for (var row = 1; row < 5; row++)
                    Assert.AreEqual(banner[row].Text, lines[row], $"Banner row {row}");

                Assert.AreEqual(string.Empty, lines[6]);
                Assert.AreEqual(Title, lines[7]);
                Assert.AreEqual(string.Empty, lines[8]);

                for (var row = 0; row < 12; row++)
                    Assert.AreEqual(UnfilteredRows[row], lines[9 + row], $"List row {row}");
                for (var row = 21; row < 37; row++)
                    Assert.AreEqual(string.Empty, lines[row], $"Empty list row at line {row}");

                Assert.AreEqual(string.Empty, lines[37]);
                Assert.AreEqual(StatusForTwelveTests, lines[38]);
                Assert.AreEqual(FullFooter, lines[39]);
            })
            .Step("Every line is plain, break-free and declared no wider than the window", context =>
            {
                var lines = TestSelectionMenuLayout.Render(NewState(120, 40), 120, 40, ColorMode.None);

                foreach (var line in lines)
                {
                    Assert.DoesNotContain("", line.Text);
                    Assert.IsLessThanOrEqualTo(120, line.Width);
                    Assert.IsLessThanOrEqualTo(line.Width, line.Text.Length);
                }
            })
            .Run();
    }

    [Test]
    public async Task Verify_golden_frame_at_80_by_24_with_the_compact_wordmark()
    {
        await Scenario()
            .Step("The wordmark sits right-aligned on the title row, 19 list rows follow, the footer drops the home/end hint", context =>
            {
                var state = NewState(80, 24);
                Assert.AreEqual(19, state.VisibleRowCount);

                var lines = RenderText(state, 80, 24);

                Assert.HasCount(24, lines);
                // The title is fitted to 80 - 11 (wordmark) - 2 (gap) = 67 columns.
                Assert.AreEqual(Title + new string(' ', 47) + "  ⚡ TestFuzn", lines[0]);
                Assert.AreEqual(string.Empty, lines[1]);

                for (var row = 0; row < 12; row++)
                    Assert.AreEqual(UnfilteredRows[row], lines[2 + row], $"List row {row}");
                for (var row = 14; row < 21; row++)
                    Assert.AreEqual(string.Empty, lines[row], $"Empty list row at line {row}");

                Assert.AreEqual(string.Empty, lines[21]);
                Assert.AreEqual(StatusForTwelveTests, lines[22]);
                Assert.AreEqual(FooterWithoutHomeEnd, lines[23]);
            })
            .Step("The title row declares the full width with the wordmark's lightning counted two columns", context =>
            {
                var titleLine = TestSelectionMenuLayout.Render(NewState(80, 24), 80, 24, ColorMode.None)[0];

                Assert.AreEqual(80, titleLine.Width);
                Assert.AreEqual(79, titleLine.Text.Length);
            })
            .Run();
    }

    [Test]
    public async Task Verify_golden_frame_at_60_by_15_without_a_logo_and_a_scrolling_window()
    {
        await Scenario()
            .Step("Ten list rows show the first ten tests, the widest name truncated with an ellipsis, the footer keeps four hints", context =>
            {
                var state = NewState(60, 15);
                Assert.AreEqual(10, state.VisibleRowCount);

                var lines = RenderText(state, 60, 15);

                Assert.HasCount(15, lines);
                Assert.AreEqual(Title, lines[0]);
                Assert.AreEqual(string.Empty, lines[1]);
                for (var row = 0; row < 10; row++)
                    Assert.AreEqual(UnfilteredRows[row], lines[2 + row], $"List row {row}");
                Assert.AreEqual(string.Empty, lines[12]);
                Assert.AreEqual(StatusForTwelveTests, lines[13]);
                Assert.AreEqual(FooterWithoutPaging, lines[14]);
            })
            .Step("End scrolls the window so the last test is on the last list row, highlighted and cut at 60 columns", context =>
            {
                var state = NewState(60, 15);
                state.MoveToLast();
                Assert.AreEqual(2, state.FirstVisibleMatch);

                var lines = RenderText(state, 60, 15);

                Assert.HasCount(15, lines);
                Assert.AreEqual("   3  Fuzn.Shop.Tests.CatalogTests.Verify_search_products", lines[2]);
                Assert.AreEqual("  11  Fuzn.Shop.Tests.ProductTests.Verify_product_reviews", lines[10]);
                Assert.AreEqual("▸ 12  Fuzn.Shop.Tests.SearchLoadTests.Verify_search_under_l…", lines[11]);
                Assert.AreEqual(60, lines[11].Length);
                Assert.AreEqual(string.Empty, lines[12]);
                Assert.AreEqual(StatusForTwelveTests, lines[13]);
            })
            .Run();
    }

    [Test]
    public async Task Verify_filter_number_entry_and_empty_states_on_the_frame()
    {
        await Scenario()
            .Step("A text filter lists only the matches under their own numbers and counts them on the status line", context =>
            {
                var state = NewState(80, 24);
                Type(state, "cat");

                var lines = RenderText(state, 80, 24);

                Assert.AreEqual("▸  2  Fuzn.Shop.Tests.CatalogTests.Verify_browse_products", lines[2]);
                Assert.AreEqual("   3  Fuzn.Shop.Tests.CatalogTests.Verify_search_products", lines[3]);
                Assert.AreEqual(string.Empty, lines[4]);
                Assert.AreEqual("Filter: cat▏  2 of 12 tests", lines[22]);
            })
            .Step("A number entry keeps the whole list, moves the highlight to the numbered test and names it on the status line", context =>
            {
                var state = NewState(80, 24);
                Type(state, "12");

                var lines = RenderText(state, 80, 24);

                Assert.AreEqual("   1  Fuzn.Shop.Tests.CartTests.Verify_add_to_cart", lines[2]);
                Assert.AreEqual("▸ 12  Fuzn.Shop.Tests.SearchLoadTests.Verify_search_under_load", lines[13]);
                Assert.AreEqual("Filter: 12▏  test 12 of 12", lines[22]);
            })
            .Step("Navigating away from the entered test moves the pointer and drops the named test from the status line for the plain count", context =>
            {
                var state = NewState(80, 24);
                Type(state, "3");
                state.MoveDown();

                var lines = RenderText(state, 80, 24);

                Assert.AreEqual("   3  Fuzn.Shop.Tests.CatalogTests.Verify_search_products", lines[4]);
                Assert.AreEqual("▸  4  Fuzn.Shop.Tests.CheckoutTests.Verify_checkout", lines[5]);
                Assert.AreEqual("  12  Fuzn.Shop.Tests.SearchLoadTests.Verify_search_under_load", lines[13]);
                Assert.AreEqual("Filter: 3▏  12 tests", lines[22]);

                state.MoveUp();
                Assert.AreEqual("Filter: 3▏  test 3 of 12", RenderText(state, 80, 24)[22]);
            })
            .Step("A filter matching nothing leaves the list blank and says so", context =>
            {
                var state = NewState(80, 24);
                Type(state, "zzz");

                var lines = RenderText(state, 80, 24);

                for (var row = 2; row < 21; row++)
                    Assert.AreEqual(string.Empty, lines[row], $"List row at line {row}");
                Assert.AreEqual("Filter: zzz▏  no matching tests", lines[22]);
            })
            .Step("No tests at all renders a blank list and says so; a single test is counted in the singular", context =>
            {
                var empty = new TestSelectionMenuState(Array.Empty<string>()) { VisibleRowCount = 19 };
                var lines = RenderText(empty, 80, 24);
                Assert.HasCount(24, lines);
                for (var row = 2; row < 21; row++)
                    Assert.AreEqual(string.Empty, lines[row], $"List row at line {row}");
                Assert.AreEqual("Filter: ▏  no tests", lines[22]);

                var single = new TestSelectionMenuState(new[] { "Fuzn.Shop.Tests.CartTests.Verify_add_to_cart" }) { VisibleRowCount = 19 };
                lines = RenderText(single, 80, 24);
                Assert.AreEqual("▸ 1  Fuzn.Shop.Tests.CartTests.Verify_add_to_cart", lines[2]);
                Assert.AreEqual("Filter: ▏  1 test", lines[22]);
            })
            .Run();
    }

    [Test]
    public async Task Verify_styling_of_the_highlight_the_match_and_the_status_line()
    {
        await Scenario()
            .Step("TrueColor styles the highlighted row whole in the warm accent with the matched text underlined inside it", context =>
            {
                var state = NewState(80, 24);
                Type(state, "cat");

                var lines = RenderText(state, 80, 24, ColorMode.TrueColor);

                Assert.AreEqual(
                    "[1;38;2;255;157;61m▸  2  Fuzn.Shop.Tests.[0m"
                        + "[1;4;38;2;255;157;61mCat[0m"
                        + "[1;38;2;255;157;61malogTests.Verify_browse_products[0m",
                    lines[2]);
                Assert.AreEqual(
                    "  [2m 3[0m  Fuzn.Shop.Tests.[4mCat[0malogTests.Verify_search_products",
                    lines[3]);
                Assert.AreEqual("Filter: cat[2m▏[0m  [2m2 of 12 tests[0m", lines[22]);
            })
            .Step("Without a filter the other rows dim only their number and the highlighted row carries no underline", context =>
            {
                var lines = RenderText(NewState(80, 24), 80, 24, ColorMode.TrueColor);

                Assert.AreEqual("[1;38;2;255;157;61m▸  1  Fuzn.Shop.Tests.CartTests.Verify_add_to_cart[0m", lines[2]);
                Assert.AreEqual("  [2m 2[0m  Fuzn.Shop.Tests.CatalogTests.Verify_browse_products", lines[3]);
                Assert.AreEqual("Filter: [2m▏[0m  [2m12 tests[0m", lines[22]);
            })
            .Step("A number entry underlines nothing and a no-match status is a warning", context =>
            {
                var state = NewState(80, 24);
                Type(state, "1");
                var lines = RenderText(state, 80, 24, ColorMode.TrueColor);
                Assert.AreEqual("[1;38;2;255;157;61m▸  1  Fuzn.Shop.Tests.CartTests.Verify_add_to_cart[0m", lines[2]);
                Assert.AreEqual("Filter: 1[2m▏[0m  [2mtest 1 of 12[0m", lines[22]);

                var noMatch = NewState(80, 24);
                Type(noMatch, "zzz");
                lines = RenderText(noMatch, 80, 24, ColorMode.Colors16);
                Assert.AreEqual("Filter: zzz[2m▏[0m  [93mno matching tests[0m", lines[22]);
            })
            .Step("Monochrome keeps the decorations and drops the color", context =>
            {
                var state = NewState(80, 24);
                Type(state, "cat");

                var lines = RenderText(state, 80, 24, ColorMode.Monochrome);

                Assert.AreEqual(
                    "[1m▸  2  Fuzn.Shop.Tests.[0m[1;4mCat[0m[1malogTests.Verify_browse_products[0m",
                    lines[2]);
            })
            .Run();
    }

    [Test]
    public async Task Verify_names_are_escaped_sanitized_and_truncated()
    {
        await Scenario()
            .Step("Brackets in a name render literally and never open a style", context =>
            {
                var state = new TestSelectionMenuState(new[] { "Tests.Weird[bold]name[/]", "Tests.Plain" }) { VisibleRowCount = 19 };

                var lines = RenderText(state, 80, 24, ColorMode.TrueColor);

                Assert.AreEqual("[1;38;2;255;157;61m▸ 1  Tests.Weird[bold]name[/][0m", lines[2]);
                Assert.AreEqual("  [2m2[0m  Tests.Plain", lines[3]);
            })
            .Step("A filter with brackets is escaped on the status line and underlines its literal match", context =>
            {
                var state = new TestSelectionMenuState(new[] { "Tests.Weird[1]", "Tests.Plain" }) { VisibleRowCount = 19 };
                Type(state, "[1]");

                var lines = RenderText(state, 80, 24, ColorMode.TrueColor);

                Assert.AreEqual("[1;38;2;255;157;61m▸ 1  Tests.Weird[0m[1;4;38;2;255;157;61m[1][0m", lines[2]);
                Assert.AreEqual("Filter: [1][2m▏[0m  [2m1 of 2 tests[0m", lines[22]);
            })
            .Step("Control characters in a name become spaces so a row stays one row", context =>
            {
                var state = new TestSelectionMenuState(new[] { "Tests.Two\nLines\tTabbed" }) { VisibleRowCount = 19 };

                var lines = RenderText(state, 80, 24);

                Assert.AreEqual("▸ 1  Tests.Two Lines Tabbed", lines[2]);
            })
            .Step("A name wider than the window is cut with an ellipsis at the window width", context =>
            {
                var state = new TestSelectionMenuState(new[] { "Tests." + new string('x', 200) }) { VisibleRowCount = 19 };

                var lines = TestSelectionMenuLayout.Render(state, 40, 24, ColorMode.None);

                Assert.AreEqual("▸ 1  Tests." + new string('x', 28) + "…", lines[2].Text);
                Assert.AreEqual(40, lines[2].Width);
            })
            .Step("A match whose edge would split a surrogate pair is not underlined", context =>
            {
                var state = new TestSelectionMenuState(new[] { "Tests.Rocket🚀Launch" }) { VisibleRowCount = 19 };
                state.AppendToFilter('\uD83D');

                var lines = RenderText(state, 80, 24, ColorMode.TrueColor);

                Assert.AreEqual("[1;38;2;255;157;61m▸ 1  Tests.Rocket🚀Launch[0m", lines[2]);

                state.AppendToFilter('\uDE80');
                lines = RenderText(state, 80, 24, ColorMode.TrueColor);
                Assert.AreEqual("[1;38;2;255;157;61m▸ 1  Tests.Rocket[0m[1;4;38;2;255;157;61m🚀[0m[1;38;2;255;157;61mLaunch[0m", lines[2]);
            })
            .Run();
    }

    [Test]
    public async Task Verify_logo_and_row_count_rules_at_their_boundaries()
    {
        await Scenario()
            .Step("No logo below 80 columns, the banner from 27 rows at 80 columns, the compact wordmark below that", context =>
            {
                Assert.AreEqual(LogoVariant.None, TestSelectionMenuLayout.SelectLogoVariant(79, 40));
                Assert.AreEqual(LogoVariant.None, TestSelectionMenuLayout.SelectLogoVariant(60, 15));
                Assert.AreEqual(LogoVariant.Banner, TestSelectionMenuLayout.SelectLogoVariant(80, 27));
                Assert.AreEqual(LogoVariant.Banner, TestSelectionMenuLayout.SelectLogoVariant(120, 40));
                Assert.AreEqual(LogoVariant.Compact, TestSelectionMenuLayout.SelectLogoVariant(80, 26));
                Assert.AreEqual(LogoVariant.Compact, TestSelectionMenuLayout.SelectLogoVariant(80, 24));
                Assert.AreEqual(LogoVariant.Compact, TestSelectionMenuLayout.SelectLogoVariant(200, 1));
            })
            .Step("The list gets the height minus the five fixed rows, minus seven more with the banner, never negative", context =>
            {
                Assert.AreEqual(28, TestSelectionMenuLayout.MeasureListRowCount(120, 40));
                Assert.AreEqual(19, TestSelectionMenuLayout.MeasureListRowCount(80, 24));
                Assert.AreEqual(10, TestSelectionMenuLayout.MeasureListRowCount(60, 15));
                Assert.AreEqual(15, TestSelectionMenuLayout.MeasureListRowCount(80, 27));
                Assert.AreEqual(35, TestSelectionMenuLayout.MeasureListRowCount(79, 40));
                Assert.AreEqual(0, TestSelectionMenuLayout.MeasureListRowCount(40, 5));
                Assert.AreEqual(0, TestSelectionMenuLayout.MeasureListRowCount(40, 4));
                Assert.AreEqual(0, TestSelectionMenuLayout.MeasureListRowCount(40, 0));
            })
            .Step("At 80 by 27 the banner shows with exactly fifteen list rows", context =>
            {
                var lines = RenderText(NewState(80, 27), 80, 27);

                Assert.HasCount(27, lines);
                Assert.AreEqual(BannerFirstRow, lines[0]);
                Assert.AreEqual(Title, lines[7]);
                Assert.AreEqual(UnfilteredRows[0], lines[9]);
                Assert.AreEqual(StatusForTwelveTests, lines[25]);
            })
            .Run();
    }

    [Test]
    public async Task Verify_frame_degrades_at_tiny_sizes_and_the_size_sweep_never_overflows()
    {
        await Scenario()
            .Step("Very short heights keep the footer on the last row and cut the content above it", context =>
            {
                var lines = RenderText(NewState(40, 3), 40, 3);
                CollectionAssert.AreEqual(new[] { Title, string.Empty, "↑↓ move · enter run · type to filter" }, lines);

                lines = RenderText(NewState(40, 1), 40, 1);
                CollectionAssert.AreEqual(new[] { "↑↓ move · enter run · type to filter" }, lines);
            })
            .Step("A height below 1 leaves the frame unclipped and a width below 1 renders nothing", context =>
            {
                var lines = RenderText(NewState(40, 0), 40, 0);
                CollectionAssert.AreEqual(new[] { Title, string.Empty, string.Empty, StatusForTwelveTests, "↑↓ move · enter run · type to filter" }, lines);

                Assert.IsEmpty(TestSelectionMenuLayout.Render(NewState(0, 24), 0, 24, ColorMode.None));
                Assert.IsEmpty(TestSelectionMenuLayout.Render(NewState(-5, 24), -5, 24, ColorMode.None));
            })
            .Step("Across widths and heights every frame is exactly the height and no line is wider than the width", context =>
            {
                var names = TestNames.Concat(new[] { "Tests." + new string('y', 150) }).ToArray();
                var widths = new[] { 1, 2, 5, 12, 20, 59, 60, 79, 80, 100, 119, 120, 200 };
                var heights = new[] { 1, 2, 3, 5, 10, 15, 24, 26, 27, 40, 80 };
                var frameCount = 0;

                foreach (var width in widths)
                {
                    foreach (var height in heights)
                    {
                        var state = new TestSelectionMenuState(names) { VisibleRowCount = TestSelectionMenuLayout.MeasureListRowCount(width, height) };
                        state.MoveToLast();
                        Type(state, "y");

                        var lines = TestSelectionMenuLayout.Render(state, width, height, ColorMode.None);

                        Assert.HasCount(height, lines, $"Frame {width}x{height}");
                        foreach (var line in lines)
                        {
                            Assert.IsLessThanOrEqualTo(width, line.Width, $"Frame {width}x{height}");
                            Assert.IsLessThanOrEqualTo(line.Width, line.Text.Length, $"Frame {width}x{height}");
                            Assert.DoesNotContain("\n", line.Text);
                            Assert.DoesNotContain("", line.Text);
                        }

                        frameCount++;
                    }
                }

                Assert.AreEqual(widths.Length * heights.Length, frameCount);
            })
            .Step("A null state is rejected", context =>
            {
                Assert.ThrowsExactly<ArgumentNullException>(() => TestSelectionMenuLayout.Render(null!, 80, 24, ColorMode.None));
            })
            .Run();
    }
}
