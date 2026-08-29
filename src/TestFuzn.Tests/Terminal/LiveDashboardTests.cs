using Fuzn.TestFuzn.Internals.Execution;
using Fuzn.TestFuzn.Internals.Terminal;

namespace Fuzn.TestFuzn.Tests.Terminal;

/// <summary>
/// Pins the live dashboard's terminal lifecycle and render wiring against a fake writer: the
/// sequences that enter and leave the alternate screen, that the terminal is restored exactly
/// once on every exit path (completion, an exception, cancellation), that each render reads the
/// writer's size once and paints the layout at that size through the diff renderer, and that
/// the spinner advances one glyph per render.
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

                Assert.ThrowsExactly<InvalidOperationException>(() => dashboard.Render());
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

                dashboard.Render();

                var flush = Assert.ContainsSingle(writer.Writes);
                var expectedLines = LiveDashboardLayout.Render(new[] { WarmupSnapshot(42) }, LiveDashboardViewState.Default, 40, 6, ColorMode.None, SparklineGlyphSet.Braille, "⠋");
                Assert.HasCount(6, expectedLines);
                Assert.AreEqual(FullRedraw(expectedLines), flush);
                // Hand-derived anchors, so the golden is not only the layout compared to itself:
                // below 64 columns the tiles wrap, and the first row's three boxes share 38
                // columns as 13, 13 and 12.
                Assert.Contains(AnsiCodes.MoveCursor(1, 1) + TitleLine("⠋") + AnsiCodes.MoveCursor(2, 1) + "╭───────────╮ ╭───────────╮ ╭──────────╮", flush);
                Assert.EndsWith(AnsiCodes.MoveCursor(6, 1) + "q quit" + AnsiCodes.EndSynchronizedOutput, flush);
            })
            .Step("An unchanged data state repaints only the spinner on the title row", context =>
            {
                var writer = new FakeTerminalWriter { WindowWidth = 40, WindowHeight = 6 };
                using var dashboard = new LiveDashboard(writer, LiveCapabilities(), () => new[] { WarmupSnapshot(42) });
                dashboard.Start();
                dashboard.Render();
                writer.ClearWrites();

                dashboard.Render();

                var flush = Assert.ContainsSingle(writer.Writes);
                Assert.AreEqual(SynchronizedFlush(RepaintedLine(1, TitleLine("⠙"))), flush);
            })
            .Step("A resized terminal gets a full redraw laid out at the new size", context =>
            {
                var writer = new FakeTerminalWriter { WindowWidth = 40, WindowHeight = 6 };
                using var dashboard = new LiveDashboard(writer, LiveCapabilities(), () => new[] { WarmupSnapshot(42) });
                dashboard.Start();
                dashboard.Render();
                writer.ClearWrites();

                writer.WindowWidth = 50;
                writer.WindowHeight = 7;
                dashboard.Render();

                var flush = Assert.ContainsSingle(writer.Writes);
                var expectedLines = LiveDashboardLayout.Render(new[] { WarmupSnapshot(42) }, LiveDashboardViewState.Default, 50, 7, ColorMode.None, SparklineGlyphSet.Braille, "⠙");
                Assert.HasCount(7, expectedLines);
                Assert.AreEqual(FullRedraw(expectedLines), flush);
                Assert.EndsWith(AnsiCodes.MoveCursor(7, 1) + "q quit" + AnsiCodes.EndSynchronizedOutput, flush);
            })
            .Step("Each render reads the width and the height exactly once, and Start reads neither", context =>
            {
                var writer = new FakeTerminalWriter { WindowWidth = 40, WindowHeight = 6 };
                using var dashboard = new LiveDashboard(writer, LiveCapabilities(), () => new[] { WarmupSnapshot(42) });

                dashboard.Start();
                Assert.AreEqual(0, writer.WindowWidthReadCount);
                Assert.AreEqual(0, writer.WindowHeightReadCount);

                dashboard.Render();
                Assert.AreEqual(1, writer.WindowWidthReadCount);
                Assert.AreEqual(1, writer.WindowHeightReadCount);

                dashboard.Render();
                dashboard.Render();
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

                Assert.ThrowsExactly<IOException>(() => dashboard.Render());
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
                dashboard.Render();
                writer.ClearWrites();

                snapshots[0] = WarmupSnapshot(43);
                dashboard.Render();

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

                Assert.ThrowsExactly<InvalidOperationException>(() => dashboard.Render());
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
                dashboard.Render();
                writer.ClearWrites();

                for (var render = 0; render < BrailleSpinnerFrames.Length; render++)
                    dashboard.Render();

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

                dashboard.Render();
                Assert.Contains(AnsiCodes.MoveCursor(1, 1) + TitleLine(AsciiSpinnerFrames[0]), Assert.ContainsSingle(writer.Writes));
                writer.ClearWrites();

                for (var render = 0; render < AsciiSpinnerFrames.Length; render++)
                    dashboard.Render();

                Assert.HasCount(AsciiSpinnerFrames.Length, writer.Writes);
                for (var index = 1; index < AsciiSpinnerFrames.Length; index++)
                    Assert.AreEqual(SynchronizedFlush(RepaintedLine(1, TitleLine(AsciiSpinnerFrames[index]))), writer.Writes[index - 1]);

                Assert.AreEqual(SynchronizedFlush(RepaintedLine(1, TitleLine(AsciiSpinnerFrames[0]))), writer.Writes[AsciiSpinnerFrames.Length - 1]);
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
                dashboard.Render();
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
                dashboard.Render();
                dashboard.Dispose();
                writer.ClearWrites();

                dashboard.Dispose();

                Assert.IsEmpty(writer.Writes);
                Assert.ThrowsExactly<ObjectDisposedException>(() => dashboard.Render());
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
                        dashboard.Render();
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
                        dashboard.Render();
                        cancellation.Cancel();
                        cancellation.Token.ThrowIfCancellationRequested();
                        dashboard.Render();
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
                    dashboard.Render();
                    dashboard.Render();
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
