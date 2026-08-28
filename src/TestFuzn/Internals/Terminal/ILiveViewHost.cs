namespace Fuzn.TestFuzn.Internals.Terminal;

/// <summary>
/// What the live view needs from the process it runs in — the terminal, for output and keys,
/// and the clock — behind one seam, so the console manager can be driven hermetically:
/// production wraps the real console and wall clock (<see cref="ConsoleLiveViewHost"/>); tests
/// inject a fake writer, a fake reader they press keys into, hand-resolved capabilities, a
/// clock they set and a delay they release one tick at a time.
/// </summary>
internal interface ILiveViewHost
{
    /// <summary>Resolves the terminal's capabilities; asked once, when live output is about to start.</summary>
    TerminalCapabilities DetectCapabilities();

    /// <summary>The writer the dashboard renders to; only asked for when the capabilities support the live view.</summary>
    ITerminalWriter CreateTerminalWriter();

    /// <summary>
    /// The reader the live view polls keys from; only asked for when the capabilities support
    /// the live view, which requires an interactive terminal — so input is never read when it
    /// is redirected.
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
