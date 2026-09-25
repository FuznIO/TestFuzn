using Fuzn.TestFuzn.Internals.Terminal;

namespace Fuzn.TestFuzn.Tests.Terminal;

internal sealed class FakeLiveViewHost : ILiveViewHost
{
    public FakeLiveViewHost(TerminalCapabilities capabilities)
    {
        if (capabilities == null)
            throw new ArgumentNullException(nameof(capabilities), "Capabilities cannot be null.");

        Capabilities = capabilities;
    }

    public TerminalCapabilities Capabilities { get; }

    public FakeTerminalWriter Writer { get; } = new FakeTerminalWriter();

    public FakeTerminalReader Reader { get; } = new FakeTerminalReader();

    public bool HasReader { get; set; } = true;

    public bool HasWriter { get; set; } = true;

    public int DetectCapabilitiesCallCount { get; private set; }

    public int CreateTerminalWriterCallCount { get; private set; }

    public int CreateTerminalReaderCallCount { get; private set; }

    public int DelayCount { get; private set; }

    public List<TimeSpan> DelayIntervals { get; } = new List<TimeSpan>();

    public int DelayLimit { get; set; } = 1000;

    public Action<int>? OnDelay { get; set; }

    public DateTime UtcNow { get; set; } = SyntheticLoadSnapshots.BaseTime;

    public TerminalCapabilities DetectCapabilities()
    {
        DetectCapabilitiesCallCount++;
        return Capabilities;
    }

    public ITerminalWriter CreateTerminalWriter()
    {
        CreateTerminalWriterCallCount++;
        if (!HasWriter)
            return null!;

        return Writer;
    }

    public ITerminalReader CreateTerminalReader()
    {
        CreateTerminalReaderCallCount++;
        if (!HasReader)
            return null!;

        return Reader;
    }

    public Task Delay(TimeSpan interval, CancellationToken cancellationToken)
    {
        DelayCount++;
        DelayIntervals.Add(interval);
        if (DelayCount > DelayLimit)
            throw new InvalidOperationException($"The caller waited {DelayLimit} times without ending; the test is not driving it to an end.");

        if (OnDelay != null)
            OnDelay(DelayCount);

        return Task.CompletedTask;
    }
}
