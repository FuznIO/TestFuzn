using Fuzn.TestFuzn.Internals.Terminal;

namespace Fuzn.TestFuzn.Tests.Terminal;

[TestClass]
public class FrameRendererTests : Test
{
    [Test]
    public async Task Verify_first_render_is_a_full_redraw()
    {
        await Scenario()
            .Step("The first render disables auto-wrap, resets styling, erases the screen, and paints every line at its row", context =>
            {
                var writer = new FakeTerminalWriter();
                var renderer = new FrameRenderer(writer);

                renderer.Render(CreateFrame("Scenario: checkout", "Requests: 128", "Errors: 0"), 80, 24);

                var flush = Assert.ContainsSingle(writer.Writes);
                Assert.AreEqual(
                    SynchronizedFlush(
                        AnsiCodes.DisableAutoWrap,
                        AnsiCodes.Reset,
                        AnsiCodes.EraseScreen,
                        AnsiCodes.MoveCursor(1, 1), "Scenario: checkout",
                        AnsiCodes.MoveCursor(2, 1), "Requests: 128",
                        AnsiCodes.MoveCursor(3, 1), "Errors: 0"),
                    flush);
            })
            .Step("The first render of an empty frame still erases the screen", context =>
            {
                var writer = new FakeTerminalWriter();
                var renderer = new FrameRenderer(writer);

                renderer.Render(new FrameBuffer(), 80, 24);

                var flush = Assert.ContainsSingle(writer.Writes);
                Assert.AreEqual(
                    SynchronizedFlush(AnsiCodes.DisableAutoWrap, AnsiCodes.Reset, AnsiCodes.EraseScreen),
                    flush);
            })
            .Run();
    }

    [Test]
    public async Task Verify_unchanged_frame_writes_nothing()
    {
        await Scenario()
            .Step("Rendering the same frame again emits no output at all", context =>
            {
                var writer = new FakeTerminalWriter();
                var renderer = new FrameRenderer(writer);
                var frame = CreateFrame("Scenario: checkout", "Requests: 128");
                renderer.Render(frame, 80, 24);
                writer.ClearWrites();

                renderer.Render(frame, 80, 24);

                Assert.IsEmpty(writer.Writes);
            })
            .Step("A different frame instance with equal lines also emits nothing", context =>
            {
                var writer = new FakeTerminalWriter();
                var renderer = new FrameRenderer(writer);
                renderer.Render(CreateFrame("Scenario: checkout", "Requests: 128"), 80, 24);
                writer.ClearWrites();

                renderer.Render(CreateFrame("Scenario: checkout", "Requests: 128"), 80, 24);

                Assert.IsEmpty(writer.Writes);
            })
            .Run();
    }

    [Test]
    public async Task Verify_single_changed_line_repaints_only_that_line()
    {
        await Scenario()
            .Step("Only the changed line is addressed, style-reset, erased, and rewritten", context =>
            {
                var writer = new FakeTerminalWriter();
                var renderer = new FrameRenderer(writer);
                renderer.Render(CreateFrame("Scenario: checkout", "Requests: 128", "Errors: 0"), 80, 24);
                writer.ClearWrites();

                renderer.Render(CreateFrame("Scenario: checkout", "Requests: 256", "Errors: 0"), 80, 24);

                var flush = Assert.ContainsSingle(writer.Writes);
                Assert.AreEqual(
                    SynchronizedFlush(AnsiCodes.MoveCursor(2, 1), AnsiCodes.Reset, AnsiCodes.EraseLine, "Requests: 256"),
                    flush);
            })
            .Step("The diff runs against a snapshot, so mutating the rendered buffer is safe", context =>
            {
                var writer = new FakeTerminalWriter();
                var renderer = new FrameRenderer(writer);
                var frame = CreateFrame("Scenario: checkout", "Requests: 128");
                renderer.Render(frame, 80, 24);
                writer.ClearWrites();

                frame.Clear();
                frame.AddLines(new[] { "Scenario: checkout", "Requests: 256" });
                renderer.Render(frame, 80, 24);

                var flush = Assert.ContainsSingle(writer.Writes);
                Assert.AreEqual(
                    SynchronizedFlush(AnsiCodes.MoveCursor(2, 1), AnsiCodes.Reset, AnsiCodes.EraseLine, "Requests: 256"),
                    flush);
            })
            .Run();
    }

