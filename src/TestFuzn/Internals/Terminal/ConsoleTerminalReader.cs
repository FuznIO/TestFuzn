namespace Fuzn.TestFuzn.Internals.Terminal;

/// <summary>
/// Production <see cref="ITerminalReader"/> over the real console — with
/// <see cref="ConsoleTerminalWriter"/>, the only place the rendering engine touches
/// <see cref="Console"/>. Callers gate on <see cref="TerminalCapabilities.IsInteractive"/>:
/// both the availability check and the read throw when input is redirected. Keys are read
/// intercepted, so a key press never echoes onto the alternate screen. Ctrl+C is left alone:
/// it stays a console break (<see cref="Console.TreatControlCAsInput"/> is never set, so the
/// terminal turns it into a break signal instead of a key), and the standalone runner's
/// <see cref="Console.CancelKeyPress"/> handler keeps working while the live view polls.
/// </summary>
internal sealed class ConsoleTerminalReader : ITerminalReader
{
    public bool TryReadKey(out ConsoleKeyInfo key)
    {
        // The availability check is what keeps this non-blocking: ReadKey on its own waits for
        // a key press, which would stall the render loop.
        if (!Console.KeyAvailable)
        {
            key = default;
            return false;
        }

        key = Console.ReadKey(intercept: true);
        return true;
    }
}
