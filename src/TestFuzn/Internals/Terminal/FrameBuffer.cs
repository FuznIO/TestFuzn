namespace Fuzn.TestFuzn.Internals.Terminal;

/// <summary>
/// A frame of styled lines built in memory before rendering. Each line is a fully rendered
/// string with any SGR styling already embedded; <see cref="FrameRenderer"/> diffs whole lines
/// by string equality, so two lines differing only in styling count as changed. Each line must
/// fit the window width the frame was laid out for: the renderer disables terminal auto-wrap as
/// a defense (an overlong line overwrites its last column instead of wrapping into the next
/// row) but does not otherwise wrap-protect.
/// </summary>
internal sealed class FrameBuffer
{
    private readonly List<string> _lines = new();

    /// <summary>The lines of the frame, top to bottom.</summary>
    public IReadOnlyList<string> Lines => _lines;

    /// <summary>Appends one line to the bottom of the frame.</summary>
    public void AddLine(string line)
    {
        if (line == null)
            throw new ArgumentNullException(nameof(line), "Line cannot be null.");

        _lines.Add(line);
    }

    /// <summary>Appends multiple lines to the bottom of the frame, e.g. a widget's output.</summary>
    public void AddLines(IEnumerable<string> lines)
    {
        if (lines == null)
            throw new ArgumentNullException(nameof(lines), "Lines cannot be null.");

        foreach (var line in lines)
            AddLine(line);
    }

    /// <summary>Removes all lines so the buffer can be reused to build the next frame.</summary>
    public void Clear()
    {
        _lines.Clear();
    }
}
