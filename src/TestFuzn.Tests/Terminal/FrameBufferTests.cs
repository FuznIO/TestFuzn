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

                Assert.ThrowsExactly<ArgumentNullException>(() => frame.AddLines(null!));
                Assert.ThrowsExactly<ArgumentNullException>(() => frame.AddLines(new[] { "Valid line", null! }));
            })
            .Run();
    }
}
