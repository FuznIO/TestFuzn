using Fuzn.TestFuzn.Internals.Execution;
using Fuzn.TestFuzn.Internals.Terminal;

namespace Fuzn.TestFuzn.Tests.Terminal;

/// <summary>
/// Pins the live dashboard's terminal lifecycle and render wiring against a fake writer: the
/// sequences that enter and leave the alternate screen, that the terminal is restored exactly
/// once on every exit path (completion, an exception, cancellation), that each render reads the
/// writer's size once and paints the layout at that size through the diff renderer, and that
/// the spinner advances one glyph per render — and holds while the passed view state is
/// paused, so a paused render of an unchanged state writes nothing.
/// </summary>
[TestClass]
public class LiveDashboardTests : Test
{
    private const string EnterSequence = AnsiCodes.EnterAlternateScreen + AnsiCodes.HideCursor + AnsiCodes.DisableAutoWrap;
    private const string RestoreSequence = AnsiCodes.Reset + AnsiCodes.EnableAutoWrap + AnsiCodes.ShowCursor + AnsiCodes.ExitAlternateScreen;

    private static readonly string[] BrailleSpinnerFrames = { "⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏" };
    private static readonly string[] AsciiSpinnerFrames = { "|", "/", "-", "\\" };

    /// <summary>
    /// Interactive and ANSI-capable so the live view is supported, with <see cref="ColorMode.None"/>
    /// so the frame body carries no SGR and the goldens stay readable; the dashboard's own
    /// screen-mode sequences do not depend on the color mode. (Resolve never produces this
    /// combination — the constructor allows it as a test convenience.)
    /// </summary>
    private static TerminalCapabilities LiveCapabilities()
    {
        return new TerminalCapabilities(isInteractive: true, supportsAnsi: true, colorMode: ColorMode.None);
    }

    private static LiveMetricsSnapshot WarmupSnapshot(int elapsedSeconds)
    {
        return new LiveMetricsSnapshot
        {
            ScenarioName = "Browse catalog",
            Phase = LoadTestPhase.Warmup,
            PhaseLabel = "warmup: Fixed 50 rps",
            Duration = TimeSpan.FromSeconds(elapsedSeconds)
        };
    }

    private static string TitleLine(string spinnerGlyph)
    {
        return "Browse catalog  " + spinnerGlyph + " Running";
    }

    [Test]
    public async Task Verify_start_enters_the_alternate_screen()
    {
        await Scenario()
            .Step("Start enters the alternate screen, hides the cursor and disables auto-wrap in one write", context =>
            {
                var writer = new FakeTerminalWriter();
                using var dashboard = new LiveDashboard(writer, LiveCapabilities(), () => new[] { WarmupSnapshot(42) });

                dashboard.Start();

                var write = Assert.ContainsSingle(writer.Writes);
                Assert.AreEqual(EnterSequence, write);
            })
            .Step("Starting twice throws", context =>
            {
                var writer = new FakeTerminalWriter();
                using var dashboard = new LiveDashboard(writer, LiveCapabilities(), () => new[] { WarmupSnapshot(42) });
                dashboard.Start();

                Assert.ThrowsExactly<InvalidOperationException>(() => dashboard.Start());
            })
            .Step("Rendering before start throws and writes nothing", context =>
            {
                var writer = new FakeTerminalWriter();
                using var dashboard = new LiveDashboard(writer, LiveCapabilities(), () => new[] { WarmupSnapshot(42) });

                Assert.ThrowsExactly<InvalidOperationException>(() => dashboard.Render(LiveDashboardViewState.Default));
                Assert.IsEmpty(writer.Writes);
            })
            .Run();
    }

