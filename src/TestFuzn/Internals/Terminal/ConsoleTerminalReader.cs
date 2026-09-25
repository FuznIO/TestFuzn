namespace Fuzn.TestFuzn.Internals.Terminal;

internal sealed class ConsoleTerminalReader : ITerminalReader
{
    public bool TryReadKey(out ConsoleKeyInfo key)
    {
        if (!Console.KeyAvailable)
        {
            key = default;
            return false;
        }

        key = Console.ReadKey(intercept: true);
        return true;
    }

    public string? ReadLine()
    {
        return Console.ReadLine();
    }
}
