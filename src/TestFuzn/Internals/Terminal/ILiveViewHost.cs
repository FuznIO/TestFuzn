namespace Fuzn.TestFuzn.Internals.Terminal;

internal interface ILiveViewHost
{
    TerminalCapabilities DetectCapabilities();

    ITerminalWriter CreateTerminalWriter();

    ITerminalReader CreateTerminalReader();

    DateTime UtcNow { get; }

    Task Delay(TimeSpan interval, CancellationToken cancellationToken);
}
