using System.Net;
using System.Text;

namespace Fuzn.TestFuzn.Internals.Reports;

internal static class BrandHtml
{
    public const string GoogleFontsLinks =
        @"<link rel=""preconnect"" href=""https://fonts.googleapis.com"">"
        + @"<link rel=""preconnect"" href=""https://fonts.gstatic.com"" crossorigin>"
        + @"<link rel=""stylesheet"" href=""https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700&display=swap"">";

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

    // Icon paths shared with Cockpit, drawn on a 24x24 stroke grid.
    private static readonly Dictionary<string, string[]> IconPaths = new()
    {
        ["tests"] = ["M4.6 7.2h14.8", "M4.6 12h14.8", "M4.6 16.8h9.6"],
        ["pulse"] = ["M3 12h3.6l2.4-6 4.2 12 2.4-6H21"],
        ["load"] = ["M3.4 18.4v-5.2", "M8.4 18.4V8.8", "M13.4 18.4v-7.6", "M18.4 18.4V5.2"],
        ["users"] = ["M9 11.4a3.6 3.6 0 1 0 0-7.2 3.6 3.6 0 0 0 0 7.2z", "M2.4 20.4a6.6 6.6 0 0 1 13.2 0", "M16.2 4.4a3.6 3.6 0 0 1 0 6.9", "M18 14.2a6.6 6.6 0 0 1 3.6 6.2"],
        ["list"] = ["M8.4 6.6h12", "M8.4 12h12", "M8.4 17.4h12", "M3.6 6.6h.01", "M3.6 12h.01", "M3.6 17.4h.01"],
        ["settings"] = ["M4.8 20.4V4.8a1.2 1.2 0 0 1 1.2-1.2h7.2a1.2 1.2 0 0 1 1.2 1.2v15.6", "M14.4 9.6h3.6a1.2 1.2 0 0 1 1.2 1.2v9.6", "M3.6 20.4h16.8", "M8.4 7.2h2.4", "M8.4 10.8h2.4", "M8.4 14.4h2.4"],
        ["layers"] = ["M12 3 3 7.5 12 12l9-4.5L12 3Z", "m3 12.5 9 4.5 9-4.5", "m3 17 9 4.5 9-4.5"],
        ["check"] = ["M20 6 9 17l-5-5"],
        ["x"] = ["m7 7 10 10", "m17 7-10 10"],
        ["circle"] = ["M12 18.5a6.5 6.5 0 1 0 0-13 6.5 6.5 0 0 0 0 13Z"],
        ["doc"] = ["M14 3H7a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2V8l-5-5Z", "M14 3v5h5", "M9 13h6", "M9 17h6"],
        ["alert"] = ["M10.3 4.2 2.8 17.5A2 2 0 0 0 4.5 20.5h15a2 2 0 0 0 1.7-3L13.7 4.2a2 2 0 0 0-3.4 0Z", "M12 9.5v4", "M12 17h.01"],
        ["play"] = ["M12 21a9 9 0 1 0 0-18 9 9 0 0 0 0 18Z", "m10 8.5 6 3.5-6 3.5v-7Z"],
        ["clock"] = ["M12 21a9 9 0 1 0 0-18 9 9 0 0 0 0 18Z", "M12 7.5V12l3 1.5"],
        ["hash"] = ["M5 9h14", "M5 15h14", "M10 4 8 20", "M16 4l-2 16"],
        ["server"] = ["M4 5h16v6H4V5Z", "M4 13h16v6H4v-6Z", "M8 8h.01", "M8 16h.01"],
        ["flag"] = ["M5 21V4", "M5 4h11l-2 4 2 4H5"],
        ["tag"] = ["M3.6 12.6V4.6a1 1 0 0 1 1-1h8l7.8 7.8a1.4 1.4 0 0 1 0 2l-6 6a1.4 1.4 0 0 1-2 0z", "M8.4 8.4h.01"],
        ["info"] = ["M12 21a9 9 0 1 0 0-18 9 9 0 0 0 0 18z", "M12 11v5", "M12 8h.01"],
        ["folder"] = ["M3.5 6.5a1.5 1.5 0 0 1 1.5-1.5h4.2l2 2.2H19a1.5 1.5 0 0 1 1.5 1.5v9.3A1.5 1.5 0 0 1 19 19.5H5a1.5 1.5 0 0 1-1.5-1.5z"],
        ["percent"] = ["M18.5 5.5 5.5 18.5", "M7 9.5a2.5 2.5 0 1 0 0-5 2.5 2.5 0 0 0 0 5Z", "M17 19.5a2.5 2.5 0 1 0 0-5 2.5 2.5 0 0 0 0 5Z"],
        ["code"] = ["m8 8-4 4 4 4", "m16 8 4 4-4 4"],
        ["groups"] = ["M3.6 4.8h7.2v6h-7.2z", "M13.2 4.8h7.2v6h-7.2z", "M3.6 13.2h7.2v6h-7.2z", "M13.2 13.2h7.2v6h-7.2z"]
    };