    [Test]
    public async Task Verify_shorter_frame_erases_leftover_lines()
    {
        await Scenario()
            .Step("Leftover lines from the previous frame are erased, not left on screen", context =>
            {
                var writer = new FakeTerminalWriter();
                var renderer = new FrameRenderer(writer);
                renderer.Render(CreateFrame("Scenario: checkout", "Requests: 128", "Errors: 0"), 80, 24);
                writer.ClearWrites();

                renderer.Render(CreateFrame("Scenario: checkout", "Requests: 128"), 80, 24);

                var flush = Assert.ContainsSingle(writer.Writes);
                Assert.AreEqual(
                    SynchronizedFlush(AnsiCodes.MoveCursor(3, 1), AnsiCodes.Reset, AnsiCodes.EraseLine),
                    flush);
            })
            .Step("A changed line and leftover erasure combine in one flush", context =>
            {
                var writer = new FakeTerminalWriter();
                var renderer = new FrameRenderer(writer);
                renderer.Render(CreateFrame("Scenario: checkout", "Requests: 128", "Errors: 0"), 80, 24);
                writer.ClearWrites();

                renderer.Render(CreateFrame("Scenario: checkout", "Requests: 256"), 80, 24);

                var flush = Assert.ContainsSingle(writer.Writes);
                Assert.AreEqual(
                    SynchronizedFlush(
                        AnsiCodes.MoveCursor(2, 1), AnsiCodes.Reset, AnsiCodes.EraseLine, "Requests: 256",
                        AnsiCodes.MoveCursor(3, 1), AnsiCodes.Reset, AnsiCodes.EraseLine),
                    flush);
            })
            .Step("Shrinking to an empty frame erases every previous line", context =>
            {
                var writer = new FakeTerminalWriter();
                var renderer = new FrameRenderer(writer);
                renderer.Render(CreateFrame("Scenario: checkout", "Requests: 128"), 80, 24);
                writer.ClearWrites();

                renderer.Render(new FrameBuffer(), 80, 24);

                var flush = Assert.ContainsSingle(writer.Writes);
                Assert.AreEqual(
                    SynchronizedFlush(
                        AnsiCodes.MoveCursor(1, 1), AnsiCodes.Reset, AnsiCodes.EraseLine,
                        AnsiCodes.MoveCursor(2, 1), AnsiCodes.Reset, AnsiCodes.EraseLine),
                    flush);
            })
            .Run();
    }

    [Test]
    public async Task Verify_longer_frame_paints_the_new_lines()
    {
        await Scenario()
            .Step("Lines added below the previous frame are painted at their rows", context =>
            {
                var writer = new FakeTerminalWriter();
                var renderer = new FrameRenderer(writer);
                renderer.Render(CreateFrame("Scenario: checkout", "Requests: 128"), 80, 24);
                writer.ClearWrites();

                renderer.Render(CreateFrame("Scenario: checkout", "Requests: 128", "Errors: 0"), 80, 24);

                var flush = Assert.ContainsSingle(writer.Writes);
                Assert.AreEqual(
                    SynchronizedFlush(AnsiCodes.MoveCursor(3, 1), AnsiCodes.Reset, AnsiCodes.EraseLine, "Errors: 0"),
                    flush);
            })
            .Run();
    }

