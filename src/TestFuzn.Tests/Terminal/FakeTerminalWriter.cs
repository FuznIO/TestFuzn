using Fuzn.TestFuzn.Internals.Terminal;

namespace Fuzn.TestFuzn.Tests.Terminal;

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

    public int WindowWidthReadCount { get; private set; }

    public int WindowHeightReadCount { get; private set; }

    public Exception? WindowSizeReadFailure { get; set; }

    public Exception? WriteFailure { get; set; }

    public int FailedWriteCount { get; private set; }

    public Action<int>? FailedWriteObserver { get; set; }

    public Action<string>? WriteObserver { get; set; }

    public IReadOnlyList<string> Writes => _writes;

    public void Write(string text)
    {
        if (WriteFailure != null)
        {
            var failure = WriteFailure;
            FailedWriteCount++;
            if (FailedWriteObserver != null)
                FailedWriteObserver(FailedWriteCount);

            throw failure;
        }

        _writes.Add(text);

        if (WriteObserver != null)
            WriteObserver(text);
    }

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
