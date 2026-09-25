using Fuzn.TestFuzn.ConsoleOutput;
using Fuzn.TestFuzn.Contracts.Results.Load;
using Fuzn.TestFuzn.Internals.Terminal;
using Fuzn.TestFuzn.Runner;
using Fuzn.TestFuzn.Tests.Terminal;

namespace Fuzn.TestFuzn.Tests.Runner;

[TestClass]
public class BaseTestFuznRunnerAdapterTests : Test
{
    private const string ReportPath = "C:/results/TestReport.html";
    private const string ExecutionEnvironment = "local";
    private const string TargetEnvironment = "test";

    private static LoadViewHeader Header()
    {
        return new LoadViewHeader { ExecutionEnvironment = ExecutionEnvironment, TargetEnvironment = TargetEnvironment };
    }

    private static FakeLiveViewHost AnsiHost(int windowWidth)
    {
        var host = new FakeLiveViewHost(new TerminalCapabilities(isInteractive: true, supportsAnsi: true, colorMode: ColorMode.TrueColor));
        host.Writer.WindowWidth = windowWidth;
        host.Writer.WindowHeight = 40;
        return host;
    }

    private static FakeLiveViewHost RedirectedHost()
    {
        var host = new FakeLiveViewHost(TerminalCapabilities.Resolve(isOutputRedirected: true, isInputRedirected: true, isVirtualTerminalEnabled: true, term: "xterm-256color", colorTerm: "truecolor", noColor: null));
        host.Writer.WindowWidth = 200;
        return host;
    }

    private static Dictionary<Scenario, ScenarioLoadResult> SmallResults()
    {
        var scenario = new Scenario("Checkout flow");
        var result = SyntheticLoadSnapshots.Snapshot(ok: 3, okPercentile95Ms: 12);
        return new Dictionary<Scenario, ScenarioLoadResult> { { scenario, result } };
    }

    private static List<string> ExpectedWrites(IReadOnlyList<RenderedLine> lines)
    {
        return lines.Select(line => line.Text + Environment.NewLine).ToList();
    }

    [Test]
    public async Task Verify_markup_and_plain_writes()
    {
        await Scenario()
            .Step("On an ANSI terminal a markup line is styled; the size is never read for it", context =>
            {
                var host = AnsiHost(120);
                using var adapter = new FakeTestFuznRunnerAdapter(host);

                adapter.WriteMarkup("[green]OK[/] done");

                var write = Assert.ContainsSingle(host.Writer.Writes);
                Assert.AreEqual("\u001b[38;5;2mOK\u001b[0m done" + Environment.NewLine, write);
                Assert.AreEqual(0, host.Writer.WindowWidthReadCount);
            })
            .Step("On a redirected output the same line is plain with zero escape bytes", context =>
            {
                var host = RedirectedHost();
                using var adapter = new FakeTestFuznRunnerAdapter(host);

                adapter.WriteMarkup("[green]OK[/] done");

                var write = Assert.ContainsSingle(host.Writer.Writes);
                Assert.AreEqual("OK done" + Environment.NewLine, write);
                Assert.DoesNotContain(AnsiCodes.Escape, write);
            })
            .Step("A plain write is formatted with its arguments only when there are any, so braces in a bare message are written as they are", context =>
            {
                var host = RedirectedHost();
                using var adapter = new FakeTestFuznRunnerAdapter(host);

                adapter.Write("Report: {0}", "/tmp/report.html");
                adapter.Write("a {b} c");
                adapter.Write(Environment.NewLine);

                CollectionAssert.AreEqual(
                    new[]
                    {
                        "Report: /tmp/report.html" + Environment.NewLine,
                        "a {b} c" + Environment.NewLine,
                        Environment.NewLine + Environment.NewLine
                    },
                    host.Writer.Writes.ToList());
            })
            .Step("The capabilities are detected once and the writer created once across writes", context =>
            {
                var host = AnsiHost(120);
                using var adapter = new FakeTestFuznRunnerAdapter(host);

                adapter.WriteMarkup("one");
                adapter.Write("two");
                adapter.WritePanel(new[] { "three" }, "Panel");

                Assert.AreEqual(1, host.DetectCapabilitiesCallCount);
                Assert.AreEqual(1, host.CreateTerminalWriterCallCount);
            })
            .Step("A host without a terminal writer fails loud on the first write; a null host is rejected", context =>
            {
                var host = AnsiHost(120);
                host.HasWriter = false;
                using var adapter = new FakeTestFuznRunnerAdapter(host);

                Assert.ThrowsExactly<InvalidOperationException>(() => adapter.WriteMarkup("x"));
                Assert.ThrowsExactly<ArgumentNullException>(() => new FakeTestFuznRunnerAdapter(null!));
            })
            .Step("Disposing twice is safe, and the token can no longer be read once disposed", context =>
            {
                var adapter = new FakeTestFuznRunnerAdapter(RedirectedHost());
                Assert.IsFalse(adapter.CancellationToken.IsCancellationRequested);

                adapter.Dispose();
                adapter.Dispose();

                Assert.ThrowsExactly<ObjectDisposedException>(() => _ = adapter.CancellationToken);
            })
            .Run();
    }