    [Test]
    public async Task Verify_passed_size_change_forces_full_redraw()
    {
        await Scenario()
            .Step("Passing a changed width repaints the whole frame even though no line changed", context =>
            {
                var writer = new FakeTerminalWriter();
                var renderer = new FrameRenderer(writer);
                renderer.Render(CreateFrame("Scenario: checkout", "Requests: 128"), 80, 24);
                writer.ClearWrites();

                renderer.Render(CreateFrame("Scenario: checkout", "Requests: 128"), 120, 24);

                var flush = Assert.ContainsSingle(writer.Writes);
                Assert.AreEqual(
                    SynchronizedFlush(
                        AnsiCodes.DisableAutoWrap,
                        AnsiCodes.Reset,
                        AnsiCodes.EraseScreen,
                        AnsiCodes.MoveCursor(1, 1), "Scenario: checkout",
                        AnsiCodes.MoveCursor(2, 1), "Requests: 128"),
                    flush);
            })
            .Step("Passing a changed height repaints the whole frame even though no line changed", context =>
            {
                var writer = new FakeTerminalWriter();
                var renderer = new FrameRenderer(writer);
                renderer.Render(CreateFrame("Scenario: checkout", "Requests: 128"), 80, 24);
                writer.ClearWrites();

                renderer.Render(CreateFrame("Scenario: checkout", "Requests: 128"), 80, 40);

                var flush = Assert.ContainsSingle(writer.Writes);
                Assert.AreEqual(
                    SynchronizedFlush(
                        AnsiCodes.DisableAutoWrap,
                        AnsiCodes.Reset,
                        AnsiCodes.EraseScreen,
                        AnsiCodes.MoveCursor(1, 1), "Scenario: checkout",
                        AnsiCodes.MoveCursor(2, 1), "Requests: 128"),
                    flush);
            })
            .Step("After the resize redraw, an unchanged frame at the same size writes nothing again", context =>
            {
                var writer = new FakeTerminalWriter();
                var renderer = new FrameRenderer(writer);
                renderer.Render(CreateFrame("Scenario: checkout"), 80, 24);
                renderer.Render(CreateFrame("Scenario: checkout"), 120, 24);
                writer.ClearWrites();

                renderer.Render(CreateFrame("Scenario: checkout"), 120, 24);

                Assert.IsEmpty(writer.Writes);
            })
            .Run();
    }

    [Test]
    public async Task Verify_styled_lines_diff_on_exact_sgr_sequences()
    {
        await Scenario()
            .Step("A line whose styling changed is repainted with the new SGR sequences intact", context =>
            {
                var writer = new FakeTerminalWriter();
                var renderer = new FrameRenderer(writer);
                var boldTotal = AnsiCodes.Bold + "Total: 42" + AnsiCodes.Reset;
                var greenTotal = AnsiCodes.Foreground(ConsoleColor.Green) + "Total: 42" + AnsiCodes.Reset;
                renderer.Render(CreateFrame("Summary", boldTotal), 80, 24);
                writer.ClearWrites();

                renderer.Render(CreateFrame("Summary", greenTotal), 80, 24);

                var flush = Assert.ContainsSingle(writer.Writes);
                Assert.AreEqual(
                    SynchronizedFlush(AnsiCodes.MoveCursor(2, 1), AnsiCodes.Reset, AnsiCodes.EraseLine, greenTotal),
                    flush);
            })
            .Step("An identically styled line is not repainted", context =>
            {
                var writer = new FakeTerminalWriter();
                var renderer = new FrameRenderer(writer);
                var boldTotal = AnsiCodes.Bold + "Total: 42" + AnsiCodes.Reset;
                renderer.Render(CreateFrame("Summary", boldTotal), 80, 24);
                writer.ClearWrites();

                renderer.Render(CreateFrame("Summary", AnsiCodes.Bold + "Total: 42" + AnsiCodes.Reset), 80, 24);

                Assert.IsEmpty(writer.Writes);
            })
            .Run();
    }

