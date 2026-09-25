namespace Fuzn.TestFuzn.Internals.Terminal;

internal static class StartupBanner
{
    public const int Height = 1;

    private const string LabelStyle = "bold #ff9d3d";
    private const string SubjectStyle = "bold";

    public static IReadOnlyList<RenderedLine> Render(string label, string subject, ColorMode colorMode)
    {
        if (label == null)
            throw new ArgumentNullException(nameof(label), "Label cannot be null.");
        if (subject == null)
            throw new ArgumentNullException(nameof(subject), "Subject cannot be null.");

        var titleMarkup = "[" + LabelStyle + "]" + MarkupParser.Escape(label) + "[/] [" + SubjectStyle + "]" + MarkupParser.Escape(subject) + "[/]";
        return new[] { MarkupText.RenderTruncated(titleMarkup, int.MaxValue, colorMode) };
    }

    public static void Write(ITerminalWriter writer, string label, string subject, ColorMode colorMode)
    {
        if (writer == null)
            throw new ArgumentNullException(nameof(writer), "Writer cannot be null.");

        foreach (var line in Render(label, subject, colorMode))
            writer.Write(line.Text + Environment.NewLine);
    }
}
