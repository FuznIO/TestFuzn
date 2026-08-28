using System.Text;

namespace Fuzn.TestFuzn.Internals.Terminal;

/// <summary>
/// Renders frames to the terminal by diffing each frame against the previously rendered one and
/// repainting only the lines that changed, each flush wrapped in DEC synchronized output so the
/// terminal applies it atomically. A change in the window size passed to <see cref="Render"/>
/// (or <see cref="Reset"/>) forces a full redraw; frames taller than the passed height are
/// clipped to it before diffing. Every full redraw starts by disabling terminal auto-wrap
/// (DECAWM), and every erase is preceded by <see cref="AnsiCodes.Reset"/>, so rendering is
/// independent of widget style discipline. Each frame line must fit the width it was laid out
/// for: beyond the DECAWM defense (an overlong line overwrites its last column instead of
/// wrapping into the next row) the renderer does not wrap-protect. Rendering primitives only:
/// the render loop, alternate screen, and cursor visibility are owned by the caller, which also
/// gates on <see cref="TerminalCapabilities"/> and should restore auto-wrap
/// (<see cref="AnsiCodes.EnableAutoWrap"/>) when leaving the alternate screen. Designed to be
/// driven by a single render loop; not thread-safe.
/// </summary>
internal sealed class FrameRenderer
{
    private readonly ITerminalWriter _writer;

    private string[]? _previousLines;
    private int _previousWindowWidth;
    private int _previousWindowHeight;

    public FrameRenderer(ITerminalWriter writer)
    {
        if (writer == null)
            throw new ArgumentNullException(nameof(writer), "Writer cannot be null.");

        _writer = writer;
    }

    /// <summary>
    /// Renders the frame: a full redraw (disable auto-wrap + reset styling + erase screen +
    /// every line) on the first render, after <see cref="Reset"/>, or when the passed window
    /// size differs from the previously passed size; otherwise a diff that repaints only changed
    /// lines (cursor address + reset styling + erase-line + content) and erases leftover lines
    /// when the new frame is shorter than the previous one. An unchanged frame writes nothing at
    /// all. At most one write is emitted per call. Pass the size the frame was laid out for; one
    /// size reading per frame, done by the caller — the renderer never reads the writer's
    /// dimensions, so layout and diff always agree on the size even if the terminal resizes
    /// between the caller's reading and the render.
    /// </summary>
    public void Render(FrameBuffer frame, int windowWidth, int windowHeight)
    {
        if (frame == null)
            throw new ArgumentNullException(nameof(frame), "Frame cannot be null.");

        // Snapshot the clipped lines so later mutation of the frame buffer cannot corrupt the diff state.
        var lines = ClipToWindowHeight(frame.Lines, windowHeight);

        var output = new StringBuilder();
        if (_previousLines == null || windowWidth != _previousWindowWidth || windowHeight != _previousWindowHeight)
            AppendFullRedraw(output, lines);
        else
            AppendChangedLines(output, lines, _previousLines);

        if (output.Length > 0)
            _writer.Write(AnsiCodes.BeginSynchronizedOutput + output.ToString() + AnsiCodes.EndSynchronizedOutput);

        _previousLines = lines;
        _previousWindowWidth = windowWidth;
        _previousWindowHeight = windowHeight;
    }

    /// <summary>
    /// Forgets the previously rendered frame so the next <see cref="Render"/> performs a full
    /// redraw, e.g. after other output has drawn over the screen.
    /// </summary>
    public void Reset()
    {
        _previousLines = null;
    }

    private static string[] ClipToWindowHeight(IReadOnlyList<string> lines, int windowHeight)
    {
        var visibleLineCount = Math.Min(lines.Count, Math.Max(windowHeight, 0));

        var clippedLines = new string[visibleLineCount];
        for (var row = 0; row < visibleLineCount; row++)
            clippedLines[row] = lines[row];

        return clippedLines;
    }

    private static void AppendFullRedraw(StringBuilder output, string[] lines)
    {
        output.Append(AnsiCodes.DisableAutoWrap);
        output.Append(AnsiCodes.Reset);
        output.Append(AnsiCodes.EraseScreen);

        for (var row = 0; row < lines.Length; row++)
        {
            output.Append(AnsiCodes.MoveCursor(row + 1, 1));
            output.Append(lines[row]);
        }
    }

    private static void AppendChangedLines(StringBuilder output, string[] lines, string[] previousLines)
    {
        for (var row = 0; row < lines.Length; row++)
        {
            if (row < previousLines.Length && previousLines[row] == lines[row])
                continue;

            output.Append(AnsiCodes.MoveCursor(row + 1, 1));
            output.Append(AnsiCodes.Reset);
            output.Append(AnsiCodes.EraseLine);
            output.Append(lines[row]);
        }

        for (var row = lines.Length; row < previousLines.Length; row++)
        {
            output.Append(AnsiCodes.MoveCursor(row + 1, 1));
            output.Append(AnsiCodes.Reset);
            output.Append(AnsiCodes.EraseLine);
        }
    }
}
