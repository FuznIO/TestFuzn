using System.Text;

namespace Fuzn.TestFuzn.Internals.Terminal;

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

    public void Render(FrameBuffer frame, int windowWidth, int windowHeight)
    {
        if (frame == null)
            throw new ArgumentNullException(nameof(frame), "Frame cannot be null.");

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
