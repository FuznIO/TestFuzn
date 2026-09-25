using System.Text;

namespace Fuzn.TestFuzn.Internals.Terminal;

internal static class LogoWidget
{
    public const int MinimumWidthForLogo = 80;

    public const int BannerWidth = 69;

    public const int BannerHeight = 6;

    public const int CompactWidth = 11;

    public const int CompactHeight = 1;

    private static readonly TerminalColor GradientStart = TerminalColor.FromRgb(0xFF, 0x5C, 0x00);
    private static readonly TerminalColor GradientEnd = TerminalColor.FromRgb(0xFF, 0xCF, 0x6B);

    private static readonly string Colors16Accent = AnsiCodes.Foreground(ConsoleColor.Yellow);

    private const string Lightning = "⚡";
    private const string Wordmark = "TestFuzn";

    private static readonly string[] BannerArt =
    {
        "████████╗███████╗███████╗████████╗███████╗██╗   ██╗███████╗███╗   ██╗",
        "╚══██╔══╝██╔════╝██╔════╝╚══██╔══╝██╔════╝██║   ██║╚══███╔╝████╗  ██║",
        "   ██║   █████╗  ███████╗   ██║   █████╗  ██║   ██║  ███╔╝ ██╔██╗ ██║",
        "   ██║   ██╔══╝  ╚════██║   ██║   ██╔══╝  ██║   ██║ ███╔╝  ██║╚██╗██║",
        "   ██║   ███████╗███████║   ██║   ██║     ╚██████╔╝███████╗██║ ╚████║",
        "   ╚═╝   ╚══════╝╚══════╝   ╚═╝   ╚═╝      ╚═════╝ ╚══════╝╚═╝  ╚═══╝"
    };

    public static LogoVariant SelectVariant(int availableWidth, int availableHeight)
    {
        if (availableWidth >= BannerWidth && availableHeight >= BannerHeight)
            return LogoVariant.Banner;

        if (availableWidth >= CompactWidth && availableHeight >= CompactHeight)
            return LogoVariant.Compact;

        return LogoVariant.None;
    }

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