    [Test]
    public async Task Verify_table_data_and_panel_render_in_bordered_boxes_sized_to_their_content()
    {
        await Scenario()
            .Step("A table renders in a box at its natural width, the columns two spaces apart", context =>
            {
                var host = RedirectedHost();
                using var adapter = new FakeTestFuznRunnerAdapter(host);
                var table = new TableData();
                table.Columns.AddRange(new[] { "Name", "Count" });
                table.Rows.Add(new List<string> { "alpha", "10" });
                table.Rows.Add(new List<string> { "b", "5" });

                adapter.WriteTable(table);

                CollectionAssert.AreEqual(
                    new[]
                    {
                        "╭──────────────╮" + Environment.NewLine,
                        "│ Name   Count │" + Environment.NewLine,
                        "│ alpha  10    │" + Environment.NewLine,
                        "│ b      5     │" + Environment.NewLine,
                        "╰──────────────╯" + Environment.NewLine
                    },
                    host.Writer.Writes.ToList());
            })
            .Step("A panel is sized to its widest message with its header in the top border", context =>
            {
                var host = RedirectedHost();
                using var adapter = new FakeTestFuznRunnerAdapter(host);

                adapter.WritePanel(new[] { "hello world", "hi" }, "Info");

                CollectionAssert.AreEqual(
                    new[]
                    {
                        "╭─ Info ──────╮" + Environment.NewLine,
                        "│ hello world │" + Environment.NewLine,
                        "│ hi          │" + Environment.NewLine,
                        "╰─────────────╯" + Environment.NewLine
                    },
                    host.Writer.Writes.ToList());
            })
            .Step("A table wider than the terminal narrows to the terminal's width", context =>
            {
                var host = AnsiHost(50);
                using var adapter = new FakeTestFuznRunnerAdapter(host);
                var table = new TableData();
                table.Columns.Add("Name");
                table.Rows.Add(new List<string> { new string('x', 80) });

                adapter.WriteTable(table);

                Assert.HasCount(4, host.Writer.Writes);
                foreach (var write in host.Writer.Writes)
                    Assert.AreEqual(50 + Environment.NewLine.Length, write.Length);

                Assert.AreEqual(1, host.Writer.WindowWidthReadCount);
            })
            .Step("Null tables and messages are rejected", context =>
            {
                using var adapter = new FakeTestFuznRunnerAdapter(RedirectedHost());

                Assert.ThrowsExactly<ArgumentNullException>(() => adapter.WriteTable(null!));
                Assert.ThrowsExactly<ArgumentNullException>(() => adapter.WritePanel(null!, "header"));
                Assert.ThrowsExactly<ArgumentNullException>(() => adapter.WriteSummary(DateTime.UtcNow, TimeSpan.Zero, null!, ReportPath, ExecutionEnvironment, TargetEnvironment));
            })
            .Run();
    }

