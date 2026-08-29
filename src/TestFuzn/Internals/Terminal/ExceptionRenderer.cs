namespace Fuzn.TestFuzn.Internals.Terminal;

/// <summary>
/// Renders an exception for the standalone runner's normal screen buffer — what the runner
/// prints when a run fails — as styled lines: a headline of the exception's type name and
/// message, its stack trace one frame per line (dimmed, each frame's text kept whole, so a
/// frame's <c>in file:line</c> stays), then the exceptions it wraps — the chain of inner
/// exceptions, each headed "Caused by:", or, for an <see cref="AggregateException"/>, every
/// inner exception numbered "Inner exception i of n:" — each rendered the same way and indented
/// two columns deeper per level, to a depth of <see cref="MaximumDepth"/>, past which the rest
/// is elided with one line. Text from the exception — the message, the frames — is escaped so
/// brackets render literally and control-sanitized so it can neither break a line nor carry an
/// escape sequence; a multi-line message renders one line per line. Nothing is truncated or
/// wrapped: a frame is what the reader copies from the scrollback, so it is written whole and
/// the terminal wraps it. Colors only when the color mode has them — the headline's type in bold
/// red, the message red, the frames dim, the inner-exception labels yellow — and plain text with
/// zero escape bytes in <see cref="ColorMode.None"/>. Stateless and thread-safe.
/// </summary>
internal static class ExceptionRenderer
{
    /// <summary>How many levels of inner exceptions are rendered before the rest is elided.</summary>
    public const int MaximumDepth = 10;

    /// <summary>The line rendered in place of the inner exceptions below <see cref="MaximumDepth"/>.</summary>
    internal const string ElidedText = "… deeper inner exceptions omitted";

    private const string TypeStyle = "bold " + TerminalPalette.FailedStyle;
    private const string MessageStyle = TerminalPalette.FailedStyle;
    private const string FrameStyle = TerminalPalette.SecondaryStyle;
    private const string InnerLabelStyle = TerminalPalette.WarningStyle;

    private const string IndentPerLevel = "  ";

    private static readonly string[] LineBreaks = { "\r\n", "\n", "\r" };

    /// <summary>Renders the exception, its stack trace and the exceptions it wraps as lines.</summary>
    public static IReadOnlyList<RenderedLine> Render(Exception exception, ColorMode colorMode)
    {
        if (exception == null)
            throw new ArgumentNullException(nameof(exception), "Exception cannot be null.");

        var lines = new List<RenderedLine>();
        AddException(lines, exception, string.Empty, 0, colorMode);
        return lines;
    }

    /// <summary>
    /// Writes the lines <see cref="Render"/> produces to the writer, one write per line, each
    /// ending in a line break. The writer is only ever written to: its size is never read, so a
    /// redirected output is fine.
    /// </summary>
    public static void Write(ITerminalWriter writer, Exception exception, ColorMode colorMode)
    {
        if (writer == null)
            throw new ArgumentNullException(nameof(writer), "Writer cannot be null.");

        foreach (var line in Render(exception, colorMode))
            writer.Write(line.Text + Environment.NewLine);
    }

    private static void AddException(List<RenderedLine> lines, Exception exception, string indent, int depth, ColorMode colorMode)
    {
        AddHeadline(lines, exception, indent, string.Empty, colorMode);
        AddFrames(lines, exception, indent, colorMode);
        AddInnerExceptions(lines, exception, indent, depth, colorMode);
    }

    // The headline: an optional label (the inner-exception heading), the type name and the
    // message — one line per line of the message, the continuation lines aligned under the
    // first without the type.
    private static void AddHeadline(List<RenderedLine> lines, Exception exception, string indent, string label, ColorMode colorMode)
    {
        var messageLines = SplitLines(exception.Message);

        var headline = indent + label + "[" + TypeStyle + "]" + MarkupParser.Escape(exception.GetType().Name) + ":[/] [" + MessageStyle + "]" + MarkupParser.Escape(messageLines[0]) + "[/]";
        lines.Add(MarkupText.RenderTruncated(headline, int.MaxValue, colorMode));

        for (var index = 1; index < messageLines.Length; index++)
            lines.Add(MarkupText.RenderTruncated(indent + "[" + MessageStyle + "]" + MarkupParser.Escape(messageLines[index]) + "[/]", int.MaxValue, colorMode));
    }

    // One dimmed line per frame of the stack trace, trailing whitespace dropped, empty lines
    // skipped; an exception that was never thrown has no trace and gets no frame lines.
    private static void AddFrames(List<RenderedLine> lines, Exception exception, string indent, ColorMode colorMode)
    {
        var stackTrace = exception.StackTrace;
        if (string.IsNullOrEmpty(stackTrace))
            return;

        foreach (var frame in SplitLines(stackTrace))
        {
            var text = frame.TrimEnd();
            if (text.Length == 0)
                continue;

            lines.Add(MarkupText.RenderTruncated(indent + "[" + FrameStyle + "]" + MarkupParser.Escape(text) + "[/]", int.MaxValue, colorMode));
        }
    }

    // An aggregate's inner exceptions numbered, any other exception's inner chain one link at a
    // time; each one level deeper. The depth cap keeps a pathological chain from flooding the
    // terminal.
    private static void AddInnerExceptions(List<RenderedLine> lines, Exception exception, string indent, int depth, ColorMode colorMode)
    {
        var innerExceptions = InnerExceptionsOf(exception);
        if (innerExceptions.Count == 0)
            return;

        var innerIndent = indent + IndentPerLevel;
        if (depth + 1 >= MaximumDepth)
        {
            lines.Add(MarkupText.RenderTruncated(innerIndent + "[" + InnerLabelStyle + "]" + ElidedText + "[/]", int.MaxValue, colorMode));
            return;
        }

        for (var index = 0; index < innerExceptions.Count; index++)
        {
            string label;
            if (exception is AggregateException)
                label = "[" + InnerLabelStyle + "]Inner exception " + (index + 1) + " of " + innerExceptions.Count + ":[/] ";
            else
                label = "[" + InnerLabelStyle + "]Caused by:[/] ";

            var innerException = innerExceptions[index];
            AddHeadline(lines, innerException, innerIndent, label, colorMode);
            AddFrames(lines, innerException, innerIndent, colorMode);
            AddInnerExceptions(lines, innerException, innerIndent, depth + 1, colorMode);
        }
    }

    private static IReadOnlyList<Exception> InnerExceptionsOf(Exception exception)
    {
        if (exception is AggregateException aggregateException)
        {
            var innerExceptions = new List<Exception>();
            foreach (var innerException in aggregateException.InnerExceptions)
            {
                if (innerException != null)
                    innerExceptions.Add(innerException);
            }

            return innerExceptions;
        }

        if (exception.InnerException != null)
            return new[] { exception.InnerException };

        return Array.Empty<Exception>();
    }

    private static string[] SplitLines(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return new[] { string.Empty };

        return text.Split(LineBreaks, StringSplitOptions.None);
    }
}
