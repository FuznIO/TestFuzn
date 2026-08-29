using Fuzn.TestFuzn.Internals.Terminal;

namespace Fuzn.TestFuzn.Tests.Terminal;

[TestClass]
public class FrameBufferTests : Test
{
    [Test]
    public async Task Verify_lines_accumulate_in_order()
    {
        await Scenario()
            .Step("AddLine appends lines top to bottom", context =>
            {
                var frame = new FrameBuffer();

                frame.AddLine("Scenario: checkout");
                frame.AddLine("Requests: 128");
                frame.AddLine("Errors: 0");

                Assert.HasCount(3, frame.Lines);
                Assert.AreEqual("Scenario: checkout", frame.Lines[0]);
                Assert.AreEqual("Requests: 128", frame.Lines[1]);
                Assert.AreEqual("Errors: 0", frame.Lines[2]);
            })
            .Step("AddLines appends every line after the existing ones", context =>
            {
                var frame = new FrameBuffer();
                frame.AddLine("Header");

                frame.AddLines(new[] { "Body first", "Body second" });

                Assert.HasCount(3, frame.Lines);
                Assert.AreEqual("Header", frame.Lines[0]);
                Assert.AreEqual("Body first", frame.Lines[1]);
                Assert.AreEqual("Body second", frame.Lines[2]);
            })
            .Step("An empty line is a valid frame line", context =>
            {
                var frame = new FrameBuffer();

                frame.AddLine("");

                Assert.ContainsSingle(frame.Lines);
                Assert.AreEqual("", frame.Lines[0]);
            })
            .Run();
    }

    [Test]
    public async Task Verify_line_breaks_split_into_one_entry_per_row()
    {
        await Scenario()
            .Step("AddLine splits text on mixed line break kinds into one entry per physical line", context =>
            {
                var frame = new FrameBuffer();

                frame.AddLine("a\r\nb\nc\rd");

                Assert.HasCount(4, frame.Lines);
                Assert.AreEqual("a", frame.Lines[0]);
                Assert.AreEqual("b", frame.Lines[1]);
                Assert.AreEqual("c", frame.Lines[2]);
                Assert.AreEqual("d", frame.Lines[3]);
            })
            .Step("A trailing line break terminates the final line without opening an empty one", context =>
            {
                var frame = new FrameBuffer();

                frame.AddLine("Status: Completed successfully.\r\n");

                Assert.ContainsSingle(frame.Lines);
                Assert.AreEqual("Status: Completed successfully.", frame.Lines[0]);
            })
            .Step("Consecutive breaks keep the blank line between them", context =>
            {
                var frame = new FrameBuffer();

                frame.AddLine("a\n\nb");

                Assert.HasCount(3, frame.Lines);
                Assert.AreEqual("a", frame.Lines[0]);
                Assert.AreEqual("", frame.Lines[1]);
                Assert.AreEqual("b", frame.Lines[2]);
            })
            .Step("A line break on its own stores one empty row", context =>
            {
                var frame = new FrameBuffer();

                frame.AddLine("\r\n");

                Assert.ContainsSingle(frame.Lines);
                Assert.AreEqual("", frame.Lines[0]);
            })
            .Step("AddLines splits each line the same way AddLine does", context =>
            {
                var frame = new FrameBuffer();

                frame.AddLines(new[] { "x\ny", "z" });

                Assert.HasCount(3, frame.Lines);
                Assert.AreEqual("x", frame.Lines[0]);
                Assert.AreEqual("y", frame.Lines[1]);
                Assert.AreEqual("z", frame.Lines[2]);
            })
            .Step("AddLines stores pre-rendered widget lines as-is, one entry per line", context =>
            {
                var frame = new FrameBuffer();

                frame.AddLines(StatTileWidget.Render(new StatTile("Requests", "128"), 12, ColorMode.None));

                Assert.HasCount(2, frame.Lines);
                Assert.AreEqual("Requests    ", frame.Lines[0]);
                Assert.AreEqual("128         ", frame.Lines[1]);
            })
            .Run();
    }

    [Test]
    public async Task Verify_clear_empties_the_buffer()
    {
        await Scenario()
            .Step("Clear removes all lines so the buffer can build the next frame", context =>
            {
                var frame = new FrameBuffer();
                frame.AddLine("Old frame line");

                frame.Clear();

                Assert.IsEmpty(frame.Lines);
            })
            .Run();
    }

    [Test]
    public async Task Verify_null_lines_are_rejected()
    {
        await Scenario()
            .Step("AddLine rejects a null line", context =>
            {
                var frame = new FrameBuffer();

                Assert.ThrowsExactly<ArgumentNullException>(() => frame.AddLine(null!));
            })
            .Step("AddLines rejects a null collection and a null element", context =>
            {
                var frame = new FrameBuffer();

                Assert.ThrowsExactly<ArgumentNullException>(() => frame.AddLines((IEnumerable<string>)null!));
                Assert.ThrowsExactly<ArgumentNullException>(() => frame.AddLines(new[] { "Valid line", null! }));
                Assert.ThrowsExactly<ArgumentNullException>(() => frame.AddLines((IEnumerable<RenderedLine>)null!));
            })
            .Run();
    }
}
