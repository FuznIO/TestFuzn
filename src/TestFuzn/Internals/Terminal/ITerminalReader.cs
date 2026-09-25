namespace Fuzn.TestFuzn.Internals.Terminal;

internal interface ITerminalReader
{
    bool TryReadKey(out ConsoleKeyInfo key);

    string? ReadLine();
}