    [Test]
    public async Task Verify_frame_taller_than_window_is_clipped()
    {
        await Scenario()
            .Step("Only the lines that fit the passed height are painted", context =>
            {
                var writer = new FakeTerminalWriter();
                var renderer = new FrameRenderer(writer);

                renderer.Render(CreateFrame("Row 1", "Row 2", "Row 3"), 80, 2);

                var flush = Assert.ContainsSingle(writer.Writes);
                Assert.AreEqual(
                    SynchronizedFlush(
                        AnsiCodes.DisableAutoWrap,
                        AnsiCodes.Reset,
                        AnsiCodes.EraseScreen,
                        AnsiCodes.MoveCursor(1, 1), "Row 1",
                        AnsiCodes.MoveCursor(2, 1), "Row 2"),
                    flush);
            })
            .Step("A change on a clipped-away line emits nothing", context =>
            {
                var writer = new FakeTerminalWriter();
                var renderer = new FrameRenderer(writer);
                renderer.Render(CreateFrame("Row 1", "Row 2", "Row 3"), 80, 2);
                writer.ClearWrites();

                renderer.Render(CreateFrame("Row 1", "Row 2", "Row 3 changed"), 80, 2);

                Assert.IsEmpty(writer.Writes);
            })
            .Step("Growing the passed height full-redraws and paints the previously clipped line", context =>
            {
                var writer = new FakeTerminalWriter();
                var renderer = new FrameRenderer(writer);
                renderer.Render(CreateFrame("Row 1", "Row 2", "Row 3"), 80, 2);
                writer.ClearWrites();

                renderer.Render(CreateFrame("Row 1", "Row 2", "Row 3"), 80, 3);

                var flush = Assert.ContainsSingle(writer.Writes);
                Assert.AreEqual(
                    SynchronizedFlush(
                        AnsiCodes.DisableAutoWrap,
                        AnsiCodes.Reset,
                        AnsiCodes.EraseScreen,
                        AnsiCodes.MoveCursor(1, 1), "Row 1",
                        AnsiCodes.MoveCursor(2, 1), "Row 2",
                        AnsiCodes.MoveCursor(3, 1), "Row 3"),
                    flush);
            })
            .Run();
    }

    [Test]
    public async Task Verify_degenerate_window_heights_emit_only_the_erase_redraw()
    {
        await Scenario()
            .Step("A passed height of zero clips every line and emits only the erase-only full redraw", context =>
            {
                var writer = new FakeTerminalWriter();
                var renderer = new FrameRenderer(writer);

                renderer.Render(CreateFrame("Row 1", "Row 2"), 80, 0);

                var flush = Assert.ContainsSingle(writer.Writes);
                Assert.AreEqual(
                    SynchronizedFlush(AnsiCodes.DisableAutoWrap, AnsiCodes.Reset, AnsiCodes.EraseScreen),
                    flush);
            })
            .Step("A negative passed height clips every line and emits only the erase-only full redraw", context =>
            {
                var writer = new FakeTerminalWriter();
                var renderer = new FrameRenderer(writer);

                renderer.Render(CreateFrame("Row 1", "Row 2"), 80, -1);

                var flush = Assert.ContainsSingle(writer.Writes);
                Assert.AreEqual(
                    SynchronizedFlush(AnsiCodes.DisableAutoWrap, AnsiCodes.Reset, AnsiCodes.EraseScreen),
                    flush);
            })
            .Run();
    }

    [Test]
    public async Task Verify_reset_forces_full_redraw()
    {
        await Scenario()
            .Step("After Reset the next render repaints the whole frame", context =>
            {
                var writer = new FakeTerminalWriter();
                var renderer = new FrameRenderer(writer);
                renderer.Render(CreateFrame("Scenario: checkout", "Requests: 128"), 80, 24);
                writer.ClearWrites();

                renderer.Reset();
                renderer.Render(CreateFrame("Scenario: checkout", "Requests: 128"), 80, 24);

                var flush = Assert.ContainsSingle(writer.Writes);
                Assert.AreEqual(
                    SynchronizedFlush(
                        AnsiCodes.DisableAutoWrap,
                        AnsiCodes.Reset,
                        AnsiCodes.EraseScreen,
                        AnsiCodes.MoveCursor(1, 1), "Scenario: checkout",
                        AnsiCodes.MoveCursor(2, 1), "Requests: 128"),
                    flush);
            })
            .Run();
    }

    [Test]
    public async Task Verify_null_arguments_are_rejected()
    {
        await Scenario()
            .Step("The renderer requires a writer", context =>
            {
                Assert.ThrowsExactly<ArgumentNullException>(() => new FrameRenderer(null!));
            })
            .Step("Render requires a frame", context =>
            {
                var renderer = new FrameRenderer(new FakeTerminalWriter());

                Assert.ThrowsExactly<ArgumentNullException>(() => renderer.Render(null!, 80, 24));
            })
            .Run();
    }

    private static FrameBuffer CreateFrame(params string[] lines)
    {
        var frame = new FrameBuffer();
        frame.AddLines(lines);
        return frame;
    }

    private static string SynchronizedFlush(params string[] parts)
    {
        return AnsiCodes.BeginSynchronizedOutput + string.Concat(parts) + AnsiCodes.EndSynchronizedOutput;
    }
}