    [Test]
    public async Task Verify_summary_and_advanced_table_use_the_terminal_width_only_on_a_wide_enough_ansi_terminal()
    {
        await Scenario()
            .Step("On an ANSI terminal the summary is laid out at the terminal's width, read once for the write", context =>
            {
                var host = AnsiHost(100);
                using var adapter = new FakeTestFuznRunnerAdapter(host);
                var results = SmallResults();

                adapter.WriteSummary(DateTime.UtcNow, TimeSpan.FromSeconds(5), results, ReportPath, ExecutionEnvironment, TargetEnvironment);

                CollectionAssert.AreEqual(ExpectedWrites(LoadSummaryLayout.Render(results, 100, ColorMode.TrueColor, ReportPath, Header())), host.Writer.Writes.ToList());
                Assert.AreEqual(1, host.Writer.WindowWidthReadCount);
                Assert.AreEqual(0, host.Writer.WindowHeightReadCount);
            })
            .Step("On a redirected output the summary is laid out at the default width, plain, the size never read", context =>
            {
                var host = RedirectedHost();
                using var adapter = new FakeTestFuznRunnerAdapter(host);
                var results = SmallResults();

                adapter.WriteSummary(DateTime.UtcNow, TimeSpan.FromSeconds(5), results, ReportPath, ExecutionEnvironment, TargetEnvironment);

                CollectionAssert.AreEqual(ExpectedWrites(LoadSummaryLayout.Render(results, BaseTestFuznRunnerAdapter.DefaultWidth, ColorMode.None, ReportPath, Header())), host.Writer.Writes.ToList());
                Assert.AreEqual(0, host.Writer.WindowWidthReadCount);
                foreach (var write in host.Writer.Writes)
                    Assert.DoesNotContain(AnsiCodes.Escape, write);
            })
            .Step("A terminal narrower than the minimum width gets the default width too, so the widgets never degrade into fragments", context =>
            {
                var host = AnsiHost(BaseTestFuznRunnerAdapter.MinimumWidth - 1);
                using var adapter = new FakeTestFuznRunnerAdapter(host);
                var results = SmallResults();

                adapter.WriteSummary(DateTime.UtcNow, TimeSpan.FromSeconds(5), results, ReportPath, ExecutionEnvironment, TargetEnvironment);

                CollectionAssert.AreEqual(ExpectedWrites(LoadSummaryLayout.Render(results, BaseTestFuznRunnerAdapter.DefaultWidth, ColorMode.TrueColor, ReportPath, Header())), host.Writer.Writes.ToList());
                Assert.AreEqual(1, host.Writer.WindowWidthReadCount);
            })
            .Step("A terminal too narrow to show every number of the summary whole gets the default width as well, never a cut number", context =>
            {
                var host = AnsiHost(40);
                using var adapter = new FakeTestFuznRunnerAdapter(host);
                var results = SmallResults();
                foreach (var result in results.Values)
                {
                    result.Ok.ResponseTimeMin = TimeSpan.FromDays(500);
                    result.Ok.ResponseTimeMean = TimeSpan.FromDays(500);
                }

                Assert.IsGreaterThan(40, LoadSummaryLayout.MeasureMinimumWidth(results));

                adapter.WriteSummary(DateTime.UtcNow, TimeSpan.FromSeconds(5), results, ReportPath, ExecutionEnvironment, TargetEnvironment);

                CollectionAssert.AreEqual(ExpectedWrites(LoadSummaryLayout.Render(results, BaseTestFuznRunnerAdapter.DefaultWidth, ColorMode.TrueColor, ReportPath, Header())), host.Writer.Writes.ToList());
                Assert.AreEqual(1, host.Writer.WindowWidthReadCount);
                foreach (var write in host.Writer.Writes)
                    Assert.DoesNotContain(MarkupText.Ellipsis.ToString(), write);
            })
            .Step("An advanced table is laid out by its layout at the same width", context =>
            {
                var host = AnsiHost(60);
                using var adapter = new FakeTestFuznRunnerAdapter(host);
                var table = new AdvancedTable { ColumnCount = 2 };
                table.Rows.Add(new AdvancedTableRow { Cells = { new AdvancedTableCell("Scenario: Checkout", 1), new KeyValueCell("Passed", "12 ms", 1) } });
                table.Rows.Add(new AdvancedTableRow { IsDivider = true });
                table.Rows.Add(new AdvancedTableRow { Cells = { new AdvancedTableCell(new string('x', 100), 2) } });

                adapter.WriteAdvancedTable(table);

                CollectionAssert.AreEqual(ExpectedWrites(AdvancedTableLayout.Render(table, 60, ColorMode.TrueColor)), host.Writer.Writes.ToList());
                foreach (var write in host.Writer.Writes)
                    Assert.AreEqual(60 + Environment.NewLine.Length, write.Length);
            })
            .Run();
    }
}
