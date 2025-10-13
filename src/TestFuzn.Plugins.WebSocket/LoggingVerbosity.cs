namespace Fuzn.TestFuzn.Plugins.WebSocket;

/// <summary>
/// Specifies the verbosity level for WebSocket logging.
/// </summary>
public enum LoggingVerbosity
{
    /// <summary>
    /// No logging output.
    /// </summary>
    None,

    /// <summary>
    /// Minimal logging - only connection lifecycle events.
    /// </summary>
    Minimal,

    /// <summary>
    /// Full logging - includes all messages and detailed information.
    /// </summary>
    Full
}
