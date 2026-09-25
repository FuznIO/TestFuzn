namespace Fuzn.TestFuzn.Internals.Terminal;

internal static class ExceptionRenderer
{
    public const int MaximumDepth = 10;

    internal const string ElidedText = "… deeper inner exceptions omitted";

    private const string TypeStyle = "bold " + LiveDashboardLayout.FailedStyle;
    private const string MessageStyle = LiveDashboardLayout.FailedStyle;
    private const string FrameStyle = LiveDashboardLayout.SecondaryStyle;
    private const string InnerLabelStyle = LiveDashboardLayout.WarningStyle;

    private const string IndentPerLevel = "  ";

    private static readonly string[] LineBreaks = { "\r\n", "\n", "\r" };

    public static IReadOnlyList<RenderedLine> Render(Exception exception, ColorMode colorMode)
    {
        if (exception == null)
            throw new ArgumentNullException(nameof(exception), "Exception cannot be null.");

        var lines = new List<RenderedLine>();
        AddException(lines, exception, string.Empty, 0, colorMode);
        return lines;
    }

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

    private static void AddHeadline(List<RenderedLine> lines, Exception exception, string indent, string label, ColorMode colorMode)
    {
        var messageLines = SplitLines(exception.Message);

        var headline = indent + label + "[" + TypeStyle + "]" + MarkupParser.Escape(exception.GetType().Name) + ":[/] [" + MessageStyle + "]" + MarkupParser.Escape(messageLines[0]) + "[/]";
        lines.Add(MarkupText.RenderTruncated(headline, int.MaxValue, colorMode));

        for (var index = 1; index < messageLines.Length; index++)
            lines.Add(MarkupText.RenderTruncated(indent + "[" + MessageStyle + "]" + MarkupParser.Escape(messageLines[index]) + "[/]", int.MaxValue, colorMode));
    }

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
