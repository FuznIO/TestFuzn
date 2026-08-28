namespace Fuzn.TestFuzn.Internals.Terminal;

/// <summary>
/// The glyph vocabulary <see cref="SparklineWidget"/> renders with. The widget does not detect
/// what the terminal or its encoding can display — the caller picks the set (braille needs a
/// font and encoding that cover the braille patterns block, while the block elements are far
/// more widely supported).
/// </summary>
internal enum SparklineGlyphSet
{
    /// <summary>Braille patterns, two samples per character at four vertical levels each.</summary>
    Braille,

    /// <summary>Block elements ▁▂▃▄▅▆▇█, one sample per character at eight vertical levels.</summary>
    Blocks
}