    [Test]
    public async Task Verify_render_paints_the_layout_at_the_writer_size()
    {
        await Scenario()
            .Step("The first render is a full redraw of the layout at the writer's width and height with the first spinner glyph", context =>
            {
                var writer = new FakeTerminalWriter { WindowWidth = 40, WindowHeight = 6 };
                using var dashboard = new LiveDashboard(writer, LiveCapabilities(), () => new[] { WarmupSnapshot(42) });
                dashboard.Start();
                writer.ClearWrites();

                dashboard.Render(LiveDashboardViewState.Default);

                var flush = Assert.ContainsSingle(writer.Writes);
                var expectedLines = LiveDashboardLayout.Render(new[] { WarmupSnapshot(42) }, LiveDashboardViewState.Default, 40, 6, ColorMode.None, SparklineGlyphSet.Braille, "⠋");
                Assert.HasCount(6, expectedLines);
                Assert.AreEqual(FullRedraw(expectedLines), flush);
                // Hand-derived anchors, so the golden is not only the layout compared to itself:
                // below 64 columns the tiles wrap, and the first row's three boxes share 38
                // columns as 13, 13 and 12.
                Assert.Contains(AnsiCodes.MoveCursor(1, 1) + TitleLine("⠋") + AnsiCodes.MoveCursor(2, 1) + "╭───────────╮ ╭───────────╮ ╭──────────╮", flush);
                Assert.EndsWith(AnsiCodes.MoveCursor(6, 1) + "1 overview · 2 step · 3 errors · q quit" + AnsiCodes.EndSynchronizedOutput, flush);
            })
            .Step("An unchanged data state repaints only the spinner on the title row", context =>
            {
                var writer = new FakeTerminalWriter { WindowWidth = 40, WindowHeight = 6 };
                using var dashboard = new LiveDashboard(writer, LiveCapabilities(), () => new[] { WarmupSnapshot(42) });
                dashboard.Start();
                dashboard.Render(LiveDashboardViewState.Default);
                writer.ClearWrites();

                dashboard.Render(LiveDashboardViewState.Default);

                var flush = Assert.ContainsSingle(writer.Writes);
                Assert.AreEqual(SynchronizedFlush(RepaintedLine(1, TitleLine("⠙"))), flush);
            })
            .Step("A resized terminal gets a full redraw laid out at the new size", context =>
            {
                var writer = new FakeTerminalWriter { WindowWidth = 40, WindowHeight = 6 };
                using var dashboard = new LiveDashboard(writer, LiveCapabilities(), () => new[] { WarmupSnapshot(42) });
                dashboard.Start();
                dashboard.Render(LiveDashboardViewState.Default);
                writer.ClearWrites();

                writer.WindowWidth = 50;
                writer.WindowHeight = 7;
                dashboard.Render(LiveDashboardViewState.Default);

                var flush = Assert.ContainsSingle(writer.Writes);
                var expectedLines = LiveDashboardLayout.Render(new[] { WarmupSnapshot(42) }, LiveDashboardViewState.Default, 50, 7, ColorMode.None, SparklineGlyphSet.Braille, "⠙");
                Assert.HasCount(7, expectedLines);
                Assert.AreEqual(FullRedraw(expectedLines), flush);
                Assert.EndsWith(AnsiCodes.MoveCursor(7, 1) + "1 overview · 2 step · 3 errors · q quit" + AnsiCodes.EndSynchronizedOutput, flush);
            })
            .Step("Each render reads the width and the height exactly once, and Start reads neither", context =>
            {
                var writer = new FakeTerminalWriter { WindowWidth = 40, WindowHeight = 6 };
                using var dashboard = new LiveDashboard(writer, LiveCapabilities(), () => new[] { WarmupSnapshot(42) });

                dashboard.Start();
                Assert.AreEqual(0, writer.WindowWidthReadCount);
                Assert.AreEqual(0, writer.WindowHeightReadCount);

                dashboard.Render(LiveDashboardViewState.Default);
                Assert.AreEqual(1, writer.WindowWidthReadCount);
                Assert.AreEqual(1, writer.WindowHeightReadCount);

                dashboard.Render(LiveDashboardViewState.Default);
                dashboard.Render(LiveDashboardViewState.Default);
                Assert.AreEqual(3, writer.WindowWidthReadCount);
                Assert.AreEqual(3, writer.WindowHeightReadCount);
            })
            .Step("A failing size read propagates out of Render before anything is written", context =>
            {
                var writer = new FakeTerminalWriter { WindowWidth = 40, WindowHeight = 6 };
                using var dashboard = new LiveDashboard(writer, LiveCapabilities(), () => new[] { WarmupSnapshot(42) });
                dashboard.Start();
                writer.ClearWrites();
                writer.WindowSizeReadFailure = new IOException("The handle is invalid.");

                Assert.ThrowsExactly<IOException>(() => dashboard.Render(LiveDashboardViewState.Default));
                Assert.IsEmpty(writer.Writes);
            })
            .Run();
    }

