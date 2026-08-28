using Fuzn.TestFuzn.Internals.ConsoleOutput;

namespace Fuzn.TestFuzn.Internals.Terminal;

/// <summary>
/// The production <see cref="ILiveViewHost"/>: the real console (capabilities detected from
/// the console's redirect state and environment, output through <see cref="ConsoleTerminalWriter"/>,
/// keys through <see cref="ConsoleTerminalReader"/>) and the wall clock.
/// </summary>
internal sealed class ConsoleLiveViewHost : ILiveViewHost
{
    public TerminalCapabilities DetectCapabilities()
    {
        return TerminalCapabilities.Detect(new EnvironmentWrapper());
    }

    public ITerminalWriter CreateTerminalWriter()
    {
        return new ConsoleTerminalWriter();
    }

    public ITerminalReader CreateTerminalReader()
    {
        return new ConsoleTerminalReader();
    }

    public DateTime UtcNow => DateTime.UtcNow;

    public Task Delay(TimeSpan interval, CancellationToken cancellationToken)
    {
        return DelayHelper.Delay(interval, cancellationToken);
    }
}
