namespace Fuzn.TestFuzn.Internals.Terminal;

internal static class FailureDetailsLayout
{
    internal const string Header = "Failure Details";
    internal const string InnerExceptionLabel = "Inner Exception:";
    internal const string NoAssertionFailureText = "No assertion failed. The scenario is marked failed by its requests or steps; see Step Performance and Step Errors.";

    private const string Indent = "  ";

    public static List<string?> Lines(Exception? whileWarmingUp, Exception? whileRunning, Exception? whenDone, TestStatus status, string failedText, int width)
    {
        var lines = new List<string?>();
        AddFailure(lines, "AssertWhileWarmingUp Failed", whileWarmingUp, width);
        AddFailure(lines, "AssertWhileRunning Failed", whileRunning, width);
        AddFailure(lines, "AssertWhenDone Failed", whenDone, width);

        if (lines.Count == 0 && status == TestStatus.Failed)
        {
            lines.Add("[bold " + LiveDashboardLayout.FailedStyle + "]" + failedText + "[/]");
            AddWrapped(lines, NoAssertionFailureText, width);
        }

        return lines;
    }

    private static void AddFailure(List<string?> lines, string title, Exception? exception, int width)
    {
        if (exception == null)
            return;

        if (lines.Count > 0)
            lines.Add(string.Empty);

        lines.Add("[bold " + LiveDashboardLayout.FailedStyle + "]" + title + "[/]");
        AddWrapped(lines, exception.GetType().FullName + ": " + exception.Message, width);

        if (exception.InnerException != null)
        {
            lines.Add("[" + LiveDashboardLayout.SecondaryStyle + "]" + InnerExceptionLabel + "[/]");
            AddWrapped(lines, exception.InnerException.GetType().FullName + ": " + exception.InnerException.Message, width);
        }
    }

    private static void AddWrapped(List<string?> lines, string text, int width)
    {
        var sanitized = MarkupText.SanitizeControlCharacters(text);
        foreach (var wrapped in LoadSummaryLayout.WrapText(sanitized, width - Indent.Length))
            lines.Add("[" + LiveDashboardLayout.FailedStyle + "]" + Indent + MarkupParser.Escape(wrapped) + "[/]");
    }
}
