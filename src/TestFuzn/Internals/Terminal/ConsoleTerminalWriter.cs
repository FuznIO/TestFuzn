namespace Fuzn.TestFuzn.Internals.Terminal;

internal sealed class ConsoleTerminalWriter : ITerminalWriter
{
    public int WindowWidth => Console.WindowWidth;

    public int WindowHeight => Console.WindowHeight;

    public void Write(string text)
    {
        Console.Out.Write(text);
    }
}
