namespace Fuzn.TestFuzn.Internals.Terminal;

/// <summary>
/// Injectable input seam for the terminal rendering engine, the counterpart of
/// <see cref="ITerminalWriter"/>: every key press the engine reacts to comes through this
/// reader, so input handling is fully testable without a TTY and nothing in the engine touches
/// <see cref="Console"/> directly. Callers gate on <see cref="TerminalCapabilities.IsInteractive"/>:
/// keys can only be read when input is attached to a terminal.
/// </summary>
internal interface ITerminalReader
{
    /// <summary>
    /// Reads the next key press waiting in the input buffer, without echoing it, and returns
    /// false at once when none is waiting — never blocks, so a render loop can poll it on every
    /// tick and drain everything typed since the last one.
    /// </summary>
    bool TryReadKey(out ConsoleKeyInfo key);
}
