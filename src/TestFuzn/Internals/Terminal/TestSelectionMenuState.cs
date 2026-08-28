using System.Globalization;

namespace Fuzn.TestFuzn.Internals.Terminal;

/// <summary>
/// The test selection menu's interaction state, pure and console-free: the tests' display
/// names in list order, the filter typed so far, which tests match it, the highlighted match,
/// and the window of matches that fits the rows the list has on screen. Keys come in through
/// <see cref="HandleKey"/> — up/down move the highlight one match, page up/down move it by the
/// visible rows, home/end jump to the first and last match, all clamped at the ends (no
/// wrap-around); a printable character appends to the filter and backspace removes its last
/// character (a surrogate pair as one); Enter selects the highlighted test and is a no-op when
/// nothing matches; Escape quits; every other key — control characters, Alt and Control chords,
/// function keys — is ignored. The filter is a case-insensitive substring search over the
/// names, and the highlight stays on the same test while it keeps matching, else moves to the
/// first match (none when nothing matches). Number entry: a filter that is nothing but digits
/// and names a listed test — 1 to the test count — is a number, not a search: the list stays
/// complete and the highlight jumps to that test, so typing its number and pressing Enter runs
/// it, as on the prompt fallback; a number naming no test (0, or beyond the count) is searched
/// for as text like any other filter. Navigation after a number entry moves the highlight away
/// from the named test while the filter, and <see cref="EnteredTestNumber"/> with it, keep
/// saying what was typed — Enter then runs the highlighted test, not the typed number, and the
/// layout reports the plain count instead of the named test. The window scrolls only as far as needed to keep the
/// highlight visible, and is pulled up when the matches no longer fill it; the caller sets
/// <see cref="VisibleRowCount"/> from the layout before handling keys, so paging and the window
/// follow the terminal size. Test indexes are positions in <see cref="TestNames"/>; a test's
/// number, as shown and typed, is its index plus one. Not thread-safe: one menu loop drives it.
/// </summary>
internal sealed class TestSelectionMenuState
{
    private readonly IReadOnlyList<string> _testNames;
    private readonly int[] _allTests;
    private IReadOnlyList<int> _matchingTests;
    private string _filter = string.Empty;
    private int? _enteredTestNumber;
    private int _highlightedPosition;
    private int _firstVisibleMatch;
    private int _visibleRowCount;

    /// <param name="testNames">The tests' display names, in list order.</param>
    public TestSelectionMenuState(IReadOnlyList<string> testNames)
    {
        if (testNames == null)
            throw new ArgumentNullException(nameof(testNames), "Test names cannot be null.");
        foreach (var testName in testNames)
        {
            if (testName == null)
                throw new ArgumentNullException(nameof(testNames), "Test names cannot contain null.");
        }

        _testNames = testNames;
        _allTests = new int[testNames.Count];
        for (var index = 0; index < _allTests.Length; index++)
            _allTests[index] = index;

        _matchingTests = _allTests;
        _highlightedPosition = _allTests.Length > 0 ? 0 : -1;
    }

    /// <summary>The tests' display names, in list order; a test's index here is its identity.</summary>
    public IReadOnlyList<string> TestNames => _testNames;

    /// <summary>The filter text typed so far; empty when nothing is typed.</summary>
    public string Filter => _filter;

    /// <summary>
    /// The indexes of the tests matching the filter, in list order: every test when the filter
    /// is empty or a number entry, else the case-insensitive substring matches.
    /// </summary>
    public IReadOnlyList<int> MatchingTests => _matchingTests;

    /// <summary>The index of the highlighted test — the one Enter selects — or null when nothing matches.</summary>
    public int? HighlightedTest => _highlightedPosition < 0 ? null : _matchingTests[_highlightedPosition];

    /// <summary>
    /// The test number the filter names when it is a number entry — a purely numeric filter
    /// naming a listed test, 1 to the test count — or null when the filter is empty or a text
    /// search.
    /// </summary>
    public int? EnteredTestNumber => _enteredTestNumber;

