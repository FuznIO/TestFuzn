namespace Fuzn.TestFuzn.Internals.Terminal;

/// <summary>
/// The standalone runner's live load dashboard as a terminal session: owns the alternate screen
/// while it runs and paints <see cref="LiveDashboardLayout"/> frames through a
/// <see cref="FrameRenderer"/>. <see cref="Start"/> enters the alternate screen, hides the
/// cursor and disables auto-wrap. Each <see cref="Render"/> reads the terminal size once, asks
/// the snapshot provider for the scenarios' current views, lays the frame out at that size
/// under the caller's <see cref="LiveDashboardViewState"/> and hands it to the renderer (which
/// repaints only what changed), advancing the running-status spinner one glyph per call — a
/// caller rendering at <see cref="RenderInterval"/> animates the spinner and shows each 1 Hz
/// data sample within a quarter second of it being published. While the state is paused the
/// spinner holds its glyph instead, so a render whose snapshots and state have not changed
/// lays out the very frame the renderer last painted and writes nothing: a paused caller can
/// keep rendering every tick — a key that changes the state or a resize still repaints — and
/// the terminal stays quiet in between.
/// <see cref="Dispose"/> restores the terminal — SGR reset, auto-wrap re-enabled, cursor shown,
/// alternate screen left — in one write, exactly once, and only when the dashboard was started;
/// callers keep the dashboard in a using/finally so the terminal is restored on completion,
/// cancellation and exceptions alike, and after that no further rendering is possible. The
/// provider is consulted on every render, so a sampling loop can publish new snapshots between
/// renders; the snapshots themselves are immutable, so a frame never shows a torn view. Requires
/// a terminal with <see cref="TerminalCapabilities.SupportsLiveView"/> — the caller gates on it
/// before constructing the dashboard, so the writer's dimensions are never read on a redirected
/// or console-less output. One thread at a time: Start and Render run on the render loop's
/// thread and the final Dispose may run on the thread that stopped the loop, with a
/// happens-before between the handoffs (the awaited loop task).
/// </summary>
internal sealed class LiveDashboard : IDisposable
{
    /// <summary>The render cadence the spinner is designed for: ~4 frames per second over 1 Hz data.</summary>
    public static readonly TimeSpan RenderInterval = TimeSpan.FromMilliseconds(250);

    // The running-status spinner frames: braille alongside braille sparklines, ASCII when the
    // glyph set says the terminal cannot show braille. Every frame is a single-column glyph.
    private static readonly string[] BrailleSpinnerFrames = { "⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏" };
    private static readonly string[] AsciiSpinnerFrames = { "|", "/", "-", "\\" };

    private readonly ITerminalWriter _writer;
    private readonly TerminalCapabilities _capabilities;
    private readonly Func<IReadOnlyList<LiveMetricsSnapshot>> _snapshotsProvider;
    private readonly SparklineGlyphSet _sparklineGlyphSet;
    private readonly string[] _spinnerFrames;
    private readonly FrameRenderer _renderer;
    private readonly FrameBuffer _frame = new();
    private int _spinnerFrameIndex;
    private bool _isStarted;
    private bool _isDisposed;

    /// <param name="writer">The terminal to render to; its dimensions are read once per render.</param>
    /// <param name="capabilities">The terminal's capabilities; must support the live view.</param>
    /// <param name="snapshotsProvider">Returns the scenarios' current views, in display order, each time a frame is rendered.</param>
    /// <param name="sparklineGlyphSet">The glyph vocabulary for the sparklines, which also selects the spinner's glyphs.</param>
    public LiveDashboard(ITerminalWriter writer, TerminalCapabilities capabilities, Func<IReadOnlyList<LiveMetricsSnapshot>> snapshotsProvider, SparklineGlyphSet sparklineGlyphSet = SparklineGlyphSet.Braille)
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
        _sparklineGlyphSet = sparklineGlyphSet;
        _spinnerFrames = sparklineGlyphSet == SparklineGlyphSet.Braille ? BrailleSpinnerFrames : AsciiSpinnerFrames;
        _renderer = new FrameRenderer(writer);
    }

    /// <summary>
    /// Enters the alternate screen, hides the cursor and disables auto-wrap, in one write. The
    /// screen is left blank until the first <see cref="Render"/>. Throws when already started.
    /// </summary>
    public void Start()
    {
        ThrowIfDisposed();
        if (_isStarted)
            throw new InvalidOperationException("The live dashboard has already been started.");

        _isStarted = true;
        _writer.Write(AnsiCodes.EnterAlternateScreen + AnsiCodes.HideCursor + AnsiCodes.DisableAutoWrap);
    }

    /// <summary>
    /// Renders one frame: reads the terminal size once, lays out the provider's current
    /// snapshots at that size under the given view state with the spinner's next glyph, and
    /// hands the frame to the diff renderer, so an unchanged data state repaints only the
    /// spinner. A paused state holds the spinner on its current glyph — the frame then changes
    /// only with the snapshots, the state or the size, and an unchanged one writes nothing.
    /// Throws when not started.
    /// </summary>
    public void Render(LiveDashboardViewState viewState)
    {
        ThrowIfDisposed();
        if (!_isStarted)
            throw new InvalidOperationException("The live dashboard has not been started.");

        // One size reading per frame, passed to both the layout and the renderer, so the two
        // always agree on the size even if the terminal resizes between them.
        var width = _writer.WindowWidth;
        var height = _writer.WindowHeight;

        var snapshots = _snapshotsProvider();
        if (snapshots == null)
            throw new InvalidOperationException("The snapshots provider returned null.");

        var spinnerGlyph = _spinnerFrames[_spinnerFrameIndex];
        if (!viewState.IsPaused)
            _spinnerFrameIndex = (_spinnerFrameIndex + 1) % _spinnerFrames.Length;

        _frame.Clear();
        _frame.AddLines(LiveDashboardLayout.Render(snapshots, viewState, width, height, _capabilities.ColorMode, _sparklineGlyphSet, spinnerGlyph));
        _renderer.Render(_frame, width, height);
    }

    /// <summary>
    /// Restores the terminal if the dashboard was started — resets SGR styling, re-enables
    /// auto-wrap, shows the cursor and leaves the alternate screen, in one write — exactly once;
    /// later calls do nothing, and Start and Render throw afterwards.
    /// </summary>
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