    [Test]
    public async Task Verify_snapshots_provider_is_consulted_on_every_render()
    {
        await Scenario()
            .Step("A snapshot published between renders is painted by the next render", context =>
            {
                // Twelve rows show both tile rows: the elapsed clock is the value line of the
                // second row's second box (20 and 19 columns wide), on terminal row 8.
                var writer = new FakeTerminalWriter { WindowWidth = 40, WindowHeight = 12 };
                var snapshots = new[] { WarmupSnapshot(42) };
                using var dashboard = new LiveDashboard(writer, LiveCapabilities(), () => snapshots);
                dashboard.Start();
                dashboard.Render(LiveDashboardViewState.Default);
                writer.ClearWrites();

                snapshots[0] = WarmupSnapshot(43);
                dashboard.Render(LiveDashboardViewState.Default);

                var flush = Assert.ContainsSingle(writer.Writes);
                Assert.AreEqual(
                    SynchronizedFlush(
                        RepaintedLine(1, TitleLine("⠙")),
                        RepaintedLine(8, "│ 0" + new string(' ', 15) + " │ │ 00:00:43" + new string(' ', 7) + " │")),
                    flush);
            })
            .Step("A provider returning null fails loud instead of rendering a blank frame", context =>
            {
                var writer = new FakeTerminalWriter { WindowWidth = 40, WindowHeight = 6 };
                using var dashboard = new LiveDashboard(writer, LiveCapabilities(), () => null!);
                dashboard.Start();
                writer.ClearWrites();

                Assert.ThrowsExactly<InvalidOperationException>(() => dashboard.Render(LiveDashboardViewState.Default));
                Assert.IsEmpty(writer.Writes);
            })
            .Run();
    }

    [Test]
    public async Task Verify_spinner_cycles_through_its_frames()
    {
        await Scenario()
            .Step("The braille spinner advances one glyph per render and wraps after the last", context =>
            {
                var writer = new FakeTerminalWriter { WindowWidth = 40, WindowHeight = 6 };
                using var dashboard = new LiveDashboard(writer, LiveCapabilities(), () => new[] { WarmupSnapshot(42) });
                dashboard.Start();
                dashboard.Render(LiveDashboardViewState.Default);
                writer.ClearWrites();

                for (var render = 0; render < BrailleSpinnerFrames.Length; render++)
                    dashboard.Render(LiveDashboardViewState.Default);

                Assert.HasCount(BrailleSpinnerFrames.Length, writer.Writes);
                for (var index = 1; index < BrailleSpinnerFrames.Length; index++)
                    Assert.AreEqual(SynchronizedFlush(RepaintedLine(1, TitleLine(BrailleSpinnerFrames[index]))), writer.Writes[index - 1]);

                Assert.AreEqual(SynchronizedFlush(RepaintedLine(1, TitleLine(BrailleSpinnerFrames[0]))), writer.Writes[BrailleSpinnerFrames.Length - 1]);
            })
            .Step("The blocks glyph set uses the ASCII spinner", context =>
            {
                var writer = new FakeTerminalWriter { WindowWidth = 40, WindowHeight = 6 };
                using var dashboard = new LiveDashboard(writer, LiveCapabilities(), () => new[] { WarmupSnapshot(42) }, SparklineGlyphSet.Blocks);
                dashboard.Start();
                writer.ClearWrites();

                dashboard.Render(LiveDashboardViewState.Default);
                Assert.Contains(AnsiCodes.MoveCursor(1, 1) + TitleLine(AsciiSpinnerFrames[0]), Assert.ContainsSingle(writer.Writes));
                writer.ClearWrites();

                for (var render = 0; render < AsciiSpinnerFrames.Length; render++)
                    dashboard.Render(LiveDashboardViewState.Default);

                Assert.HasCount(AsciiSpinnerFrames.Length, writer.Writes);
                for (var index = 1; index < AsciiSpinnerFrames.Length; index++)
                    Assert.AreEqual(SynchronizedFlush(RepaintedLine(1, TitleLine(AsciiSpinnerFrames[index]))), writer.Writes[index - 1]);

                Assert.AreEqual(SynchronizedFlush(RepaintedLine(1, TitleLine(AsciiSpinnerFrames[0]))), writer.Writes[AsciiSpinnerFrames.Length - 1]);
            })
            .Run();
    }

