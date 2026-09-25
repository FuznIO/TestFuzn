namespace Fuzn.TestFuzn.Internals.Terminal;

internal interface ITerminalWriter
{
    int WindowWidth { get; }

    int WindowHeight { get; }

    void Write(string text);
}
