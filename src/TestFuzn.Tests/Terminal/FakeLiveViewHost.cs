using Fuzn.TestFuzn.Internals.Terminal;

namespace Fuzn.TestFuzn.Tests.Terminal;

/// <summary>
/// Test <see cref="ILiveViewHost"/> for callers driven one tick at a time on the test thread:
/// hand-resolved capabilities, a <see cref="FakeTerminalWriter"/> and a
/// <see cref="FakeTerminalReader"/> the test inspects and feeds, a settable clock, and a delay
/// that returns at once — so a poll loop over this host runs synchronously — counting every
/// wait and invoking <see cref="OnDelay"/> with the wait's ordinal first, which is where a test
/// presses keys, cancels a token or resizes the writer for the caller's next tick. A caller
/// that keeps waiting past <see cref="DelayLimit"/> fails loud instead of spinning forever,
/// since nothing here ever blocks. The reader or the writer can be withheld to pin that a caller
/// fails before touching the terminal, and every creation is counted.
/// </summary>
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

    /// <summary>When false, <see cref="CreateTerminalReader"/> returns null, as a host without keyboard input would.</summary>
    public bool HasReader { get; set; } = true;

    /// <summary>When false, <see cref="CreateTerminalWriter"/> returns null, as a host without a terminal would.</summary>
    public bool HasWriter { get; set; } = true;

    /// <summary>How many times <see cref="DetectCapabilities"/> has been called.</summary>
    public int DetectCapabilitiesCallCount { get; private set; }

    /// <summary>How many times <see cref="CreateTerminalWriter"/> has been called.</summary>
    public int CreateTerminalWriterCallCount { get; private set; }

    /// <summary>How many times <see cref="CreateTerminalReader"/> has been called.</summary>
    public int CreateTerminalReaderCallCount { get; private set; }

    /// <summary>How many times <see cref="Delay"/> has been called.</summary>
    public int DelayCount { get; private set; }

    /// <summary>The interval of every delay, in order.</summary>
    public List<TimeSpan> DelayIntervals { get; } = new List<TimeSpan>();

    /// <summary>The delay count past which <see cref="Delay"/> throws, so a caller that never ends fails the test instead of hanging it.</summary>
    public int DelayLimit { get; set; } = 1000;

    /// <summary>Called with the delay's ordinal (1 for the first) at the start of every delay, before it returns.</summary>
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
