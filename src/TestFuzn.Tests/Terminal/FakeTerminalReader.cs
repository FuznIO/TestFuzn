using System.Collections.Concurrent;
using Fuzn.TestFuzn.Internals.Terminal;

namespace Fuzn.TestFuzn.Tests.Terminal;

/// <summary>
/// Test <see cref="ITerminalReader"/> fed by the test: pressed keys are handed out in order,
/// one per <see cref="TryReadKey"/>, and an empty queue reads as no key waiting — as the real
/// console does between key presses; typed lines are handed out in order, one per
/// <see cref="ReadLine"/>, and an empty queue reads as the end of the input. Every read
/// attempt is counted so a test can pin that keys or lines are never consulted, what is still
/// queued shows what was never read, and a read can be made to throw, the way
/// <c>Console.KeyAvailable</c> does on an input that is not a terminal. Safe to press keys
/// from the test thread while a render loop polls from its own.
/// </summary>
internal sealed class FakeTerminalReader : ITerminalReader
{
    private readonly ConcurrentQueue<ConsoleKeyInfo> _keys = new();
    private readonly ConcurrentQueue<string> _lines = new();
    private int _tryReadKeyCallCount;
    private int _readLineCallCount;

    /// <summary>How many times <see cref="TryReadKey"/> has been called, whether or not a key was waiting.</summary>
    public int TryReadKeyCallCount => Volatile.Read(ref _tryReadKeyCallCount);

    /// <summary>How many times <see cref="ReadLine"/> has been called, whether or not a line was waiting.</summary>
    public int ReadLineCallCount => Volatile.Read(ref _readLineCallCount);

    /// <summary>The keys pressed and not yet read.</summary>
    public int PendingKeyCount => _keys.Count;

    /// <summary>The lines typed and not yet read.</summary>
    public int PendingLineCount => _lines.Count;

    /// <summary>When set, every read throws it (after counting the read).</summary>
    public Exception? ReadFailure { get; set; }

    /// <summary>
    /// When set, <see cref="ReadLine"/> waits on it (after counting the read) before answering,
    /// so a test can hold a line read pending — the way a real console read blocks until a line
    /// is typed — and release it at the end by setting the event.
    /// </summary>
    public ManualResetEventSlim? ReadLineGate { get; set; }

    /// <summary>
    /// Presses a letter, digit or space key, as typing that character produces it: the
    /// character itself with its key (an upper-case letter carries Shift).
    /// </summary>
    public void Press(char keyChar)
    {
        if (!char.IsAsciiLetterOrDigit(keyChar) && keyChar != ' ')
            throw new ArgumentException("Only letters, digits and space map to a key by character; press other keys as a ConsoleKeyInfo.", nameof(keyChar));

        // For these characters the ConsoleKey value is the upper-case character code.
        var key = (ConsoleKey)char.ToUpperInvariant(keyChar);
        Press(new ConsoleKeyInfo(keyChar, key, shift: char.IsAsciiLetterUpper(keyChar), alt: false, control: false));
    }

    /// <summary>
    /// Presses a key that types no printable character — an arrow, Enter, Escape, Backspace,
    /// Tab, Home, End, Page Up/Down, a function key — with the control character the console
    /// reports for Enter, Escape, Backspace and Tab, and no character for the rest.
    /// </summary>
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

    /// <summary>Presses a key exactly as given.</summary>
    public void Press(ConsoleKeyInfo key)
    {
        _keys.Enqueue(key);
    }

    /// <summary>Types a line, without its terminator, for the next <see cref="ReadLine"/>.</summary>
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
