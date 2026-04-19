using System.Text;

namespace Fuzn.TestFuzn.Internals.Reports;

internal static class BrandHtml
{
    public const string GoogleFontsLinks =
        @"<link rel=""preconnect"" href=""https://fonts.googleapis.com"">"
        + @"<link rel=""preconnect"" href=""https://fonts.gstatic.com"" crossorigin>"
        + @"<link rel=""stylesheet"" href=""https://fonts.googleapis.com/css2?family=Outfit:wght@400;500;600;700&family=DM+Sans:wght@400;500&family=DM+Mono:wght@400;500&display=swap"">";

    public const string CheckCircleSvg =
        @"<svg viewBox=""0 0 24 24"" xmlns=""http://www.w3.org/2000/svg"">"
        + @"<path d=""M22 11.08V12a10 10 0 1 1-5.93-9.14""/>"
        + @"<polyline points=""22 4 12 14.01 9 11.01""/>"
        + @"</svg>";

    public const string XCircleSvg =
        @"<svg viewBox=""0 0 24 24"" xmlns=""http://www.w3.org/2000/svg"">"
        + @"<circle cx=""12"" cy=""12"" r=""10""/>"
        + @"<line x1=""15"" y1=""9"" x2=""9"" y2=""15""/>"
        + @"<line x1=""9"" y1=""9"" x2=""15"" y2=""15""/>"
        + @"</svg>";

    public const string AlertTriangleSvg =
        @"<svg viewBox=""0 0 24 24"" xmlns=""http://www.w3.org/2000/svg"">"
        + @"<path d=""M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z""/>"
        + @"<line x1=""12"" y1=""9"" x2=""12"" y2=""13""/>"
        + @"<line x1=""12"" y1=""17"" x2=""12.01"" y2=""17""/>"
        + @"</svg>";

    public const string MinusCircleSvg =
        @"<svg viewBox=""0 0 24 24"" xmlns=""http://www.w3.org/2000/svg"">"
        + @"<circle cx=""12"" cy=""12"" r=""10""/>"
        + @"<line x1=""8"" y1=""12"" x2=""16"" y2=""12""/>"
        + @"</svg>";

    public const string InfoCircleSvg =
        @"<svg viewBox=""0 0 24 24"" xmlns=""http://www.w3.org/2000/svg"">"
        + @"<circle cx=""12"" cy=""12"" r=""10""/>"
        + @"<line x1=""12"" y1=""16"" x2=""12"" y2=""12""/>"
        + @"<line x1=""12"" y1=""8"" x2=""12.01"" y2=""8""/>"
        + @"</svg>";

    private const string TestFuznMarkSvg =
        @"<svg width=""36"" height=""26"" viewBox=""0 0 88 64"" fill=""none"" xmlns=""http://www.w3.org/2000/svg"">"
        + @"<path d=""M 0,6 C 22,6 30,32 44,32"" stroke=""currentColor"" stroke-width=""3"" stroke-linecap=""round"" fill=""none"" opacity=""0.25""/>"
        + @"<path d=""M 0,32 C 16,32 28,32 44,32"" stroke=""currentColor"" stroke-width=""3"" stroke-linecap=""round"" fill=""none"" opacity=""0.25""/>"
        + @"<path d=""M 0,58 C 22,58 30,32 44,32"" stroke=""currentColor"" stroke-width=""3"" stroke-linecap=""round"" fill=""none"" opacity=""0.25""/>"
        + @"<line x1=""44"" y1=""32"" x2=""88"" y2=""32"" stroke=""currentColor"" stroke-width=""3.5"" stroke-linecap=""round""/>"
        + @"<circle cx=""44"" cy=""32"" r=""8"" fill=""currentColor""/>"
        + @"</svg>";

    public static void WriteMasthead(StringBuilder b, string? rightSlotHtml = null)
    {
        b.AppendLine(@"<div class=""masthead"">");
        b.AppendLine(@"<div class=""testfuzn-logo"" style=""color: var(--brand-accent);"">");
        b.AppendLine(TestFuznMarkSvg);
        b.AppendLine(@"<span class=""wordmark"">TestFuzn</span>");
        b.AppendLine("</div>");
        if (!string.IsNullOrEmpty(rightSlotHtml))
            b.AppendLine(rightSlotHtml);
        b.AppendLine("</div>");
    }

    public static void WriteProductSignature(StringBuilder b)
    {
        b.AppendLine(@"<div class=""product-signature"">");
        b.AppendLine(@"<span class=""dot""></span>TestFuzn · a Fuzn product");
        b.AppendLine("</div>");
    }

    public static string TestStatusIcon(TestStatus status) =>
        status switch
        {
            TestStatus.Passed  => $@"<span class=""status-icon passed"">{CheckCircleSvg}</span>",
            TestStatus.Failed  => $@"<span class=""status-icon failed"">{XCircleSvg}</span>",
            TestStatus.Skipped => $@"<span class=""status-icon skipped"">{MinusCircleSvg}</span>",
            _ => string.Empty
        };

    public static string TestStatusIconLarge(TestStatus status) =>
        status switch
        {
            TestStatus.Passed  => $@"<span class=""status-icon lg passed"">{CheckCircleSvg}</span>",
            TestStatus.Failed  => $@"<span class=""status-icon lg failed"">{XCircleSvg}</span>",
            TestStatus.Skipped => $@"<span class=""status-icon lg warning"">{AlertTriangleSvg}</span>",
            _ => string.Empty
        };

    public static string InfoIconLarge() => $@"<span class=""status-icon lg info"">{InfoCircleSvg}</span>";

    public static string StepStatusIcon(StepStatus status) =>
        status switch
        {
            StepStatus.Passed  => $@"<span class=""status-icon passed"">{CheckCircleSvg}</span>",
            StepStatus.Failed  => $@"<span class=""status-icon failed"">{XCircleSvg}</span>",
            StepStatus.Skipped => $@"<span class=""status-icon warning"">{AlertTriangleSvg}</span>",
            _ => string.Empty
        };
}
