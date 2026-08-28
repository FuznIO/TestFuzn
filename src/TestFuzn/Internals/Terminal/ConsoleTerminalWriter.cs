namespace Fuzn.TestFuzn.Internals.Terminal;

/// <summary>
/// Production <see cref="ITerminalWriter"/> over the real console — the one place the rendering
/// engine touches <see cref="Console"/>. Callers gate on <see cref="TerminalCapabilities"/>:
/// the window dimensions are only meaningful when output is not redirected.
/// </summary>
internal sealed class ConsoleTerminalWriter : ITerminalWriter
{
    public int WindowWidth => Console.WindowWidth;

    public int WindowHeight => Console.WindowHeight;

    public void Write(string text)
    {
        Console.Out.Write(text);
    }
}
