namespace Fuzn.TestFuzn.Internals.Terminal;

/// <summary>
/// What a key press did to the test selection menu, as <see cref="TestSelectionMenuState.HandleKey"/>
/// reports it: the menu stays open, a test was selected, or the menu was quit.
/// </summary>
internal enum TestSelectionMenuOutcome
{
    /// <summary>The menu stays open — the key moved the highlight, edited the filter, or did nothing.</summary>
    Open,

    /// <summary>Enter selected the highlighted test, <see cref="TestSelectionMenuState.HighlightedTest"/>.</summary>
    TestSelected,

    /// <summary>Escape quit the menu without selecting a test.</summary>
    Quit
}
