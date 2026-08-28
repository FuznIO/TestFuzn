namespace Fuzn.TestFuzn.Internals.Terminal;

/// <summary>
/// Injectable output seam for the terminal rendering engine: everything the engine emits goes
/// through this writer, and the terminal dimensions come from it, so rendering is fully
/// testable without a TTY. Nothing in the engine touches <see cref="Console"/> directly.
/// </summary>
internal interface ITerminalWriter
{
    /// <summary>Current terminal width in columns.</summary>
    int WindowWidth { get; }

    /// <summary>Current terminal height in rows.</summary>
    int WindowHeight { get; }

    /// <summary>Writes raw text, including any embedded ANSI sequences, to the terminal.</summary>
    void Write(string text);
}
