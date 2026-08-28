using System.Text;

namespace Fuzn.TestFuzn.Internals.Terminal;

/// <summary>
/// Renders the TestFuzn logo in the largest variant that fits the available space: the full
/// six-row ANSI-shadow banner, else the one-row compact wordmark (⚡ TestFuzn), else nothing.
/// <see cref="SelectVariant"/> answers which variant fits without rendering, and the variant
/// dimension constants let layouts reserve space up front. <see cref="ColorMode.None"/> renders
/// either variant as plain art with zero escape bytes. Banner: <see cref="ColorMode.TrueColor"/>
/// sweeps a warm horizontal gradient — each column's glyphs take a foreground interpolated from
/// deep orange at the left edge to warm amber at the right — <see cref="ColorMode.Colors16"/>
/// renders every glyph in the single bright-yellow accent, and <see cref="ColorMode.Monochrome"/>
/// renders every glyph bold. Compact: the ⚡ takes the accent (gradient-start orange in
/// TrueColor, yellow in Colors16, unstyled in Monochrome) while the wordmark renders bold in the
/// default foreground — so TrueColor and Colors16 bold the compact wordmark even though the
/// banner never uses bold in those modes. TrueColor colors glyphs only, leaving space runs bare;
/// Monochrome and Colors16 style whole banner rows, spaces included — harmless while those
/// styles set no background or reverse video (bold and a bare foreground leave blank cells
/// blank), but retheming either mode with a background or reverse video would make the space
/// runs visible. Every styled row is Reset-closed, so each returned line is independently
/// style-complete. Stateless and thread-safe.
/// </summary>
internal static class LogoWidget
{
    /// <summary>Display width in columns of the full banner variant.</summary>
    public const int BannerWidth = 69;

    /// <summary>Row count of the full banner variant.</summary>
    public const int BannerHeight = 6;

    /// <summary>
    /// Display width in columns of the compact variant: the two-column ⚡ plus a space plus the
    /// eight-character wordmark. ⚡ (U+26A1) draws two columns wide on emoji-presentation
    /// terminals even though the framework's character-per-column rule would count one, so the
    /// declared width counts it as two — a terminal that draws it single-width under-fills by
    /// one spare column instead of overflowing.
    /// </summary>
    public const int CompactWidth = 11;

    /// <summary>Row count of the compact variant.</summary>
    public const int CompactHeight = 1;

    // Warm gradient endpoints (omarchy-style): deep orange #FF5C00 at the banner's left edge
    // sweeping to warm amber #FFCF6B at the right. Retheme the logo by tweaking these two.
    private static readonly TerminalColor GradientStart = TerminalColor.FromRgb(0xFF, 0x5C, 0x00);
    private static readonly TerminalColor GradientEnd = TerminalColor.FromRgb(0xFF, 0xCF, 0x6B);

    // The single warm accent for Colors16 mode: bright yellow (SGR 93).
    private static readonly string Colors16Accent = AnsiCodes.Foreground(ConsoleColor.Yellow);

    private const string Lightning = "⚡";
    private const string Wordmark = "TestFuzn";

    // "TestFuzn" in the ANSI-shadow figlet style: a perfect BannerWidth×BannerHeight rectangle
    // (every row exactly BannerWidth single-width box/block glyphs, space-padded).
    private static readonly string[] BannerArt =
    {
        "████████╗███████╗███████╗████████╗███████╗██╗   ██╗███████╗███╗   ██╗",
        "╚══██╔══╝██╔════╝██╔════╝╚══██╔══╝██╔════╝██║   ██║╚══███╔╝████╗  ██║",
        "   ██║   █████╗  ███████╗   ██║   █████╗  ██║   ██║  ███╔╝ ██╔██╗ ██║",
        "   ██║   ██╔══╝  ╚════██║   ██║   ██╔══╝  ██║   ██║ ███╔╝  ██║╚██╗██║",
        "   ██║   ███████╗███████║   ██║   ██║     ╚██████╔╝███████╗██║ ╚████║",
        "   ╚═╝   ╚══════╝╚══════╝   ╚═╝   ╚═╝      ╚═════╝ ╚══════╝╚═╝  ╚═══╝"
    };