    [Test]
    public async Task Verify_view_state_reaches_the_frame_and_a_pause_holds_the_spinner()
    {
        await Scenario()
            .Step("The view state's view is laid out: the error log's header, its no-errors notice and its hints replace the overview's tiles and footer, row by row", context =>
            {
                // At 40 columns the log's footer is "Esc back · ↑↓ scroll · q quit", 29
                // columns — its next hint would make it 42. The tiles' four rows become the
                // log's header, its notice and two blank rows.
                var writer = new FakeTerminalWriter { WindowWidth = 40, WindowHeight = 6 };
                using var dashboard = new LiveDashboard(writer, LiveCapabilities(), () => new[] { WarmupSnapshot(42) });
                dashboard.Start();
                dashboard.Render(LiveDashboardViewState.Default);
                writer.ClearWrites();

                dashboard.Render(LiveDashboardViewState.Default with { View = LiveDashboardView.ErrorLog });

                var flush = Assert.ContainsSingle(writer.Writes);
                Assert.AreEqual(
                    SynchronizedFlush(
                        RepaintedLine(1, TitleLine("⠙")),
                        RepaintedLine(2, "errors · 0 of 0 · Esc back"),
                        RepaintedLine(3, LiveDashboardLayout.NoErrorsNoticeText),
                        RepaintedLine(4, string.Empty),
                        RepaintedLine(5, string.Empty),
                        RepaintedLine(6, "Esc back · ↑↓ scroll · q quit")),
                    flush);
            })
            .Step("Paused, the badge joins the title and the spinner holds its glyph, so further paused renders of the same state write nothing; resuming shows the held glyph once more and then advances", context =>
            {
                var writer = new FakeTerminalWriter { WindowWidth = 40, WindowHeight = 6 };
                using var dashboard = new LiveDashboard(writer, LiveCapabilities(), () => new[] { WarmupSnapshot(42) });
                dashboard.Start();
                dashboard.Render(LiveDashboardViewState.Default);
                writer.ClearWrites();

                var paused = LiveDashboardViewState.Default with { IsPaused = true };
                dashboard.Render(paused);
                Assert.AreEqual(SynchronizedFlush(RepaintedLine(1, TitleLine("⠙") + " · " + LiveDashboardLayout.PausedBadgeText)), Assert.ContainsSingle(writer.Writes));
                writer.ClearWrites();

                dashboard.Render(paused);
                dashboard.Render(paused);
                Assert.IsEmpty(writer.Writes);
                // Every render still reads the size once: a resize while paused is caught.
                Assert.AreEqual(4, writer.WindowWidthReadCount);
                Assert.AreEqual(4, writer.WindowHeightReadCount);

                dashboard.Render(LiveDashboardViewState.Default);
                Assert.AreEqual(SynchronizedFlush(RepaintedLine(1, TitleLine("⠙"))), Assert.ContainsSingle(writer.Writes));
                writer.ClearWrites();

                dashboard.Render(LiveDashboardViewState.Default);
                Assert.AreEqual(SynchronizedFlush(RepaintedLine(1, TitleLine("⠹"))), Assert.ContainsSingle(writer.Writes));
            })
            .Step("A resize while paused is a full redraw at the new size, laid out with the held glyph and the badge", context =>
            {
                var writer = new FakeTerminalWriter { WindowWidth = 40, WindowHeight = 6 };
                using var dashboard = new LiveDashboard(writer, LiveCapabilities(), () => new[] { WarmupSnapshot(42) });
                dashboard.Start();
                dashboard.Render(LiveDashboardViewState.Default);
                var paused = LiveDashboardViewState.Default with { IsPaused = true };
                dashboard.Render(paused);
                writer.ClearWrites();

                writer.WindowWidth = 50;
                writer.WindowHeight = 7;
                dashboard.Render(paused);

                var flush = Assert.ContainsSingle(writer.Writes);
                var expectedLines = LiveDashboardLayout.Render(new[] { WarmupSnapshot(42) }, paused, 50, 7, ColorMode.None, SparklineGlyphSet.Braille, "⠙");
                Assert.HasCount(7, expectedLines);
                Assert.AreEqual(FullRedraw(expectedLines), flush);
                Assert.Contains(AnsiCodes.MoveCursor(1, 1) + TitleLine("⠙") + " · " + LiveDashboardLayout.PausedBadgeText, flush);
            })
            .Run();
    }

