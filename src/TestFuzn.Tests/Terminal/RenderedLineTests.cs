using Fuzn.TestFuzn.Internals.Terminal;

namespace Fuzn.TestFuzn.Tests.Terminal;

[TestClass]
public class RenderedLineTests : Test
{
    [Test]
    public async Task Verify_rendered_line_carries_its_text_and_declared_width()
    {
        await Scenario()
            .Step("The text and width are stored as constructed", context =>
            {
                var line = new RenderedLine("hello", 5);

                Assert.AreEqual("hello", line.Text);
                Assert.AreEqual(5, line.Width);
            })
            .Step("An empty line of width 0 is valid", context =>
            {
                var line = new RenderedLine(string.Empty, 0);

                Assert.AreEqual(string.Empty, line.Text);
                Assert.AreEqual(0, line.Width);
            })
            .Run();
    }

    [Test]
    public async Task Verify_rendered_line_rejects_breaks_null_and_negative_width()
    {
        await Scenario()
            .Step("Null text is rejected", context =>
            {
                Assert.ThrowsExactly<ArgumentNullException>(() => new RenderedLine(null!, 0));
            })
            .Step("Text containing a line break fails loud; rendered lines are break-free by construction", context =>
            {
                Assert.ThrowsExactly<ArgumentException>(() => new RenderedLine("a\nb", 3));
                Assert.ThrowsExactly<ArgumentException>(() => new RenderedLine("a\rb", 3));
                Assert.ThrowsExactly<ArgumentException>(() => new RenderedLine("a\r\nb", 3));
            })
            .Step("A negative width is rejected", context =>
            {
                Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new RenderedLine("a", -1));
            })
            .Run();
    }
}
