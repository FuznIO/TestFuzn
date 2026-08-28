namespace Fuzn.TestFuzn.Internals.Terminal;

/// <summary>
/// Injectable input seam for the terminal rendering engine, the counterpart of
/// <see cref="ITerminalWriter"/>: every key press the engine reacts to comes through this
/// reader, so input handling is fully testable without a TTY and nothing in the engine touches
/// <see cref="Console"/> directly. Callers gate key reads on
/// <see cref="TerminalCapabilities.IsInteractive"/>: keys can only be read when input is
/// attached to a terminal, whereas <see cref="ReadLine"/> works on any input, redirected or not.
/// </summary>
internal interface ITerminalReader
{
    /// <summary>
    /// Reads the next key press waiting in the input buffer, without echoing it, and returns
    /// false at once when none is waiting — never blocks, so a render loop can poll it on every
    /// tick and drain everything typed since the last one.
    /// </summary>
    bool TryReadKey(out ConsoleKeyInfo key);

    /// <summary>
    /// Reads the next line of input, blocking until a line terminator or the end of the input,
    /// and returns null at the end of the input — the prompt read of the paths that run without
    /// a live view, which works on a redirected input where <see cref="TryReadKey"/> does not.
    /// </summary>
    string? ReadLine();
}
