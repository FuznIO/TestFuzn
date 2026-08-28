using Fuzn.TestFuzn.Internals.Terminal;
using Fuzn.TestFuzn.StandaloneRunner;

namespace Fuzn.TestFuzn.Tests.Terminal;

/// <summary>
/// Pins <see cref="TestSelectionMenu"/> as a terminal session over the fakes: on a terminal
/// with live view support the alternate screen is entered once and restored once as the last
/// write on every exit path — a pick, Escape, a cancelled token (the Ctrl+C path), an
/// exception — with the keys applied between repaints and the frames diffed; number entry and
/// the filter through real key presses; the window scrolling and a resize repainting in full.
/// Without live view support the prompt fallback writes the plain numbered list and honours
/// the line semantics — a number picks, an empty line or the end of the input quits, anything
/// else is invalid and prompted again — never reading a key or the terminal size.
/// </summary>
[TestClass]
public class TestSelectionMenuTests : Test
{
    private const string EnterSequence = AnsiCodes.EnterAlternateScreen + AnsiCodes.HideCursor + AnsiCodes.DisableAutoWrap;
    private const string RestoreSequence = AnsiCodes.Reset + AnsiCodes.EnableAutoWrap + AnsiCodes.ShowCursor + AnsiCodes.ExitAlternateScreen;
    private const string Prompt = "Enter the ID of the test you want to run or enter to quit:";

    private static readonly TimeSpan RunTimeout = TimeSpan.FromSeconds(30);

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

    private static IReadOnlyList<DiscoveredTest> Tests()
    {
        return TestNames.Select(name => new DiscoveredTest { Name = name }).ToList();
    }

    /// <summary>A host with live view support and no color, so frames stay plain, at the given size.</summary>
    private static FakeLiveViewHost LiveHost(int width = 120, int height = 40)
    {
        var host = new FakeLiveViewHost(new TerminalCapabilities(isInteractive: true, supportsAnsi: true, colorMode: ColorMode.None));
        host.Writer.WindowWidth = width;
        host.Writer.WindowHeight = height;
        return host;
    }

    /// <summary>A host whose output and input are both redirected.</summary>
    private static FakeLiveViewHost RedirectedHost()
    {
        return new FakeLiveViewHost(TerminalCapabilities.Resolve(isOutputRedirected: true, isInputRedirected: true, isVirtualTerminalEnabled: true, term: "xterm-256color", colorTerm: null, noColor: null));
    }

    private static async Task<DiscoveredTest?> Select(FakeLiveViewHost host, IReadOnlyList<DiscoveredTest> tests, CancellationToken cancellationToken = default)
    {
        return await new TestSelectionMenu(host).SelectTest(tests, cancellationToken).WaitAsync(RunTimeout);
    }

    private static void Type(FakeTerminalReader reader, string text)
    {
        foreach (var character in text)
            reader.Press(character);
    }

    private static string RepaintedLine(int row, string text)
    {
        return AnsiCodes.MoveCursor(row, 1) + AnsiCodes.Reset + AnsiCodes.EraseLine + text;
    }

    private static string SynchronizedFlush(params string[] parts)
    {
        return AnsiCodes.BeginSynchronizedOutput + string.Concat(parts) + AnsiCodes.EndSynchronizedOutput;
    }

    private static void AssertEnteredAndRestoredOnce(FakeTerminalWriter writer)
    {
        Assert.IsNotEmpty(writer.Writes);
        Assert.AreEqual(EnterSequence, writer.Writes[0]);
        Assert.AreEqual(RestoreSequence, writer.Writes[writer.Writes.Count - 1]);
        Assert.AreEqual(1, writer.Writes.Count(write => write == EnterSequence));
        Assert.AreEqual(1, writer.Writes.Count(write => write == RestoreSequence));
    }

    /// <summary>The writes so far as plain lines: each one line, no escape byte, ending in exactly one terminator; returned without it.</summary>
    private static List<string> PlainLines(FakeTerminalWriter writer)
    {
        var lines = new List<string>();
        foreach (var write in writer.Writes)
        {
            Assert.DoesNotContain("", write);
            Assert.EndsWith(Environment.NewLine, write);

            var line = write.Substring(0, write.Length - Environment.NewLine.Length);
            Assert.DoesNotContain("\n", line);
            Assert.DoesNotContain("\r", line);
            lines.Add(line);
        }

        return lines;
    }

