namespace Fuzn.TestFuzn.Internals.Terminal;

internal sealed class LoadViewHeader
{
    internal const string Wordmark = "TestFuzn";
    internal const string ExecutionEnvironmentLabel = "Execution Environment:";
    internal const string TargetEnvironmentLabel = "Target Environment:";
    internal const string UnsetValueText = "-";

    public static readonly LoadViewHeader Empty = new LoadViewHeader();

    public string ExecutionEnvironment { get; init; } = string.Empty;

    public string TargetEnvironment { get; init; } = string.Empty;

    public IReadOnlyList<RenderedLine> Render(int width, ColorMode colorMode)
    {
        var environments = "[" + LiveDashboardLayout.SecondaryStyle + "]" + ExecutionEnvironmentLabel + "[/] " + TextOrUnset(ExecutionEnvironment)
            + "    [" + LiveDashboardLayout.SecondaryStyle + "]" + TargetEnvironmentLabel + "[/] " + TextOrUnset(TargetEnvironment);

        var lines = new List<RenderedLine>(2);
        lines.AddRange(LogoWidget.Render(width, LogoWidget.CompactHeight, colorMode));
        lines.Add(MarkupText.RenderTruncated(environments, width, colorMode));
        return lines;
    }

    private static string TextOrUnset(string value)
    {
        if (string.IsNullOrEmpty(value))
            return UnsetValueText;

        return MarkupParser.Escape(value);
    }
}