    [Test]
    public async Task Verify_dispose_restores_the_terminal_exactly_once()
    {
        await Scenario()
            .Step("Dispose resets styling, re-enables auto-wrap, shows the cursor and leaves the alternate screen in one write", context =>
            {
                var writer = new FakeTerminalWriter { WindowWidth = 40, WindowHeight = 6 };
                var dashboard = new LiveDashboard(writer, LiveCapabilities(), () => new[] { WarmupSnapshot(42) });
                dashboard.Start();
                dashboard.Render(LiveDashboardViewState.Default);
                writer.ClearWrites();

                dashboard.Dispose();

                var write = Assert.ContainsSingle(writer.Writes);
                Assert.AreEqual(RestoreSequence, write);
            })
            .Step("A second Dispose writes nothing, and Start and Render throw afterwards", context =>
            {
                var writer = new FakeTerminalWriter { WindowWidth = 40, WindowHeight = 6 };
                var dashboard = new LiveDashboard(writer, LiveCapabilities(), () => new[] { WarmupSnapshot(42) });
                dashboard.Start();
                dashboard.Render(LiveDashboardViewState.Default);
                dashboard.Dispose();
                writer.ClearWrites();

                dashboard.Dispose();

                Assert.IsEmpty(writer.Writes);
                Assert.ThrowsExactly<ObjectDisposedException>(() => dashboard.Render(LiveDashboardViewState.Default));
                Assert.ThrowsExactly<ObjectDisposedException>(() => dashboard.Start());
                Assert.IsEmpty(writer.Writes);
            })
            .Step("Disposing a dashboard that was never started writes nothing", context =>
            {
                var writer = new FakeTerminalWriter();
                var dashboard = new LiveDashboard(writer, LiveCapabilities(), () => new[] { WarmupSnapshot(42) });

                dashboard.Dispose();

                Assert.IsEmpty(writer.Writes);
            })
            .Run();
    }