    /// <summary>
    /// Returns the largest variant that fits the available space, without rendering: the banner
    /// when both its width and height fit, else the compact wordmark when it fits, else none.
    /// </summary>
    public static LogoVariant SelectVariant(int availableWidth, int availableHeight)
    {
        if (availableWidth >= BannerWidth && availableHeight >= BannerHeight)
            return LogoVariant.Banner;

        if (availableWidth >= CompactWidth && availableHeight >= CompactHeight)
            return LogoVariant.Compact;

        return LogoVariant.None;
    }

    /// <summary>
    /// Renders the variant <see cref="SelectVariant"/> picks for the available space; when
    /// nothing fits, renders no lines.
    /// </summary>
    public static IReadOnlyList<RenderedLine> Render(int availableWidth, int availableHeight, ColorMode colorMode)
    {
        var variant = SelectVariant(availableWidth, availableHeight);
        if (variant == LogoVariant.Banner)
            return RenderBanner(colorMode);

        if (variant == LogoVariant.Compact)
            return new[] { RenderCompact(colorMode) };

        return Array.Empty<RenderedLine>();
    }

    private static IReadOnlyList<RenderedLine> RenderBanner(ColorMode colorMode)
    {
        var lines = new RenderedLine[BannerHeight];
        for (var row = 0; row < BannerHeight; row++)
            lines[row] = new RenderedLine(StyleBannerRow(BannerArt[row], colorMode), BannerWidth);

        return lines;
    }

    private static string StyleBannerRow(string artRow, ColorMode colorMode)
    {
        switch (colorMode)
        {
            case ColorMode.None: return artRow;
            case ColorMode.Monochrome: return AnsiCodes.Bold + artRow + AnsiCodes.Reset;
            case ColorMode.Colors16: return Colors16Accent + artRow + AnsiCodes.Reset;
            case ColorMode.TrueColor: return RenderGradientRow(artRow);
            default: throw new ArgumentOutOfRangeException(nameof(colorMode), colorMode, "Unknown color mode.");
        }
    }

    // Colors one art row for TrueColor: every glyph takes the foreground interpolated for its
    // column, emitted per character (the logo renders once per frame and unchanged rows cost
    // nothing in the frame diff), while space runs stay bare to keep the bytes down.
    private static string RenderGradientRow(string artRow)
    {
        var row = new StringBuilder(artRow.Length * 20);
        for (var column = 0; column < artRow.Length; column++)
        {
            var glyph = artRow[column];
            if (glyph == ' ')
            {
                row.Append(' ');
                continue;
            }

            var position = (double)column / (BannerWidth - 1);
            row.Append(AnsiCodes.ForegroundTrueColor(
                InterpolateChannel(GradientStart.Red, GradientEnd.Red, position),
                InterpolateChannel(GradientStart.Green, GradientEnd.Green, position),
                InterpolateChannel(GradientStart.Blue, GradientEnd.Blue, position)));
            row.Append(glyph);
        }

        return row.Append(AnsiCodes.Reset).ToString();
    }

    private static byte InterpolateChannel(byte start, byte end, double position)
    {
        return (byte)Math.Round(start + ((end - start) * position), MidpointRounding.AwayFromZero);
    }

    private static RenderedLine RenderCompact(ColorMode colorMode)
    {
        switch (colorMode)
        {
            case ColorMode.None:
                return new RenderedLine(Lightning + " " + Wordmark, CompactWidth);
            case ColorMode.Monochrome:
                return new RenderedLine(Lightning + " " + AnsiCodes.Bold + Wordmark + AnsiCodes.Reset, CompactWidth);
            case ColorMode.Colors16:
                return new RenderedLine(Colors16Accent + Lightning + AnsiCodes.Reset + " " + AnsiCodes.Bold + Wordmark + AnsiCodes.Reset, CompactWidth);
            case ColorMode.TrueColor:
                return new RenderedLine(
                    AnsiCodes.ForegroundTrueColor(GradientStart.Red, GradientStart.Green, GradientStart.Blue) + Lightning + AnsiCodes.Reset
                        + " " + AnsiCodes.Bold + Wordmark + AnsiCodes.Reset,
                    CompactWidth);
            default:
                throw new ArgumentOutOfRangeException(nameof(colorMode), colorMode, "Unknown color mode.");
        }
    }
}
