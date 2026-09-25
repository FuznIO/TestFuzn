using System.Globalization;

namespace Fuzn.TestFuzn.Internals.Terminal;

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

    public IReadOnlyList<string> TestNames => _testNames;

    public string Filter => _filter;

    public IReadOnlyList<int> MatchingTests => _matchingTests;

    public int? HighlightedTest => _highlightedPosition < 0 ? null : _matchingTests[_highlightedPosition];

    public int? EnteredTestNumber => _enteredTestNumber;

    public int FirstVisibleMatch => _firstVisibleMatch;

    public int VisibleRowCount
    {
        get => _visibleRowCount;
        set
        {
            _visibleRowCount = value;
            FitWindow();
        }
    }

    public TestSelectionMenuOutcome HandleKey(ConsoleKeyInfo key)
    {
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

        if (key.KeyChar == '\0' || char.IsControl(key.KeyChar))
            return TestSelectionMenuOutcome.Open;

        AppendToFilter(key.KeyChar);
        return TestSelectionMenuOutcome.Open;
    }

    public void MoveUp()
    {
        MoveHighlightTo(_highlightedPosition - 1);
    }

    public void MoveDown()
    {
        MoveHighlightTo(_highlightedPosition + 1);
    }

    public void PageUp()
    {
        MoveHighlightTo(_highlightedPosition - PageSize());
    }

    public void PageDown()
    {
        MoveHighlightTo(_highlightedPosition + PageSize());
    }

    public void MoveToFirst()
    {
        MoveHighlightTo(0);
    }

    public void MoveToLast()
    {
        MoveHighlightTo(_matchingTests.Count - 1);
    }

    public void AppendToFilter(char character)
    {
        _filter += character;
        UpdateMatches();
    }

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
