using Fuzn.TestFuzn.Internals.Terminal;

namespace Fuzn.TestFuzn.Tests.Terminal;

/// <summary>
/// Pins <see cref="ExceptionRenderer"/>: the headline of type and message, the frames of a
/// thrown exception kept whole one per line, inner exceptions chained "Caused by" and an
/// aggregate's numbered, each level indented two columns deeper up to the depth cap; messages
/// escaped and control-sanitized, one line per line of a multi-line message; plain with zero
/// escape bytes in color mode None and styled in TrueColor; and the writer form writing one
/// line per write.
/// </summary>
[TestClass]
public class ExceptionRendererTests : Test
{
    [Test]
    public async Task Verify_headline_and_inner_exceptions_render_plain_in_color_mode_None()
    {
        await Scenario()
            .Step("A chain of inner exceptions: one headline per exception, each 'Caused by' a level deeper; never thrown, so no frames", context =>
            {
                var exception = new Exception("Step failed", new InvalidOperationException("Connection refused [localhost:7058]", new TimeoutException("Socket timed out")));

                var lines = ExceptionRenderer.Render(exception, ColorMode.None);

                CollectionAssert.AreEqual(
                    new[]
                    {
                        "Exception: Step failed",
                        "  Caused by: InvalidOperationException: Connection refused [localhost:7058]",
                        "    Caused by: TimeoutException: Socket timed out"
                    },
                    lines.Select(line => line.Text).ToList());
                foreach (var line in lines)
                {
                    Assert.AreEqual(line.Text.Length, line.Width);
                    Assert.DoesNotContain(AnsiCodes.Escape, line.Text);
                }
            })
            .Step("An aggregate's inner exceptions are numbered", context =>
            {
                var exception = new AggregateException(new InvalidOperationException("First failure"), new TimeoutException("Second failure"));

                var lines = ExceptionRenderer.Render(exception, ColorMode.None);

                CollectionAssert.AreEqual(
                    new[]
                    {
                        "AggregateException: One or more errors occurred. (First failure) (Second failure)",
                        "  Inner exception 1 of 2: InvalidOperationException: First failure",
                        "  Inner exception 2 of 2: TimeoutException: Second failure"
                    },
                    lines.Select(line => line.Text).ToList());
            })
            .Step("A multi-line message renders one line per line, the continuation lines without the type", context =>
            {
                var lines = ExceptionRenderer.Render(new Exception("line one\nline two\r\nline three"), ColorMode.None);

                CollectionAssert.AreEqual(new[] { "Exception: line one", "line two", "line three" }, lines.Select(line => line.Text).ToList());
            })
            .Step("Brackets in a message render literally and control characters sanitize to spaces, so a message can neither style nor clear the screen", context =>
            {
                var lines = ExceptionRenderer.Render(new Exception("tab\there \u001b[2J end"), ColorMode.None);

                var headline = Assert.ContainsSingle(lines);
                Assert.AreEqual("Exception: tab here  [2J end", headline.Text);
                Assert.DoesNotContain(AnsiCodes.Escape, headline.Text);
            })
            .Step("Null is rejected", context =>
            {
                Assert.ThrowsExactly<ArgumentNullException>(() => ExceptionRenderer.Render(null!, ColorMode.None));
            })
            .Run();
    }

    [Test]
    public async Task Verify_frames_of_a_thrown_exception_follow_the_headline_kept_whole()
    {
        await Scenario()
            .Step("Every frame of the stack trace is one line after the headline, its text kept whole — the file and line when the trace carries them", context =>
            {
                var thrown = Throw("Thrown for the trace");
                var expectedFrames = thrown.StackTrace!.Split('\n').Select(frame => frame.TrimEnd()).Where(frame => frame.Length > 0).ToList();
                Assert.IsNotEmpty(expectedFrames);

                var lines = ExceptionRenderer.Render(thrown, ColorMode.None);

                Assert.AreEqual("InvalidOperationException: Thrown for the trace", lines[0].Text);
                CollectionAssert.AreEqual(expectedFrames, lines.Skip(1).Select(line => line.Text).ToList());
                Assert.StartsWith("   at " + typeof(ExceptionRendererTests).FullName + ".", lines[1].Text);
            })
            .Step("A thrown outer exception's frames come before its inner exception, whose own frames are indented with it", context =>
            {
                var inner = Throw("Inner");
                Exception outer;
                try
                {
                    throw new Exception("Outer", inner);
                }
                catch (Exception exception)
                {
                    outer = exception;
                }

                var lines = ExceptionRenderer.Render(outer, ColorMode.None).Select(line => line.Text).ToList();

                Assert.AreEqual("Exception: Outer", lines[0]);
                Assert.StartsWith("   at ", lines[1]);
                var causedBy = lines.IndexOf("  Caused by: InvalidOperationException: Inner");
                Assert.IsGreaterThan(1, causedBy);
                Assert.StartsWith("     at ", lines[causedBy + 1]);
            })
            .Run();
    }