    [Test]
    public async Task Verify_terminal_is_restored_on_every_exit_path()
    {
        await Scenario()
            .Step("An exception thrown while the dashboard is in use still leaves the alternate screen, once, as the last write", context =>
            {
                var writer = new FakeTerminalWriter { WindowWidth = 40, WindowHeight = 6 };

                Assert.ThrowsExactly<InvalidOperationException>(() =>
                {
                    using (var dashboard = new LiveDashboard(writer, LiveCapabilities(), () => new[] { WarmupSnapshot(42) }))
                    {
                        dashboard.Start();
                        dashboard.Render(LiveDashboardViewState.Default);
                        throw new InvalidOperationException("The run body failed.");
                    }
                });

                AssertRestoredOnce(writer);
            })
            .Step("Cancellation while the dashboard is in use still leaves the alternate screen, once, as the last write", context =>
            {
                var writer = new FakeTerminalWriter { WindowWidth = 40, WindowHeight = 6 };
                using var cancellation = new CancellationTokenSource();

                Assert.ThrowsExactly<OperationCanceledException>(() =>
                {
                    using (var dashboard = new LiveDashboard(writer, LiveCapabilities(), () => new[] { WarmupSnapshot(42) }))
                    {
                        dashboard.Start();
                        dashboard.Render(LiveDashboardViewState.Default);
                        cancellation.Cancel();
                        cancellation.Token.ThrowIfCancellationRequested();
                        dashboard.Render(LiveDashboardViewState.Default);
                    }
                });

                AssertRestoredOnce(writer);
            })
            .Step("Normal completion leaves the alternate screen once, after the final frame", context =>
            {
                var writer = new FakeTerminalWriter { WindowWidth = 40, WindowHeight = 6 };

                using (var dashboard = new LiveDashboard(writer, LiveCapabilities(), () => new[] { WarmupSnapshot(42) }))
                {
                    dashboard.Start();
                    dashboard.Render(LiveDashboardViewState.Default);
                    dashboard.Render(LiveDashboardViewState.Default);
                }

                AssertRestoredOnce(writer);
                Assert.HasCount(4, writer.Writes);
                Assert.AreEqual(EnterSequence, writer.Writes[0]);
            })
            .Run();
    }

    [Test]
    public async Task Verify_construction_guards_and_cadence()
    {
        await Scenario()
            .Step("A terminal without live view support is rejected before anything is written", context =>
            {
                var writer = new FakeTerminalWriter();
                var redirected = TerminalCapabilities.Resolve(isOutputRedirected: true, isInputRedirected: true, isVirtualTerminalEnabled: true, term: "xterm-256color", colorTerm: null, noColor: null);
                Assert.IsFalse(redirected.SupportsLiveView);

                Assert.ThrowsExactly<ArgumentException>(() => new LiveDashboard(writer, redirected, () => new[] { WarmupSnapshot(42) }));
                Assert.IsEmpty(writer.Writes);
            })
            .Step("Null arguments are rejected", context =>
            {
                var writer = new FakeTerminalWriter();

                Assert.ThrowsExactly<ArgumentNullException>(() => new LiveDashboard(null!, LiveCapabilities(), () => new[] { WarmupSnapshot(42) }));
                Assert.ThrowsExactly<ArgumentNullException>(() => new LiveDashboard(writer, null!, () => new[] { WarmupSnapshot(42) }));
                Assert.ThrowsExactly<ArgumentNullException>(() => new LiveDashboard(writer, LiveCapabilities(), null!));
            })
            .Step("The render cadence is about four frames per second", context =>
            {
                Assert.AreEqual(TimeSpan.FromMilliseconds(250), LiveDashboard.RenderInterval);
            })
            .Run();
    }

    private static void AssertRestoredOnce(FakeTerminalWriter writer)
    {
        Assert.IsNotEmpty(writer.Writes);
        Assert.AreEqual(RestoreSequence, writer.Writes[writer.Writes.Count - 1]);
        Assert.AreEqual(1, writer.Writes.Count(write => write == RestoreSequence));
        Assert.AreEqual(1, writer.Writes.Count(write => write == EnterSequence));
    }

    private static string FullRedraw(IReadOnlyList<RenderedLine> lines)
    {
        var parts = new List<string> { AnsiCodes.DisableAutoWrap, AnsiCodes.Reset, AnsiCodes.EraseScreen };
        for (var row = 0; row < lines.Count; row++)
        {
            parts.Add(AnsiCodes.MoveCursor(row + 1, 1));
            parts.Add(lines[row].Text);
        }

        return SynchronizedFlush(parts.ToArray());
    }

    private static string RepaintedLine(int row, string text)
    {
        return AnsiCodes.MoveCursor(row, 1) + AnsiCodes.Reset + AnsiCodes.EraseLine + text;
    }

    private static string SynchronizedFlush(params string[] parts)
    {
        return AnsiCodes.BeginSynchronizedOutput + string.Concat(parts) + AnsiCodes.EndSynchronizedOutput;
    }
}
