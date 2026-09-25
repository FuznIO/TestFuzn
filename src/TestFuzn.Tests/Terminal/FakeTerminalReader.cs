using System.Collections.Concurrent;
using Fuzn.TestFuzn.Internals.Terminal;

namespace Fuzn.TestFuzn.Tests.Terminal;

internal sealed class FakeTerminalReader : ITerminalReader
{
    private readonly ConcurrentQueue<ConsoleKeyInfo> _keys = new();
    private readonly ConcurrentQueue<string> _lines = new();
    private int _tryReadKeyCallCount;
    private int _readLineCallCount;

    public int TryReadKeyCallCount => Volatile.Read(ref _tryReadKeyCallCount);

    public int ReadLineCallCount => Volatile.Read(ref _readLineCallCount);

    public int PendingKeyCount => _keys.Count;

    public int PendingLineCount => _lines.Count;

    public Exception? ReadFailure { get; set; }

    public ManualResetEventSlim? ReadLineGate { get; set; }

    public void Press(char keyChar)
    {
        if (!char.IsAsciiLetterOrDigit(keyChar) && keyChar != ' ')
            throw new ArgumentException("Only letters, digits and space map to a key by character; press other keys as a ConsoleKeyInfo.", nameof(keyChar));

        var key = (ConsoleKey)char.ToUpperInvariant(keyChar);
        Press(new ConsoleKeyInfo(keyChar, key, shift: char.IsAsciiLetterUpper(keyChar), alt: false, control: false));
    }

    public void Press(ConsoleKey key)
    {
        var keyChar = '\0';
        if (key == ConsoleKey.Enter)
            keyChar = '\r';
        else if (key == ConsoleKey.Escape)
            keyChar = '';
        else if (key == ConsoleKey.Backspace)
            keyChar = '\b';
        else if (key == ConsoleKey.Tab)
            keyChar = '\t';

        Press(new ConsoleKeyInfo(keyChar, key, shift: false, alt: false, control: false));
    }

    public void Press(ConsoleKeyInfo key)
    {
        _keys.Enqueue(key);
    }

    public void TypeLine(string line)
    {
        if (line == null)
            throw new ArgumentNullException(nameof(line), "Line cannot be null.");

        _lines.Enqueue(line);
    }

    public bool TryReadKey(out ConsoleKeyInfo key)
    {
        Interlocked.Increment(ref _tryReadKeyCallCount);

        if (ReadFailure != null)
            throw ReadFailure;

        return _keys.TryDequeue(out key);
    }

    public string? ReadLine()
    {
        Interlocked.Increment(ref _readLineCallCount);

        if (ReadFailure != null)
            throw ReadFailure;

        var gate = ReadLineGate;
        if (gate != null)
            gate.Wait();

        if (_lines.TryDequeue(out var line))
            return line;

        return null;
    }
}