    [Test]
    public async Task Verify_depth_cap_elides_deeper_inner_exceptions()
    {
        await Scenario()
            .Step("A chain of twelve renders ten levels and one elision line in place of the rest", context =>
            {
                var exception = new Exception("level 11");
                for (var level = 10; level >= 0; level--)
                    exception = new Exception("level " + level, exception);

                var lines = ExceptionRenderer.Render(exception, ColorMode.None).Select(line => line.Text).ToList();

                Assert.HasCount(ExceptionRenderer.MaximumDepth + 1, lines);
                Assert.AreEqual("Exception: level 0", lines[0]);
                Assert.AreEqual(new string(' ', 18) + "Caused by: Exception: level 9", lines[9]);
                Assert.AreEqual(new string(' ', 20) + ExceptionRenderer.ElidedText, lines[10]);
            })
            .Run();
    }

    [Test]
    public async Task Verify_TrueColor_styles_the_type_message_frames_and_labels()
    {
        await Scenario()
            .Step("The type is bold red, the message red, the inner-exception label yellow; the indent stays plain", context =>
            {
                var exception = new Exception("Step failed", new InvalidOperationException("Connection refused [localhost:7058]"));

                var lines = ExceptionRenderer.Render(exception, ColorMode.TrueColor);

                Assert.HasCount(2, lines);
                Assert.AreEqual("\u001b[1;38;5;9mException:\u001b[0m \u001b[38;5;9mStep failed\u001b[0m", lines[0].Text);
                Assert.AreEqual("Exception: Step failed".Length, lines[0].Width);
                Assert.AreEqual("  \u001b[38;5;11mCaused by:\u001b[0m \u001b[1;38;5;9mInvalidOperationException:\u001b[0m \u001b[38;5;9mConnection refused [localhost:7058]\u001b[0m", lines[1].Text);
            })
            .Step("Frames are dimmed whole", context =>
            {
                var thrown = Throw("Thrown for the trace");

                var lines = ExceptionRenderer.Render(thrown, ColorMode.TrueColor);

                Assert.IsGreaterThan(1, lines.Count);
                Assert.StartsWith("\u001b[2m   at ", lines[1].Text);
                Assert.EndsWith("\u001b[0m", lines[1].Text);
            })
            .Run();
    }

    [Test]
    public async Task Verify_write_emits_one_line_per_write_ending_in_a_line_break()
    {
        await Scenario()
            .Step("Each rendered line is one write with the line break; the writer's size is never read", context =>
            {
                var writer = new FakeTerminalWriter();
                var exception = new Exception("Step failed", new TimeoutException("Socket timed out"));

                ExceptionRenderer.Write(writer, exception, ColorMode.None);

                CollectionAssert.AreEqual(
                    new[]
                    {
                        "Exception: Step failed" + Environment.NewLine,
                        "  Caused by: TimeoutException: Socket timed out" + Environment.NewLine
                    },
                    writer.Writes.ToList());
                Assert.AreEqual(0, writer.WindowWidthReadCount);
                Assert.AreEqual(0, writer.WindowHeightReadCount);
            })
            .Step("A null writer or exception is rejected", context =>
            {
                Assert.ThrowsExactly<ArgumentNullException>(() => ExceptionRenderer.Write(null!, new Exception("x"), ColorMode.None));
                Assert.ThrowsExactly<ArgumentNullException>(() => ExceptionRenderer.Write(new FakeTerminalWriter(), null!, ColorMode.None));
            })
            .Run();
    }

    /// <summary>Throws and catches an exception, so it carries a stack trace.</summary>
    private static Exception Throw(string message)
    {
        try
        {
            throw new InvalidOperationException(message);
        }
        catch (InvalidOperationException exception)
        {
            return exception;
        }
    }
}
