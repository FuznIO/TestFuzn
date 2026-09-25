namespace Fuzn.TestFuzn.Internals.Terminal;

internal sealed class FrameBuffer
{
    private static readonly char[] LineBreakCharacters = { '\r', '\n' };

    private readonly List<string> _lines = new();

    public IReadOnlyList<string> Lines => _lines;

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

    public void AddLines(IEnumerable<string> lines)
    {
        if (lines == null)
            throw new ArgumentNullException(nameof(lines), "Lines cannot be null.");

        foreach (var line in lines)
            AddLine(line);
    }

    public void AddLines(IEnumerable<RenderedLine> lines)
    {
        if (lines == null)
            throw new ArgumentNullException(nameof(lines), "Lines cannot be null.");

        foreach (var line in lines)
            _lines.Add(line.Text);
    }

    public void Clear()
    {
        _lines.Clear();
    }
}
