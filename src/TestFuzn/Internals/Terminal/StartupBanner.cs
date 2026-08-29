namespace Fuzn.TestFuzn.Internals.Terminal;

/// <summary>
/// The three lines the standalone runner writes to the normal screen buffer when a run is about
/// to start — a test named on the command line or picked in the selection menu, or the live
/// view demo — so that what ran stays on record in the scrollback above the live view and the
/// summary: the compact logo (⚡ TestFuzn) on the first line, a title line made of a label and
/// the run's subject ("Running test:" and the test's full name), and a detail line with the
/// run's context (the test assembly and the target environment; for the demo, that its load is
/// synthetic). Plain text in, styled lines out: every input is escaped so brackets in a name
/// render literally, and control characters sanitize to spaces as everywhere in the engine.
/// Nothing is truncated or wrapped here — a name is what the reader copies from the scrollback,
/// so it is written whole and the terminal wraps it; the returned lines declare their full
/// width. The layout is fixed and compact, so it needs no terminal size and is written on any
/// output — redirected, non-ANSI, dumb — where <see cref="ColorMode.None"/> renders the same
/// three lines as plain text with zero escape bytes. The ⚡ stays in that mode, as
/// <see cref="LogoWidget"/> keeps it: the runner writes UTF-8, the stats lines and the summary
/// already carry non-ASCII glyphs on a redirected output, and the engine has no glyph-support
/// detection that could decide otherwise. The label takes the dashboard's warm accent, the
/// subject is bold and the detail line is dimmed. The logo is the compact wordmark by design —
/// <see cref="LogoVariant.Banner"/>, the six-row logo the selection menu shows, is a different
/// thing despite the shared word. Stateless and thread-safe.
/// </summary>
internal static class StartupBanner
{
    /// <summary>Row count of the banner: the logo, the title line and the detail line.</summary>
    public const int Height = 3;

    // The banner's palette as markup style constants, in the dashboard's warm family: the label
    // in the panel-header accent (#ff9d3d, downgraded to bright yellow in 16-color mode, as the
    // logo's accent is), the subject bold, the detail dimmed.
    private const string LabelStyle = "bold #ff9d3d";
    private const string SubjectStyle = "bold";
    private const string DetailStyle = "dim";

    /// <summary>
    /// Renders the banner's <see cref="Height"/> lines: the compact logo, then
    /// "<paramref name="label"/> <paramref name="subject"/>", then <paramref name="detail"/>.
    /// The texts are plain — brackets render literally — and are never truncated.
    /// </summary>
    public static IReadOnlyList<RenderedLine> Render(string label, string subject, string detail, ColorMode colorMode)
    {
        if (label == null)
            throw new ArgumentNullException(nameof(label), "Label cannot be null.");
        if (subject == null)
            throw new ArgumentNullException(nameof(subject), "Subject cannot be null.");
        if (detail == null)
            throw new ArgumentNullException(nameof(detail), "Detail cannot be null.");

        var titleMarkup = "[" + LabelStyle + "]" + MarkupParser.Escape(label) + "[/] [" + SubjectStyle + "]" + MarkupParser.Escape(subject) + "[/]";
        var detailMarkup = "[" + DetailStyle + "]" + MarkupParser.Escape(detail) + "[/]";

        // Rendering through MarkupText sanitizes control characters; the unbounded width is
        // what keeps a long name whole — the banner never truncates.
        return new[]
        {
            LogoWidget.Render(LogoWidget.CompactWidth, LogoWidget.CompactHeight, colorMode)[0],
            MarkupText.RenderTruncated(titleMarkup, int.MaxValue, colorMode),
            MarkupText.RenderTruncated(detailMarkup, int.MaxValue, colorMode)
        };
    }

    /// <summary>
    /// Writes the banner <see cref="Render"/> produces to the writer, one write per line, each
    /// ending in a line break — as the plain stats lines are written. The writer is only ever
    /// written to: its size is never read, so a redirected output is fine.
    /// </summary>
    public static void Write(ITerminalWriter writer, string label, string subject, string detail, ColorMode colorMode)
    {
        if (writer == null)
            throw new ArgumentNullException(nameof(writer), "Writer cannot be null.");

        foreach (var line in Render(label, subject, detail, colorMode))
            writer.Write(line.Text + Environment.NewLine);
    }
}
