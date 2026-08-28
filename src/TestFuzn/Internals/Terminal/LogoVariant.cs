namespace Fuzn.TestFuzn.Internals.Terminal;

/// <summary>
/// The logo size <see cref="LogoWidget"/> renders for a given available space, smallest to
/// largest. <see cref="LogoWidget.SelectVariant"/> picks the largest variant that fits.
/// </summary>
internal enum LogoVariant
{
    /// <summary>Too little space for any logo; nothing renders.</summary>
    None,

    /// <summary>The one-row compact wordmark (⚡ TestFuzn).</summary>
    Compact,

    /// <summary>The full six-row ANSI-shadow banner.</summary>
    Banner
}