    [Test]
    public async Task Verify_arrow_keys_and_enter_pick_the_highlighted_test()
    {
        await Scenario()
            .Step("Keys typed ahead are applied on the first tick: two downs and Enter pick the third test before any frame is painted", async context =>
            {
                var host = LiveHost();
                var tests = Tests();
                host.Reader.Press(ConsoleKey.DownArrow);
                host.Reader.Press(ConsoleKey.DownArrow);
                host.Reader.Press(ConsoleKey.Enter);

                var selected = await Select(host, tests);

                Assert.AreSame(tests[2], selected);
                CollectionAssert.AreEqual(new[] { EnterSequence, RestoreSequence }, host.Writer.Writes.ToList());
                Assert.AreEqual(3, host.Reader.TryReadKeyCallCount);
                Assert.AreEqual(0, host.Reader.ReadLineCallCount);
                Assert.AreEqual(0, host.DelayCount);
                Assert.AreEqual(1, host.Writer.WindowWidthReadCount);
                Assert.AreEqual(1, host.Writer.WindowHeightReadCount);
                Assert.AreEqual(1, host.CreateTerminalReaderCallCount);
                Assert.AreEqual(1, host.CreateTerminalWriterCallCount);
            })
            .Step("Keys pressed between ticks repaint only the rows that changed, and the pick ends the menu without another frame", async context =>
            {
                var host = LiveHost();
                var tests = Tests();
                host.OnDelay = tick =>
                {
                    if (tick == 1)
                        host.Reader.Press(ConsoleKey.DownArrow);
                    else if (tick == 2)
                        host.Reader.Press(ConsoleKey.Enter);
                };

                var selected = await Select(host, tests);

                Assert.AreSame(tests[1], selected);
                AssertEnteredAndRestoredOnce(host.Writer);
                Assert.HasCount(4, host.Writer.Writes);
                Assert.AreEqual(2, host.DelayCount);
                Assert.AreEqual(TestSelectionMenu.PollInterval, host.DelayIntervals[0]);

                // The first frame is a full redraw at 120x40: the banner on row 1, the title on
                // row 8, the list from row 10, the footer on row 40.
                var fullRedraw = host.Writer.Writes[1];
                Assert.StartsWith(AnsiCodes.BeginSynchronizedOutput + AnsiCodes.DisableAutoWrap + AnsiCodes.Reset + AnsiCodes.EraseScreen + AnsiCodes.MoveCursor(1, 1) + "████████╗", fullRedraw);
                Assert.Contains(AnsiCodes.MoveCursor(8, 1) + "Select a test to run" + AnsiCodes.MoveCursor(9, 1) + AnsiCodes.MoveCursor(10, 1) + "▸  1  Fuzn.Shop.Tests.CartTests.Verify_add_to_cart" + AnsiCodes.MoveCursor(11, 1) + "   2  Fuzn.Shop.Tests.CatalogTests.Verify_browse_products", fullRedraw);
                Assert.Contains(AnsiCodes.MoveCursor(39, 1) + "Filter: ▏  12 tests", fullRedraw);
                Assert.EndsWith(AnsiCodes.MoveCursor(40, 1) + "↑↓ move · enter run · type to filter · esc quit · pgup/pgdn page · home/end first/last" + AnsiCodes.EndSynchronizedOutput, fullRedraw);

                // The down key moved the highlight one row: exactly those two rows repaint.
                Assert.AreEqual(
                    SynchronizedFlush(
                        RepaintedLine(10, "   1  Fuzn.Shop.Tests.CartTests.Verify_add_to_cart"),
                        RepaintedLine(11, "▸  2  Fuzn.Shop.Tests.CatalogTests.Verify_browse_products")),
                    host.Writer.Writes[2]);
            })
            .Run();
    }