    /// <summary>The position in <see cref="MatchingTests"/> of the first match shown; the window is this and the next <see cref="VisibleRowCount"/> - 1 positions.</summary>
    public int FirstVisibleMatch => _firstVisibleMatch;

    /// <summary>
    /// The rows the list has on screen: how far a page moves, and the size of the window kept
    /// around the highlight. Setting it re-fits the window at once, so a resize never leaves
    /// the highlight off screen; a value below 1 means no rows, with the window at the highlight.
    /// </summary>
    public int VisibleRowCount
    {
        get => _visibleRowCount;
        set
        {
            _visibleRowCount = value;
            FitWindow();
        }
    }

    /// <summary>Applies one key press per the rules in the class summary and reports what it did to the menu.</summary>
    public TestSelectionMenuOutcome HandleKey(ConsoleKeyInfo key)
    {
        // A chord with Alt or Control is a command the menu does not have — for every key, so
        // Ctrl+Escape does not quit and Alt+Enter does not select, as the dashboard ignores a
        // chorded quit key — and never filter text: on a pty Alt+a arrives as ESC a and reads
        // as a with the Alt modifier.
        if ((key.Modifiers & (ConsoleModifiers.Alt | ConsoleModifiers.Control)) != 0)
            return TestSelectionMenuOutcome.Open;

        switch (key.Key)
        {
            case ConsoleKey.UpArrow:
                MoveUp();
                return TestSelectionMenuOutcome.Open;
            case ConsoleKey.DownArrow:
                MoveDown();
                return TestSelectionMenuOutcome.Open;
            case ConsoleKey.PageUp:
                PageUp();
                return TestSelectionMenuOutcome.Open;
            case ConsoleKey.PageDown:
                PageDown();
                return TestSelectionMenuOutcome.Open;
            case ConsoleKey.Home:
                MoveToFirst();
                return TestSelectionMenuOutcome.Open;
            case ConsoleKey.End:
                MoveToLast();
                return TestSelectionMenuOutcome.Open;
            case ConsoleKey.Backspace:
                RemoveLastFilterCharacter();
                return TestSelectionMenuOutcome.Open;
            case ConsoleKey.Enter:
                return _highlightedPosition < 0 ? TestSelectionMenuOutcome.Open : TestSelectionMenuOutcome.TestSelected;
            case ConsoleKey.Escape:
                return TestSelectionMenuOutcome.Quit;
        }

        // Keys without a character (function keys, unmapped keys) and control characters
        // (Tab, a bare control code) type nothing; every printable character, surrogate
        // halves of an astral-plane character included, appends to the filter.
        if (key.KeyChar == '\0' || char.IsControl(key.KeyChar))
            return TestSelectionMenuOutcome.Open;

        AppendToFilter(key.KeyChar);
        return TestSelectionMenuOutcome.Open;
    }

    /// <summary>Moves the highlight one match up; stays at the first match.</summary>
    public void MoveUp()
    {
        MoveHighlightTo(_highlightedPosition - 1);
    }

    /// <summary>Moves the highlight one match down; stays at the last match.</summary>
    public void MoveDown()
    {
        MoveHighlightTo(_highlightedPosition + 1);
    }

    /// <summary>Moves the highlight up by the visible rows (at least one); stays at the first match.</summary>
    public void PageUp()
    {
        MoveHighlightTo(_highlightedPosition - PageSize());
    }

    /// <summary>Moves the highlight down by the visible rows (at least one); stays at the last match.</summary>
    public void PageDown()
    {
        MoveHighlightTo(_highlightedPosition + PageSize());
    }

    /// <summary>Moves the highlight to the first match.</summary>
    public void MoveToFirst()
    {
        MoveHighlightTo(0);
    }

    /// <summary>Moves the highlight to the last match.</summary>
    public void MoveToLast()
    {
        MoveHighlightTo(_matchingTests.Count - 1);
    }

