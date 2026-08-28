using Fuzn.TestFuzn.Internals.Terminal;

namespace Fuzn.TestFuzn.Tests.Terminal;

/// <summary>
/// Test <see cref="ITerminalWriter"/> that captures every write, so rendering tests can assert
/// exact emitted output. The window dimensions are settable for tests of callers that read them,
/// and every read is counted so a test can pin how often a caller reads them;
/// <see cref="FrameRenderer"/> itself never reads them — it renders at the size passed to
/// <see cref="FrameRenderer.Render"/>. A dimension read can be made to throw, the way
/// <c>Console.WindowWidth</c> does on a closed or console-less output, and so can a write, the
/// way writing to a closed pipe does — a failed write records nothing, as nothing reached the
/// terminal.
/// </summary>
internal sealed class FakeTerminalWriter : ITerminalWriter
{
    private readonly List<string> _writes = new();
    private int _windowWidth = 80;
    private int _windowHeight = 24;

    public int WindowWidth
    {
        get
        {
            WindowWidthReadCount++;
            ThrowIfWindowSizeUnavailable();
            return _windowWidth;
        }
        set => _windowWidth = value;
    }

    public int WindowHeight
    {
        get
        {
            WindowHeightReadCount++;
            ThrowIfWindowSizeUnavailable();
            return _windowHeight;
        }
        set => _windowHeight = value;
    }

    /// <summary>How many times <see cref="WindowWidth"/> has been read.</summary>
    public int WindowWidthReadCount { get; private set; }

    /// <summary>How many times <see cref="WindowHeight"/> has been read.</summary>
    public int WindowHeightReadCount { get; private set; }

    /// <summary>When set, reading either dimension throws it (after counting the read).</summary>
    public Exception? WindowSizeReadFailure { get; set; }

    /// <summary>When set, every write throws it before recording anything (after counting the attempt in <see cref="FailedWriteCount"/>).</summary>
    public Exception? WriteFailure { get; set; }

    /// <summary>How many writes have thrown <see cref="WriteFailure"/>.</summary>
    public int FailedWriteCount { get; private set; }

    /// <summary>Called with every written text, so a test can merge terminal writes into a wider event log.</summary>
    public Action<string>? WriteObserver { get; set; }

    /// <summary>Every write in order, one entry per <see cref="Write"/> call.</summary>
    public IReadOnlyList<string> Writes => _writes;

    public void Write(string text)
    {
        if (WriteFailure != null)
        {
            FailedWriteCount++;
            throw WriteFailure;
        }

        _writes.Add(text);

        if (WriteObserver != null)
            WriteObserver(text);
    }

    /// <summary>Discards captured writes so a test can assert on the next render in isolation.</summary>
    public void ClearWrites()
    {
        _writes.Clear();
    }

    private void ThrowIfWindowSizeUnavailable()
    {
        if (WindowSizeReadFailure != null)
            throw WindowSizeReadFailure;
    }
}