    public static string Icon(string name, int size = 18)
    {
        var b = new StringBuilder();
        b.Append($@"<svg class=""ico"" width=""{size}"" height=""{size}"" viewBox=""0 0 24 24"" aria-hidden=""true"">");
        if (IconPaths.TryGetValue(name, out var paths))
        {
            foreach (var path in paths)
                b.Append($@"<path d=""{path}""/>");
        }
        b.Append("</svg>");
        return b.ToString();
    }

    private static string E(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

    public static void WriteMasthead(StringBuilder b, string assetsPath, string? rightSlotHtml = null)
    {
        b.AppendLine(@"<header class=""masthead"">");
        b.AppendLine(@"<picture class=""testfuzn-logo"">");
        b.AppendLine($@"<source srcset=""{assetsPath}images/testfuzn-logo-dark.png"" media=""(prefers-color-scheme: dark)"">");
        b.AppendLine($@"<img src=""{assetsPath}images/testfuzn-logo.png"" alt=""TestFuzn"" height=""32"">");
        b.AppendLine("</picture>");
        if (!string.IsNullOrEmpty(rightSlotHtml))
            b.AppendLine(rightSlotHtml);
        b.AppendLine("</header>");
    }

    public static void WritePageTitle(StringBuilder b, string icon, string title, string subtitle)
    {
        b.AppendLine(@"<div class=""page-title"">");
        b.AppendLine($@"<span class=""page-icon"">{Icon(icon, 22)}</span>");
        b.AppendLine($@"<div><h1>{E(title)}</h1><p>{E(subtitle)}</p></div>");
        b.AppendLine("</div>");
    }

    public static void CardStart(StringBuilder b, string icon, string title, string? subtitle = null, string? actionsHtml = null, string? extraClass = null)
    {
        b.AppendLine($@"<section class=""card{(extraClass == null ? "" : " " + extraClass)}"">");
        b.AppendLine(@"<div class=""card-head"">");
        b.AppendLine($@"<span class=""card-icon"">{Icon(icon, 20)}</span>");
        b.Append($@"<div class=""card-title""><h3>{E(title)}</h3>");
        if (!string.IsNullOrEmpty(subtitle))
            b.Append($"<p>{E(subtitle)}</p>");
        b.AppendLine("</div>");
        if (!string.IsNullOrEmpty(actionsHtml))
            b.AppendLine($@"<div class=""card-actions"">{actionsHtml}</div>");
        b.AppendLine("</div>");
        b.AppendLine(@"<div class=""card-body"">");
    }

    public static void CardEnd(StringBuilder b)
    {
        b.AppendLine("</div>");
        b.AppendLine("</section>");
    }

    public static void Tile(StringBuilder b, string tone, string icon, string label, string value, string? note = null)
    {
        b.AppendLine(@"<div class=""tile"">");
        b.AppendLine($@"<span class=""badge {tone}"">{Icon(icon, 17)}</span>");
        b.Append($@"<span class=""tile-body""><span class=""tile-label"">{E(label)}</span><span class=""tile-line""><span class=""tile-value"">{E(value)}</span>");
        if (!string.IsNullOrEmpty(note))
            b.Append($@"<span class=""tile-note"">{E(note)}</span>");
        b.AppendLine("</span></span>");
        b.AppendLine("</div>");
    }

    public static void DetailItem(StringBuilder b, string icon, string label, string? value)
    {
        var shown = string.IsNullOrEmpty(value) ? "—" : value;
        b.AppendLine($@"<div class=""detail"" title=""{E(shown)}""><span class=""detail-icon"">{Icon(icon, 15)}</span>"
            + $@"<span class=""detail-text""><span class=""detail-label"">{E(label)}</span><span class=""detail-value{(string.IsNullOrEmpty(value) ? " none" : "")}"">{E(shown)}</span></span></div>");
    }

    public static string Pill(string tone, string text) => $@"<span class=""spill {tone}"">{E(text)}</span>";

    public static string StatusPill(TestStatus status) =>
        status switch
        {
            TestStatus.Passed => Pill("ok", "Passed"),
            TestStatus.Failed => Pill("bad", "Failed"),
            TestStatus.Skipped => Pill("neu", "Skipped"),
            _ => string.Empty
        };

    public static string StepStatusPill(StepStatus status) =>
        status switch
        {
            StepStatus.Passed => Pill("ok", "Passed"),
            StepStatus.Failed => Pill("bad", "Failed"),
            StepStatus.Skipped => Pill("neu", "Skipped"),
            _ => string.Empty
        };

    public static void WriteProductSignature(StringBuilder b)
    {
        b.AppendLine(@"<footer class=""product-signature"">");
        b.AppendLine(@"<a href=""https://fuznlabs.com"" target=""_blank"" rel=""noopener"">TestFuzn · a Fuzn product</a>");
        b.AppendLine("</footer>");
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
            StepStatus.Skipped => $@"<span class=""status-icon skipped"">{AlertTriangleSvg}</span>",
            _ => string.Empty
        };
}
