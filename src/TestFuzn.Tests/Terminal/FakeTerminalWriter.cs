using Fuzn.TestFuzn.Internals.Terminal;

namespace Fuzn.TestFuzn.Tests.Terminal;

/// <summary>
/// Test <see cref="ITerminalWriter"/> that captures every write, so rendering tests can assert
/// exact emitted output. The window dimensions are settable for tests of callers that read them;
/// <see cref="FrameRenderer"/> itself never reads them — it renders at the size passed to
/// <see cref="FrameRenderer.Render"/>.
/// </summary>
internal sealed class FakeTerminalWriter : ITerminalWriter
{
    private readonly List<string> _writes = new();

    public int WindowWidth { get; set; } = 80;

    public int WindowHeight { get; set; } = 24;

    /// <summary>Every write in order, one entry per <see cref="Write"/> call.</summary>
    public IReadOnlyList<string> Writes => _writes;

    public void Write(string text)
    {
        _writes.Add(text);
    }

    /// <summary>Discards captured writes so a test can assert on the next render in isolation.</summary>
    public void ClearWrites()
    {
        _writes.Clear();
    }
}
