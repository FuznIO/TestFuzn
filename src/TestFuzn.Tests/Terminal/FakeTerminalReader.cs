using System.Collections.Concurrent;
using Fuzn.TestFuzn.Internals.Terminal;

namespace Fuzn.TestFuzn.Tests.Terminal;

/// <summary>
/// Test <see cref="ITerminalReader"/> fed by the test: pressed keys are handed out in order,
/// one per <see cref="TryReadKey"/>, and an empty queue reads as no key waiting — as the real
/// console does between key presses. Every read attempt is counted so a test can pin that the
/// reader is never consulted, what is still queued shows what was never read, and a read can be
/// made to throw, the way <c>Console.KeyAvailable</c> does on an input that is not a terminal.
/// Safe to press keys from the test thread while a render loop polls from its own.
/// </summary>
internal sealed class FakeTerminalReader : ITerminalReader
{
    private readonly ConcurrentQueue<ConsoleKeyInfo> _keys = new();
    private int _tryReadKeyCallCount;

    /// <summary>How many times <see cref="TryReadKey"/> has been called, whether or not a key was waiting.</summary>
    public int TryReadKeyCallCount => Volatile.Read(ref _tryReadKeyCallCount);

    /// <summary>The keys pressed and not yet read.</summary>
    public int PendingKeyCount => _keys.Count;

    /// <summary>When set, every read throws it (after counting the read).</summary>
    public Exception? ReadFailure { get; set; }

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

    /// <summary>Presses a key exactly as given.</summary>
    public void Press(ConsoleKeyInfo key)
    {
        _keys.Enqueue(key);
    }

    public bool TryReadKey(out ConsoleKeyInfo key)
    {
        Interlocked.Increment(ref _tryReadKeyCallCount);

        if (ReadFailure != null)
            throw ReadFailure;

        return _keys.TryDequeue(out key);
    }
}
