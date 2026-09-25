namespace Fuzn.TestFuzn.Internals.Terminal;

internal sealed class LiveDashboard : IDisposable
{
    public static readonly TimeSpan RenderInterval = TimeSpan.FromMilliseconds(250);

    private static readonly string[] BrailleSpinnerFrames = { "⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏" };
    private static readonly string[] AsciiSpinnerFrames = { "|", "/", "-", "\\" };

    private readonly ITerminalWriter _writer;
    private readonly TerminalCapabilities _capabilities;
    private readonly Func<IReadOnlyList<LiveMetricsSnapshot>> _snapshotsProvider;
    private readonly SpinnerGlyphSet _spinnerGlyphSet;
    private readonly LoadViewHeader? _header;
    private readonly string[] _spinnerFrames;
    private readonly FrameRenderer _renderer;
    private readonly FrameBuffer _frame = new();
    private int _spinnerFrameIndex;
    private bool _isStarted;
    private bool _isDisposed;

    public LiveDashboard(ITerminalWriter writer, TerminalCapabilities capabilities, Func<IReadOnlyList<LiveMetricsSnapshot>> snapshotsProvider, SpinnerGlyphSet spinnerGlyphSet = SpinnerGlyphSet.Braille, LoadViewHeader? header = null)
    {
        if (writer == null)
            throw new ArgumentNullException(nameof(writer), "Writer cannot be null.");
        if (capabilities == null)
            throw new ArgumentNullException(nameof(capabilities), "Capabilities cannot be null.");
        if (snapshotsProvider == null)
            throw new ArgumentNullException(nameof(snapshotsProvider), "Snapshots provider cannot be null.");
        if (!capabilities.SupportsLiveView)
            throw new ArgumentException("The terminal does not support the live view; gate on TerminalCapabilities.SupportsLiveView before creating the dashboard.", nameof(capabilities));

        _writer = writer;
        _capabilities = capabilities;
        _snapshotsProvider = snapshotsProvider;
        _spinnerGlyphSet = spinnerGlyphSet;
        _header = header;
        _spinnerFrames = spinnerGlyphSet == SpinnerGlyphSet.Braille ? BrailleSpinnerFrames : AsciiSpinnerFrames;
        _renderer = new FrameRenderer(writer);
    }

    public void Start()
    {
        ThrowIfDisposed();
        if (_isStarted)
            throw new InvalidOperationException("The live dashboard has already been started.");

        _isStarted = true;
        _writer.Write(AnsiCodes.EnterAlternateScreen + AnsiCodes.HideCursor + AnsiCodes.DisableAutoWrap);
    }

    public void Render()
    {
        ThrowIfDisposed();
        if (!_isStarted)
            throw new InvalidOperationException("The live dashboard has not been started.");

        var width = _writer.WindowWidth;
        var height = _writer.WindowHeight;

        var snapshots = _snapshotsProvider();
        if (snapshots == null)
            throw new InvalidOperationException("The snapshots provider returned null.");

        var spinnerGlyph = _spinnerFrames[_spinnerFrameIndex];
        _spinnerFrameIndex = (_spinnerFrameIndex + 1) % _spinnerFrames.Length;

        _frame.Clear();
        _frame.AddLines(LiveDashboardLayout.Render(snapshots, width, height, _capabilities.ColorMode, spinnerGlyph, _header));
        _renderer.Render(_frame, width, height);
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        if (!_isStarted)
            return;

        _writer.Write(AnsiCodes.Reset + AnsiCodes.EnableAutoWrap + AnsiCodes.ShowCursor + AnsiCodes.ExitAlternateScreen);
    }

    private void ThrowIfDisposed()
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(LiveDashboard));
    }
}
