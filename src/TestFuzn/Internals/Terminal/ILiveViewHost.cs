namespace Fuzn.TestFuzn.Internals.Terminal;

/// <summary>
/// What the live view and the standalone runner's test selection menu need from the process
/// they run in — the terminal, for output and keys, and the clock — behind one seam, so the
/// console manager and the menu can be driven hermetically: production wraps the real console
/// and wall clock (<see cref="ConsoleLiveViewHost"/>); tests inject a fake writer, a fake reader
/// they press keys into, hand-resolved capabilities, a clock they set and a delay they release
/// one tick at a time.
/// </summary>
internal interface ILiveViewHost
{
    /// <summary>Resolves the terminal's capabilities; asked once, when live output or the menu is about to start.</summary>
    TerminalCapabilities DetectCapabilities();

    /// <summary>
    /// The writer the live view writes to: the dashboard's frames when the capabilities support
    /// the live view, the plain stats lines otherwise — on which path its dimensions are never
    /// read, since a redirected output has none. The test selection menu writes its frames, or
    /// its plain numbered list, the same way.
    /// </summary>
    ITerminalWriter CreateTerminalWriter();

    /// <summary>
    /// The reader the live view polls keys from; only asked for when the capabilities support
    /// the live view, which requires an interactive terminal — so keys are never read when
    /// input is redirected. The test selection menu's prompt fallback also asks for it, on any
    /// input, for its line reads alone: <see cref="ITerminalReader.ReadLine"/> works on a
    /// redirected input where a key read does not.
    /// </summary>
    ITerminalReader CreateTerminalReader();

    /// <summary>The current UTC time — the timestamp every sample is recorded at (the metrics model rejects local time).</summary>
    DateTime UtcNow { get; }

    /// <summary>
    /// Waits one render interval. Expected to return normally when the token is cancelled
    /// (as <c>DelayHelper.Delay</c> does): cancellation is the live view's normal way to stop.
    /// </summary>
    Task Delay(TimeSpan interval, CancellationToken cancellationToken);
}