    [Test]
    public async Task Verify_escape_quits_and_enter_on_no_match_keeps_the_menu_open()
    {
        await Scenario()
            .Step("Escape returns null with the terminal restored once, after the frame that was showing", async context =>
            {
                var host = LiveHost();
                host.OnDelay = tick => host.Reader.Press(ConsoleKey.Escape);

                var selected = await Select(host, Tests());

                Assert.IsNull(selected);
                AssertEnteredAndRestoredOnce(host.Writer);
                Assert.HasCount(3, host.Writer.Writes);
                Assert.AreEqual(1, host.DelayCount);
            })
            .Step("Enter on a filter that matches nothing is a no-op: the menu repaints the empty list and Escape then quits", async context =>
            {
                var host = LiveHost(80, 24);
                host.OnDelay = tick =>
                {
                    if (tick == 1)
                    {
                        Type(host.Reader, "zzz");
                        host.Reader.Press(ConsoleKey.Enter);
                    }
                    else if (tick == 2)
                    {
                        host.Reader.Press(ConsoleKey.Escape);
                    }
                };

                var selected = await Select(host, Tests());

                Assert.IsNull(selected);
                AssertEnteredAndRestoredOnce(host.Writer);
                Assert.HasCount(4, host.Writer.Writes);
                Assert.AreEqual(2, host.DelayCount);

                // The second frame's diff: the twelve list rows erased and the status line changed.
                var diff = host.Writer.Writes[2];
                Assert.StartsWith(AnsiCodes.BeginSynchronizedOutput + RepaintedLine(3, string.Empty) + RepaintedLine(4, string.Empty), diff);
                Assert.EndsWith(RepaintedLine(23, "Filter: zzz▏  no matching tests") + AnsiCodes.EndSynchronizedOutput, diff);
            })
            .Run();
    }

    [Test]
    public async Task Verify_cancellation_ends_the_menu_with_null_and_the_terminal_restored()
    {
        await Scenario()
            .Step("A token cancelled while the menu waits — the Ctrl+C path — ends it on that tick with null", async context =>
            {
                var host = LiveHost();
                using var cancellation = new CancellationTokenSource();
                host.OnDelay = tick =>
                {
                    if (tick == 2)
                        cancellation.Cancel();
                };

                var selected = await Select(host, Tests(), cancellation.Token);

                Assert.IsNull(selected);
                AssertEnteredAndRestoredOnce(host.Writer);
                // Enter, the first frame, (an unchanged second frame writes nothing), restore.
                Assert.HasCount(3, host.Writer.Writes);
                Assert.AreEqual(2, host.DelayCount);
            })
            .Step("A token already cancelled returns null before anything is written or read", async context =>
            {
                var host = LiveHost();
                using var cancellation = new CancellationTokenSource();
                cancellation.Cancel();

                var selected = await Select(host, Tests(), cancellation.Token);

                Assert.IsNull(selected);
                Assert.IsEmpty(host.Writer.Writes);
                Assert.AreEqual(0, host.Writer.WindowWidthReadCount);
                Assert.AreEqual(0, host.Reader.TryReadKeyCallCount);
                Assert.AreEqual(0, host.DelayCount);
            })
            .Run();
    }