    /// <summary>Appends one character to the filter and re-matches.</summary>
    public void AppendToFilter(char character)
    {
        _filter += character;
        UpdateMatches();
    }

    /// <summary>Removes the filter's last character — a surrogate pair as one — and re-matches; a no-op on an empty filter.</summary>
    public void RemoveLastFilterCharacter()
    {
        if (_filter.Length == 0)
            return;

        var removedLength = 1;
        if (_filter.Length >= 2 && char.IsLowSurrogate(_filter[_filter.Length - 1]) && char.IsHighSurrogate(_filter[_filter.Length - 2]))
            removedLength = 2;

        _filter = _filter.Substring(0, _filter.Length - removedLength);
        UpdateMatches();
    }

    private int PageSize()
    {
        return Math.Max(_visibleRowCount, 1);
    }

    // Clamps the requested position into the matches and re-fits the window; no matches keeps
    // the highlight absent.
    private void MoveHighlightTo(int position)
    {
        if (_matchingTests.Count == 0)
        {
            _highlightedPosition = -1;
            FitWindow();
            return;
        }

        _highlightedPosition = Math.Clamp(position, 0, _matchingTests.Count - 1);
        FitWindow();
    }

    // Recomputes the matches for the current filter: a number entry keeps every test and
    // highlights the named one; a text search keeps the highlight on its test while that test
    // still matches, else takes the first match.
    private void UpdateMatches()
    {
        var previousHighlightedTest = HighlightedTest;

        if (TryParseTestNumber(_filter, out var testNumber))
        {
            _enteredTestNumber = testNumber;
            _matchingTests = _allTests;
            _highlightedPosition = testNumber - 1;
            FitWindow();
            return;
        }

        _enteredTestNumber = null;
        if (_filter.Length == 0)
        {
            _matchingTests = _allTests;
        }
        else
        {
            var matches = new List<int>();
            for (var index = 0; index < _testNames.Count; index++)
            {
                if (_testNames[index].IndexOf(_filter, StringComparison.OrdinalIgnoreCase) >= 0)
                    matches.Add(index);
            }

            _matchingTests = matches;
        }

        _highlightedPosition = -1;
        if (previousHighlightedTest != null)
        {
            for (var position = 0; position < _matchingTests.Count; position++)
            {
                if (_matchingTests[position] == previousHighlightedTest.Value)
                {
                    _highlightedPosition = position;
                    break;
                }
            }
        }

        if (_highlightedPosition < 0 && _matchingTests.Count > 0)
            _highlightedPosition = 0;

        FitWindow();
    }

    // A number entry is digits only — no sign, no whitespace — naming a listed test.
    private bool TryParseTestNumber(string filter, out int testNumber)
    {
        testNumber = 0;
        if (filter.Length == 0)
            return false;

        foreach (var character in filter)
        {
            if (!char.IsAsciiDigit(character))
                return false;
        }

        if (!int.TryParse(filter, NumberStyles.None, CultureInfo.InvariantCulture, out testNumber))
            return false;

        return testNumber >= 1 && testNumber <= _testNames.Count;
    }

    // Keeps the window over the matches and the highlight inside it: pulled up when the
    // matches no longer fill it, then scrolled the shortest way to include the highlight.
    private void FitWindow()
    {
        if (_highlightedPosition < 0)
        {
            _firstVisibleMatch = 0;
            return;
        }

        if (_visibleRowCount < 1)
        {
            _firstVisibleMatch = _highlightedPosition;
            return;
        }

        var lastFirstVisibleMatch = Math.Max(_matchingTests.Count - _visibleRowCount, 0);
        if (_firstVisibleMatch > lastFirstVisibleMatch)
            _firstVisibleMatch = lastFirstVisibleMatch;

        if (_highlightedPosition < _firstVisibleMatch)
            _firstVisibleMatch = _highlightedPosition;
        else if (_highlightedPosition >= _firstVisibleMatch + _visibleRowCount)
            _firstVisibleMatch = _highlightedPosition - _visibleRowCount + 1;
    }
}
