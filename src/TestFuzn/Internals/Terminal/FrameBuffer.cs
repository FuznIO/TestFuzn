namespace Fuzn.TestFuzn.Internals.Terminal;

/// <summary>
/// A frame of styled lines built in memory before rendering. One stored line is one terminal
/// row by construction: <see cref="AddLine"/> splits text containing line breaks into one entry
/// per physical line, and <see cref="MarkupRenderer"/> never lets styling span a line break, so
/// every stored row is independently style-complete. Each line is a fully rendered string with
/// any SGR styling already embedded; <see cref="FrameRenderer"/> diffs whole lines by string
/// equality, so two lines differing only in styling count as changed. Each line must fit the
/// window width the frame was laid out for: the renderer disables terminal auto-wrap as a
/// defense (an overlong line overwrites its last column instead of wrapping into the next row)
/// but does not otherwise wrap-protect.
/// </summary>
internal sealed class FrameBuffer
{
    private static readonly char[] LineBreakCharacters = { '\r', '\n' };

    private readonly List<string> _lines = new();

    /// <summary>The lines of the frame, top to bottom, one entry per terminal row.</summary>
    public IReadOnlyList<string> Lines => _lines;

    /// <summary>
    /// Appends text to the bottom of the frame, splitting on line breaks (\r\n, \n or a lone
    /// \r) so that one stored entry is one terminal row. A trailing line break terminates the
    /// final line without opening an empty one, so "a\r\n" stores one entry while "a\n\nb"
    /// stores three; text without a line break, including an empty string, stores one entry.
    /// </summary>
    public void AddLine(string line)
    {
        if (line == null)
            throw new ArgumentNullException(nameof(line), "Line cannot be null.");

        var position = 0;
        while (true)
        {
            var breakIndex = line.IndexOfAny(LineBreakCharacters, position);
            if (breakIndex < 0)
            {
                if (position == 0 || position < line.Length)
                    _lines.Add(line.Substring(position));

                return;
            }

            _lines.Add(line.Substring(position, breakIndex - position));

            var breakLength = 1;
            if (line[breakIndex] == '\r' && breakIndex + 1 < line.Length && line[breakIndex + 1] == '\n')
                breakLength = 2;

            position = breakIndex + breakLength;
        }
    }

    /// <summary>
    /// Appends multiple plain lines to the bottom of the frame. Each line is split the same way
    /// <see cref="AddLine"/> splits.
    /// </summary>
    public void AddLines(IEnumerable<string> lines)
    {
        if (lines == null)
            throw new ArgumentNullException(nameof(lines), "Lines cannot be null.");

        foreach (var line in lines)
            AddLine(line);
    }

    /// <summary>
    /// Appends a widget's pre-rendered lines to the bottom of the frame. A
    /// <see cref="RenderedLine"/> is break-free by construction, so each one is stored as-is —
    /// one entry per line, no split scan.
    /// </summary>
    public void AddLines(IEnumerable<RenderedLine> lines)
    {
        if (lines == null)
            throw new ArgumentNullException(nameof(lines), "Lines cannot be null.");

        foreach (var line in lines)
            _lines.Add(line.Text);
    }

    /// <summary>Removes all lines so the buffer can be reused to build the next frame.</summary>
    public void Clear()
    {
        _lines.Clear();
    }
}