    [Test]
    public async Task Verify_terminal_is_restored_once_on_an_exception()
    {
        await Scenario()
            .Step("A failing size read propagates with the alternate screen left as the last write", async context =>
            {
                var host = LiveHost();
                host.Writer.WindowSizeReadFailure = new IOException("The handle is invalid.");

                await Assert.ThrowsExactlyAsync<IOException>(async () => await Select(host, Tests()));

                CollectionAssert.AreEqual(new[] { EnterSequence, RestoreSequence }, host.Writer.Writes.ToList());
            })
            .Step("A failing key read propagates the same way", async context =>
            {
                var host = LiveHost();
                host.Reader.ReadFailure = new InvalidOperationException("Cannot read keys from a redirected input.");

                await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () => await Select(host, Tests()));

                CollectionAssert.AreEqual(new[] { EnterSequence, RestoreSequence }, host.Writer.Writes.ToList());
            })
            .Step("A host without a reader fails before the alternate screen, on both paths", async context =>
            {
                var live = LiveHost();
                live.HasReader = false;
                await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () => await Select(live, Tests()));
                Assert.IsEmpty(live.Writer.Writes);

                var redirected = RedirectedHost();
                redirected.HasReader = false;
                await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () => await Select(redirected, Tests()));
                Assert.IsEmpty(redirected.Writer.Writes);
            })
            .Step("A host without a writer fails before anything is read, on both paths", async context =>
            {
                var live = LiveHost();
                live.HasWriter = false;
                live.Reader.Press(ConsoleKey.Enter);
                await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () => await Select(live, Tests()));
                Assert.AreEqual(0, live.Reader.TryReadKeyCallCount);
                Assert.AreEqual(1, live.CreateTerminalWriterCallCount);

                var redirected = RedirectedHost();
                redirected.HasWriter = false;
                redirected.Reader.TypeLine("1");
                await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () => await Select(redirected, Tests()));
                Assert.AreEqual(0, redirected.Reader.ReadLineCallCount);
                Assert.AreEqual(1, redirected.CreateTerminalWriterCallCount);
            })
            .Run();
    }

    [Test]
    public async Task Verify_a_failing_writer_reports_the_menu_failure_with_the_restore_still_attempted()
    {
        await Scenario()
            .Step("A writer that breaks on the first frame: the caller sees the frame's failure, the restore was still attempted and its own failure discarded", async context =>
            {
                var host = LiveHost();
                var frameFailure = new IOException("Broken pipe on the frame.");
                var restoreFailure = new IOException("Broken pipe on the restore.");
                // Every write after the successful enter sequence fails: the frame with the
                // frame's failure, and the write after that — the restore — with its own.
                host.Writer.WriteObserver = write => host.Writer.WriteFailure = frameFailure;
                host.Writer.FailedWriteObserver = failedWriteCount => host.Writer.WriteFailure = restoreFailure;

                var thrown = await Assert.ThrowsExactlyAsync<IOException>(async () => await Select(host, Tests()));

                Assert.AreSame(frameFailure, thrown);
                Assert.AreEqual(2, host.Writer.FailedWriteCount);
                CollectionAssert.AreEqual(new[] { EnterSequence }, host.Writer.Writes.ToList());
                Assert.AreEqual(0, host.DelayCount);
            })
            .Step("When the menu itself did not fail, a failing restore is the failure reported", async context =>
            {
                var host = LiveHost();
                var restoreFailure = new IOException("Broken pipe on the restore.");
                host.OnDelay = tick =>
                {
                    host.Reader.Press(ConsoleKey.Escape);
                    host.Writer.WriteFailure = restoreFailure;
                };

                var thrown = await Assert.ThrowsExactlyAsync<IOException>(async () => await Select(host, Tests()));

                Assert.AreSame(restoreFailure, thrown);
                Assert.AreEqual(1, host.Writer.FailedWriteCount);
                Assert.HasCount(2, host.Writer.Writes);
                Assert.AreEqual(EnterSequence, host.Writer.Writes[0]);
            })
            .Run();
    }

    [Test]
    public async Task Verify_number_entry_and_filter_through_key_presses()
    {
        await Scenario()
            .Step("Typing 1, 2 and Enter picks the twelfth test", async context =>
            {
                var host = LiveHost();
                var tests = Tests();
                Type(host.Reader, "12");
                host.Reader.Press(ConsoleKey.Enter);

                Assert.AreSame(tests[11], await Select(host, tests));
                AssertEnteredAndRestoredOnce(host.Writer);
            })
            .Step("Typing a filter, moving down within its matches and Enter picks the second match", async context =>
            {
                var host = LiveHost();
                var tests = Tests();
                Type(host.Reader, "search");
                host.Reader.Press(ConsoleKey.DownArrow);
                host.Reader.Press(ConsoleKey.Enter);

                Assert.AreSame(tests[11], await Select(host, tests));
            })
            .Step("A filter with one match and Enter picks it, after Backspace corrected a typo", async context =>
            {
                var host = LiveHost();
                var tests = Tests();
                Type(host.Reader, "browsx");
                host.Reader.Press(ConsoleKey.Backspace);
                host.Reader.Press('e');
                host.Reader.Press(ConsoleKey.Enter);

                Assert.AreSame(tests[1], await Select(host, tests));
            })
            .Step("A number entry past the count is a text search and Enter on it selects nothing", async context =>
            {
                var host = LiveHost();
                host.OnDelay = tick =>
                {
                    if (tick == 1)
                    {
                        Type(host.Reader, "13");
                        host.Reader.Press(ConsoleKey.Enter);
                    }
                    else
                    {
                        host.Reader.Press(ConsoleKey.Escape);
                    }
                };

                Assert.IsNull(await Select(host, Tests()));
                Assert.AreEqual(2, host.DelayCount);
            })
            .Run();
    }

    [Test]
    public async Task Verify_window_scrolls_and_a_resize_repaints_in_full()
    {
        await Scenario()
            .Step("At 60 by 15 End scrolls the ten-row window to the last test and repaints every list row", async context =>
            {
                var host = LiveHost(60, 15);
                host.OnDelay = tick =>
                {
                    if (tick == 1)
                        host.Reader.Press(ConsoleKey.End);
                    else
                        host.Reader.Press(ConsoleKey.Escape);
                };

                Assert.IsNull(await Select(host, Tests()));
                Assert.HasCount(4, host.Writer.Writes);

                var fullRedraw = host.Writer.Writes[1];
                Assert.Contains(AnsiCodes.MoveCursor(3, 1) + "▸  1  Fuzn.Shop.Tests.CartTests.Verify_add_to_cart", fullRedraw);
                Assert.Contains(AnsiCodes.MoveCursor(12, 1) + "  10  Fuzn.Shop.Tests.ProductTests.Verify_product_details", fullRedraw);

                var diff = host.Writer.Writes[2];
                Assert.StartsWith(AnsiCodes.BeginSynchronizedOutput + RepaintedLine(3, "   3  Fuzn.Shop.Tests.CatalogTests.Verify_search_products"), diff);
                Assert.EndsWith(RepaintedLine(12, "▸ 12  Fuzn.Shop.Tests.SearchLoadTests.Verify_search_under_l…") + AnsiCodes.EndSynchronizedOutput, diff);
            })
            .Step("A terminal resized between ticks gets a full redraw laid out at the new size", async context =>
            {
                var host = LiveHost(60, 15);
                host.OnDelay = tick =>
                {
                    if (tick == 1)
                    {
                        host.Writer.WindowWidth = 120;
                        host.Writer.WindowHeight = 40;
                    }
                    else
                    {
                        host.Reader.Press(ConsoleKey.Escape);
                    }
                };

                Assert.IsNull(await Select(host, Tests()));
                Assert.HasCount(4, host.Writer.Writes);

                var redraw = host.Writer.Writes[2];
                Assert.StartsWith(AnsiCodes.BeginSynchronizedOutput + AnsiCodes.DisableAutoWrap + AnsiCodes.Reset + AnsiCodes.EraseScreen + AnsiCodes.MoveCursor(1, 1) + "████████╗", redraw);
                Assert.EndsWith(AnsiCodes.MoveCursor(40, 1) + "↑↓ move · enter run · type to filter · esc quit · pgup/pgdn page · home/end first/last" + AnsiCodes.EndSynchronizedOutput, redraw);
                // One size reading per tick: the first frame, the resized frame, the Escape tick.
                Assert.AreEqual(3, host.Writer.WindowWidthReadCount);
                Assert.AreEqual(3, host.Writer.WindowHeightReadCount);
            })
            .Run();
    }

    [Test]
    public async Task Verify_prompt_fallback_on_a_redirected_terminal()
    {
        await Scenario()
            .Step("The title, the numbered list and the prompt are plain lines; a typed number picks that test; no key or size is ever read", async context =>
            {
                var host = RedirectedHost();
                var tests = Tests();
                host.Reader.TypeLine("2");

                var selected = await Select(host, tests);

                Assert.AreSame(tests[1], selected);
                var lines = PlainLines(host.Writer);
                Assert.HasCount(16, lines);
                Assert.AreEqual("TestFuzn Test Runner", lines[0]);
                Assert.AreEqual("ID  Test Name", lines[1]);
                Assert.AreEqual(" 1  Fuzn.Shop.Tests.CartTests.Verify_add_to_cart", lines[2]);
                Assert.AreEqual(" 9  Fuzn.Shop.Tests.OrderTests.Verify_order_tracking", lines[10]);
                Assert.AreEqual("12  Fuzn.Shop.Tests.SearchLoadTests.Verify_search_under_load", lines[13]);
                Assert.AreEqual(string.Empty, lines[14]);
                Assert.AreEqual(Prompt, lines[15]);

                Assert.AreEqual(1, host.Reader.ReadLineCallCount);
                Assert.AreEqual(0, host.Reader.TryReadKeyCallCount);
                Assert.AreEqual(0, host.Writer.WindowWidthReadCount);
                Assert.AreEqual(0, host.Writer.WindowHeightReadCount);
                Assert.AreEqual(0, host.DelayCount);
                Assert.AreEqual(1, host.CreateTerminalReaderCallCount);
            })
            .Step("An empty line quits with null, and so does the end of the input", async context =>
            {
                var emptyLine = RedirectedHost();
                emptyLine.Reader.TypeLine(string.Empty);
                Assert.IsNull(await Select(emptyLine, Tests()));
                Assert.AreEqual(1, emptyLine.Reader.ReadLineCallCount);
                Assert.AreEqual(Prompt, PlainLines(emptyLine.Writer)[15]);

                var endOfInput = RedirectedHost();
                Assert.IsNull(await Select(endOfInput, Tests()));
                Assert.AreEqual(1, endOfInput.Reader.ReadLineCallCount);
                Assert.HasCount(16, PlainLines(endOfInput.Writer));
            })
            .Step("Anything that is not a listed number is invalid and prompted again until a number or an empty line", async context =>
            {
                var host = RedirectedHost();
                var tests = Tests();
                host.Reader.TypeLine("abc");
                host.Reader.TypeLine("99");
                host.Reader.TypeLine("0");
                host.Reader.TypeLine(" 1 ");

                var selected = await Select(host, tests);

                Assert.AreSame(tests[0], selected);
                Assert.AreEqual(4, host.Reader.ReadLineCallCount);
                var lines = PlainLines(host.Writer);
                CollectionAssert.AreEqual(
                    new[] { "Invalid test index.", Prompt, "Invalid test index.", Prompt, "Invalid test index.", Prompt },
                    lines.Skip(16).ToList());
            })
            .Step("Brackets in a name render literally on the list", async context =>
            {
                var host = RedirectedHost();
                var tests = new[] { new DiscoveredTest { Name = "Tests.Weird[bold]name[/]" } };

                Assert.IsNull(await Select(host, tests));
                // The ID column is as wide as its header.
                Assert.AreEqual(" 1  Tests.Weird[bold]name[/]", PlainLines(host.Writer)[2]);
            })
            .Run();
    }

    [Test]
    public async Task Verify_prompt_fallback_ends_on_cancellation_while_a_line_read_is_pending()
    {
        await Scenario()
            .Step("A token cancelled while the line read blocks — Ctrl+C at the prompt — ends the fallback with null at once, the read left pending on its worker", async context =>
            {
                var host = RedirectedHost();
                var gate = new ManualResetEventSlim(false);
                host.Reader.ReadLineGate = gate;
                using var cancellation = new CancellationTokenSource();

                var selection = new TestSelectionMenu(host).SelectTest(Tests(), cancellation.Token);

                // The read is pending on its worker: called once, blocked on the gate, the
                // selection still open.
                Assert.IsTrue(SpinWait.SpinUntil(() => host.Reader.ReadLineCallCount == 1, RunTimeout));
                Assert.IsFalse(selection.IsCompleted);
                Assert.AreEqual(Prompt + Environment.NewLine, host.Writer.Writes[host.Writer.Writes.Count - 1]);

                cancellation.Cancel();

                Assert.IsNull(await selection.WaitAsync(RunTimeout));
                Assert.AreEqual(1, host.Reader.ReadLineCallCount);
                Assert.AreEqual(0, host.Reader.TryReadKeyCallCount);
                Assert.AreEqual(0, host.Writer.WindowWidthReadCount);
                Assert.HasCount(16, PlainLines(host.Writer));

                // Release the stranded read; the console read it stands for returns at process exit.
                gate.Set();
            })
            .Step("A token already cancelled returns null before anything is written or read", async context =>
            {
                var host = RedirectedHost();
                host.Reader.TypeLine("1");
                using var cancellation = new CancellationTokenSource();
                cancellation.Cancel();

                Assert.IsNull(await Select(host, Tests(), cancellation.Token));
                Assert.IsEmpty(host.Writer.Writes);
                Assert.AreEqual(0, host.Reader.ReadLineCallCount);
            })
            .Step("A line read that fails propagates as is; the prompt is never continued over a broken input", async context =>
            {
                var host = RedirectedHost();
                host.Reader.ReadFailure = new IOException("Input closed.");

                await Assert.ThrowsExactlyAsync<IOException>(async () => await Select(host, Tests()));
                Assert.AreEqual(1, host.Reader.ReadLineCallCount);
                Assert.HasCount(16, PlainLines(host.Writer));
            })
            .Run();
    }

    [Test]
    public async Task Verify_prompt_fallback_on_other_terminals_without_live_view_support()
    {
        await Scenario()
            .Step("A dumb terminal gets the plain fallback even though both input and output are interactive", async context =>
            {
                var host = new FakeLiveViewHost(TerminalCapabilities.Resolve(isOutputRedirected: false, isInputRedirected: false, isVirtualTerminalEnabled: true, term: "dumb", colorTerm: null, noColor: null));
                Assert.IsTrue(host.Capabilities.IsInteractive);
                Assert.IsFalse(host.Capabilities.SupportsLiveView);
                var tests = Tests();
                host.Reader.TypeLine("3");

                Assert.AreSame(tests[2], await Select(host, tests));
                Assert.AreEqual("TestFuzn Test Runner", PlainLines(host.Writer)[0]);
                Assert.AreEqual(0, host.Reader.TryReadKeyCallCount);
                Assert.AreEqual(0, host.Writer.WindowWidthReadCount);
            })
            .Step("An ANSI terminal whose input is redirected gets the fallback with its title and prompt styled", async context =>
            {
                var host = new FakeLiveViewHost(TerminalCapabilities.Resolve(isOutputRedirected: false, isInputRedirected: true, isVirtualTerminalEnabled: true, term: "xterm-256color", colorTerm: null, noColor: null));
                Assert.IsFalse(host.Capabilities.SupportsLiveView);
                Assert.AreEqual(ColorMode.Colors16, host.Capabilities.ColorMode);

                Assert.IsNull(await Select(host, Tests()));

                Assert.AreEqual("[1;32mTestFuzn Test Runner[0m" + Environment.NewLine, host.Writer.Writes[0]);
                Assert.AreEqual("ID  Test Name" + Environment.NewLine, host.Writer.Writes[1]);
                Assert.AreEqual("[1;32m" + Prompt + "[0m" + Environment.NewLine, host.Writer.Writes[15]);
                Assert.AreEqual(0, host.Reader.TryReadKeyCallCount);
                Assert.AreEqual(0, host.Writer.WindowWidthReadCount);
            })
            .Run();
    }

    [Test]
    public async Task Verify_construction_guards_and_cadence()
    {
        await Scenario()
            .Step("Null arguments are rejected", async context =>
            {
                Assert.ThrowsExactly<ArgumentNullException>(() => new TestSelectionMenu(null!));

                var menu = new TestSelectionMenu(LiveHost());
                await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await menu.SelectTest(null!, CancellationToken.None));
                await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await menu.SelectTest(new DiscoveredTest[] { null! }, CancellationToken.None));
            })
            .Step("The key poll cadence is twenty times per second", context =>
            {
                Assert.AreEqual(TimeSpan.FromMilliseconds(50), TestSelectionMenu.PollInterval);
            })
            .Run();
    }
}
