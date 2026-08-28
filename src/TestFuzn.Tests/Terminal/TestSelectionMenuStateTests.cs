using Fuzn.TestFuzn.Internals.Terminal;

namespace Fuzn.TestFuzn.Tests.Terminal;

/// <summary>
/// Pins the test selection menu's pure interaction state: the clamped navigation (arrows,
/// paging by the visible rows, home/end), the window of matches scrolling only as far as the
/// highlight needs and pulled up when the matches shrink, the case-insensitive substring
/// filter keeping the highlight on a surviving test or falling to the first match, the number
/// entry rule (a purely numeric filter naming a listed test jumps to it without filtering),
/// backspace removing a surrogate pair whole, the key-to-action mapping including the keys
/// that must do nothing, and the empty list.
/// </summary>
[TestClass]
public class TestSelectionMenuStateTests : Test
{
    private static readonly string[] TestNames =
    {
        "Fuzn.Shop.Tests.CartTests.Verify_add_to_cart",
        "Fuzn.Shop.Tests.CatalogTests.Verify_browse_products",
        "Fuzn.Shop.Tests.CatalogTests.Verify_search_products",
        "Fuzn.Shop.Tests.CheckoutTests.Verify_checkout",
        "Fuzn.Shop.Tests.CheckoutTests.Verify_payment_declined",
        "Fuzn.Shop.Tests.LoginTests.Verify_login",
        "Fuzn.Shop.Tests.LoginTests.Verify_logout",
        "Fuzn.Shop.Tests.OrderTests.Verify_order_history",
        "Fuzn.Shop.Tests.OrderTests.Verify_order_tracking",
        "Fuzn.Shop.Tests.ProductTests.Verify_product_details",
        "Fuzn.Shop.Tests.ProductTests.Verify_product_reviews",
        "Fuzn.Shop.Tests.SearchLoadTests.Verify_search_under_load"
    };

    private static TestSelectionMenuState NewState(int visibleRowCount = 40)
    {
        return new TestSelectionMenuState(TestNames) { VisibleRowCount = visibleRowCount };
    }

    private static void Type(TestSelectionMenuState state, string text)
    {
        foreach (var character in text)
            state.AppendToFilter(character);
    }

    private static ConsoleKeyInfo Key(ConsoleKey key, char keyChar = '\0', bool shift = false, bool alt = false, bool control = false)
    {
        return new ConsoleKeyInfo(keyChar, key, shift, alt, control);
    }

    [Test]
    public async Task Verify_initial_state_and_clamped_navigation()
    {
        await Scenario()
            .Step("A fresh state has no filter, every test matching, the first test highlighted and the window at the top", context =>
            {
                var state = NewState();

                Assert.AreEqual(string.Empty, state.Filter);
                Assert.HasCount(12, state.MatchingTests);
                CollectionAssert.AreEqual(Enumerable.Range(0, 12).ToList(), state.MatchingTests.ToList());
                Assert.AreEqual(0, state.HighlightedTest);
                Assert.IsNull(state.EnteredTestNumber);
                Assert.AreEqual(0, state.FirstVisibleMatch);
                Assert.AreEqual(40, state.VisibleRowCount);
            })
            .Step("Down and up move one test and stay at the ends instead of wrapping", context =>
            {
                var state = NewState();

                state.MoveUp();
                Assert.AreEqual(0, state.HighlightedTest);

                state.MoveDown();
                state.MoveDown();
                Assert.AreEqual(2, state.HighlightedTest);

                state.MoveUp();
                Assert.AreEqual(1, state.HighlightedTest);

                state.MoveToLast();
                Assert.AreEqual(11, state.HighlightedTest);

                state.MoveDown();
                Assert.AreEqual(11, state.HighlightedTest);

                state.MoveToFirst();
                Assert.AreEqual(0, state.HighlightedTest);
            })
            .Step("Page down and page up move by the visible rows, clamped at the ends", context =>
            {
                var state = NewState(visibleRowCount: 4);

                state.PageDown();
                Assert.AreEqual(4, state.HighlightedTest);
                state.PageDown();
                Assert.AreEqual(8, state.HighlightedTest);
                state.PageDown();
                Assert.AreEqual(11, state.HighlightedTest);
                state.PageDown();
                Assert.AreEqual(11, state.HighlightedTest);

                state.PageUp();
                Assert.AreEqual(7, state.HighlightedTest);
                state.PageUp();
                Assert.AreEqual(3, state.HighlightedTest);
                state.PageUp();
                Assert.AreEqual(0, state.HighlightedTest);
                state.PageUp();
                Assert.AreEqual(0, state.HighlightedTest);
            })
            .Step("Without visible rows a page moves one test", context =>
            {
                var state = NewState(visibleRowCount: 0);

                state.PageDown();
                Assert.AreEqual(1, state.HighlightedTest);
            })
            .Run();
    }

    [Test]
    public async Task Verify_window_follows_the_highlight()
    {
        await Scenario()
            .Step("The window scrolls only when the highlight leaves it, by the least it needs", context =>
            {
                var state = NewState(visibleRowCount: 3);

                state.MoveDown();
                state.MoveDown();
                Assert.AreEqual(2, state.HighlightedTest);
                Assert.AreEqual(0, state.FirstVisibleMatch);

                state.MoveDown();
                Assert.AreEqual(3, state.HighlightedTest);
                Assert.AreEqual(1, state.FirstVisibleMatch);

                state.MoveToLast();
                Assert.AreEqual(9, state.FirstVisibleMatch);

                state.MoveUp();
                Assert.AreEqual(10, state.HighlightedTest);
                Assert.AreEqual(9, state.FirstVisibleMatch);

                state.MoveToFirst();
                Assert.AreEqual(0, state.FirstVisibleMatch);
            })
            .Step("Shrinking the visible rows keeps the highlight on screen; no rows parks the window at the highlight", context =>
            {
                var state = NewState(visibleRowCount: 3);
                for (var move = 0; move < 5; move++)
                    state.MoveDown();
                Assert.AreEqual(5, state.HighlightedTest);
                Assert.AreEqual(3, state.FirstVisibleMatch);

                state.VisibleRowCount = 2;
                Assert.AreEqual(4, state.FirstVisibleMatch);

                state.VisibleRowCount = 0;
                Assert.AreEqual(5, state.FirstVisibleMatch);

                state.VisibleRowCount = 4;
                Assert.AreEqual(5, state.FirstVisibleMatch);
                Assert.AreEqual(5, state.HighlightedTest);
            })
            .Step("Growing the visible rows beyond the matches pulls the window back to the top", context =>
            {
                var state = NewState(visibleRowCount: 3);
                state.MoveToLast();
                Assert.AreEqual(9, state.FirstVisibleMatch);

                state.VisibleRowCount = 20;
                Assert.AreEqual(0, state.FirstVisibleMatch);
                Assert.AreEqual(11, state.HighlightedTest);
            })
            .Step("A filter that leaves fewer matches than rows pulls the window up to show them all", context =>
            {
                var state = NewState(visibleRowCount: 3);
                state.MoveToLast();
                Assert.AreEqual(9, state.FirstVisibleMatch);

                Type(state, "search");

                CollectionAssert.AreEqual(new[] { 2, 11 }, state.MatchingTests.ToList());
                Assert.AreEqual(11, state.HighlightedTest);
                Assert.AreEqual(0, state.FirstVisibleMatch);
            })
            .Run();
    }

    [Test]
    public async Task Verify_filter_narrows_the_matches_and_keeps_a_surviving_highlight()
    {
        await Scenario()
            .Step("The filter is a case-insensitive substring search over the names", context =>
            {
                var state = NewState();

                Type(state, "cat");
                Assert.AreEqual("cat", state.Filter);
                CollectionAssert.AreEqual(new[] { 1, 2 }, state.MatchingTests.ToList());

                var upperCase = NewState();
                Type(upperCase, "CAT");
                CollectionAssert.AreEqual(new[] { 1, 2 }, upperCase.MatchingTests.ToList());

                var login = NewState();
                Type(login, "login");
                CollectionAssert.AreEqual(new[] { 5, 6 }, login.MatchingTests.ToList());
            })
            .Step("A highlighted test that keeps matching stays highlighted while the filter is typed and erased", context =>
            {
                var state = NewState();
                state.MoveDown();
                state.MoveDown();
                Assert.AreEqual(2, state.HighlightedTest);

                Type(state, "search");
                CollectionAssert.AreEqual(new[] { 2, 11 }, state.MatchingTests.ToList());
                Assert.AreEqual(2, state.HighlightedTest);

                for (var erase = 0; erase < 6; erase++)
                    state.RemoveLastFilterCharacter();

                Assert.AreEqual(string.Empty, state.Filter);
                Assert.HasCount(12, state.MatchingTests);
                Assert.AreEqual(2, state.HighlightedTest);
            })
            .Step("A highlighted test that stops matching gives way to the first match", context =>
            {
                var state = NewState();
                Assert.AreEqual(0, state.HighlightedTest);

                Type(state, "ca");
                CollectionAssert.AreEqual(new[] { 0, 1, 2 }, state.MatchingTests.ToList());
                Assert.AreEqual(0, state.HighlightedTest);

                state.AppendToFilter('t');
                CollectionAssert.AreEqual(new[] { 1, 2 }, state.MatchingTests.ToList());
                Assert.AreEqual(1, state.HighlightedTest);
            })
            .Step("No match leaves nothing highlighted, Enter is then a no-op, and erasing back restores the first match", context =>
            {
                var state = NewState();
                state.MoveDown();
                state.MoveDown();

                Type(state, "caz");
                Assert.IsEmpty(state.MatchingTests);
                Assert.IsNull(state.HighlightedTest);
                Assert.AreEqual(0, state.FirstVisibleMatch);
                Assert.AreEqual(TestSelectionMenuOutcome.Open, state.HandleKey(Key(ConsoleKey.Enter, '\r')));

                state.RemoveLastFilterCharacter();
                Assert.AreEqual("ca", state.Filter);
                Assert.AreEqual(0, state.HighlightedTest);
            })
            .Step("Backspace removes a surrogate pair as one character and is a no-op on an empty filter", context =>
            {
                var state = NewState();
                state.AppendToFilter('\uD83D');
                state.AppendToFilter('\uDE80');
                Assert.AreEqual("🚀", state.Filter);
                Assert.IsEmpty(state.MatchingTests);

                state.RemoveLastFilterCharacter();
                Assert.AreEqual(string.Empty, state.Filter);
                Assert.HasCount(12, state.MatchingTests);

                state.RemoveLastFilterCharacter();
                Assert.AreEqual(string.Empty, state.Filter);

                state.AppendToFilter('\uD83D');
                state.RemoveLastFilterCharacter();
                Assert.AreEqual(string.Empty, state.Filter);
            })
            .Run();
    }

    [Test]
    public async Task Verify_number_entry_jumps_to_the_numbered_test_without_filtering()
    {
        await Scenario()
            .Step("A purely numeric filter naming a listed test highlights it and keeps every test in the list", context =>
            {
                var state = NewState();
                state.AppendToFilter('1');
                Assert.AreEqual(1, state.EnteredTestNumber);
                Assert.AreEqual(0, state.HighlightedTest);
                Assert.HasCount(12, state.MatchingTests);

                state.AppendToFilter('2');
                Assert.AreEqual("12", state.Filter);
                Assert.AreEqual(12, state.EnteredTestNumber);
                Assert.AreEqual(11, state.HighlightedTest);
                Assert.HasCount(12, state.MatchingTests);
                Assert.AreEqual(TestSelectionMenuOutcome.TestSelected, state.HandleKey(Key(ConsoleKey.Enter, '\r')));

                var fifth = NewState();
                fifth.AppendToFilter('5');
                Assert.AreEqual(4, fifth.HighlightedTest);
            })
            .Step("A number naming no test — zero or past the count — is searched for as text", context =>
            {
                var state = NewState();
                Type(state, "13");
                Assert.IsNull(state.EnteredTestNumber);
                Assert.IsEmpty(state.MatchingTests);
                Assert.IsNull(state.HighlightedTest);

                var zero = NewState();
                zero.AppendToFilter('0');
                Assert.IsNull(zero.EnteredTestNumber);
                Assert.IsEmpty(zero.MatchingTests);
            })
            .Step("A number entry scrolls the window to the named test", context =>
            {
                var state = NewState(visibleRowCount: 3);
                Type(state, "12");
                Assert.AreEqual(9, state.FirstVisibleMatch);
            })
            .Step("Erasing the number keeps the highlight where the jump left it", context =>
            {
                var state = NewState();
                Type(state, "12");
                state.RemoveLastFilterCharacter();
                Assert.AreEqual("1", state.Filter);
                Assert.AreEqual(0, state.HighlightedTest);

                Type(state, "1");
                Assert.AreEqual(11, state.EnteredTestNumber);
                Assert.AreEqual(10, state.HighlightedTest);

                // Back to "1" jumps to the first test again; erasing that keeps it highlighted.
                state.RemoveLastFilterCharacter();
                Assert.AreEqual(0, state.HighlightedTest);
                state.RemoveLastFilterCharacter();
                Assert.AreEqual(string.Empty, state.Filter);
                Assert.IsNull(state.EnteredTestNumber);
                Assert.AreEqual(0, state.HighlightedTest);
                Assert.HasCount(12, state.MatchingTests);
            })
            .Step("Digits mixed with other characters are a text search", context =>
            {
                var state = NewState();
                Type(state, "1a");
                Assert.IsNull(state.EnteredTestNumber);
                Assert.IsEmpty(state.MatchingTests);
            })
            .Step("Navigation after a number entry moves the highlight away from the named test; Enter then runs the highlighted one, and the list stays complete", context =>
            {
                var state = NewState(visibleRowCount: 4);
                Type(state, "3");
                Assert.AreEqual(2, state.HighlightedTest);

                state.MoveDown();
                Assert.AreEqual(3, state.HighlightedTest);
                Assert.AreEqual(3, state.EnteredTestNumber);
                Assert.AreEqual("3", state.Filter);
                Assert.HasCount(12, state.MatchingTests);
                Assert.AreEqual(TestSelectionMenuOutcome.TestSelected, state.HandleKey(Key(ConsoleKey.Enter, '\r')));
                Assert.AreEqual(3, state.HighlightedTest);

                state.PageDown();
                Assert.AreEqual(7, state.HighlightedTest);
                state.MoveToFirst();
                Assert.AreEqual(0, state.HighlightedTest);
                Assert.AreEqual(3, state.EnteredTestNumber);

                // Erasing the digit keeps the navigated highlight; typing it again jumps back.
                state.RemoveLastFilterCharacter();
                Assert.AreEqual(0, state.HighlightedTest);
                Assert.IsNull(state.EnteredTestNumber);
                state.AppendToFilter('3');
                Assert.AreEqual(2, state.HighlightedTest);
                Assert.AreEqual(3, state.EnteredTestNumber);
            })
            .Run();
    }

    [Test]
    public async Task Verify_key_mapping()
    {
        await Scenario()
            .Step("Arrows, paging, home and end move the highlight and keep the menu open", context =>
            {
                var state = NewState(visibleRowCount: 4);

                Assert.AreEqual(TestSelectionMenuOutcome.Open, state.HandleKey(Key(ConsoleKey.DownArrow)));
                Assert.AreEqual(1, state.HighlightedTest);
                Assert.AreEqual(TestSelectionMenuOutcome.Open, state.HandleKey(Key(ConsoleKey.UpArrow)));
                Assert.AreEqual(0, state.HighlightedTest);
                Assert.AreEqual(TestSelectionMenuOutcome.Open, state.HandleKey(Key(ConsoleKey.PageDown)));
                Assert.AreEqual(4, state.HighlightedTest);
                Assert.AreEqual(TestSelectionMenuOutcome.Open, state.HandleKey(Key(ConsoleKey.PageUp)));
                Assert.AreEqual(0, state.HighlightedTest);
                Assert.AreEqual(TestSelectionMenuOutcome.Open, state.HandleKey(Key(ConsoleKey.End)));
                Assert.AreEqual(11, state.HighlightedTest);
                Assert.AreEqual(TestSelectionMenuOutcome.Open, state.HandleKey(Key(ConsoleKey.Home)));
                Assert.AreEqual(0, state.HighlightedTest);
            })
            .Step("Enter selects the highlighted test and Escape quits, each leaving the state as it was", context =>
            {
                var state = NewState();
                state.MoveDown();

                Assert.AreEqual(TestSelectionMenuOutcome.TestSelected, state.HandleKey(Key(ConsoleKey.Enter, '\r')));
                Assert.AreEqual(1, state.HighlightedTest);
                Assert.AreEqual(TestSelectionMenuOutcome.Quit, state.HandleKey(Key(ConsoleKey.Escape, '')));
                Assert.AreEqual(1, state.HighlightedTest);
            })
            .Step("Printable characters type into the filter, upper case and punctuation included, and Backspace erases", context =>
            {
                var state = NewState();

                Assert.AreEqual(TestSelectionMenuOutcome.Open, state.HandleKey(Key(ConsoleKey.C, 'C', shift: true)));
                Assert.AreEqual(TestSelectionMenuOutcome.Open, state.HandleKey(Key(ConsoleKey.A, 'a')));
                Assert.AreEqual(TestSelectionMenuOutcome.Open, state.HandleKey(Key(ConsoleKey.OemPeriod, '.')));
                Assert.AreEqual("Ca.", state.Filter);

                Assert.AreEqual(TestSelectionMenuOutcome.Open, state.HandleKey(Key(ConsoleKey.Backspace, '\b')));
                Assert.AreEqual("Ca", state.Filter);
                CollectionAssert.AreEqual(new[] { 0, 1, 2 }, state.MatchingTests.ToList());
            })
            .Step("Alt and Control chords, control characters and keys without a character do nothing", context =>
            {
                var state = NewState();

                Assert.AreEqual(TestSelectionMenuOutcome.Open, state.HandleKey(Key(ConsoleKey.A, 'a', alt: true)));
                Assert.AreEqual(TestSelectionMenuOutcome.Open, state.HandleKey(Key(ConsoleKey.A, '', control: true)));
                Assert.AreEqual(TestSelectionMenuOutcome.Open, state.HandleKey(Key(ConsoleKey.Tab, '\t')));
                Assert.AreEqual(TestSelectionMenuOutcome.Open, state.HandleKey(Key(ConsoleKey.F1)));
                Assert.AreEqual(TestSelectionMenuOutcome.Open, state.HandleKey(Key(ConsoleKey.Delete, '')));
                Assert.AreEqual(TestSelectionMenuOutcome.Open, state.HandleKey(Key(ConsoleKey.LeftArrow)));

                Assert.AreEqual(string.Empty, state.Filter);
                Assert.AreEqual(0, state.HighlightedTest);
                Assert.HasCount(12, state.MatchingTests);
            })
            .Step("A chord with Alt or Control does nothing for every key: Ctrl+Escape does not quit, Alt+Enter does not select, Alt+Down and Ctrl+Backspace do not move or erase", context =>
            {
                var state = NewState();
                state.MoveDown();
                state.AppendToFilter('a');
                Assert.AreEqual("a", state.Filter);

                Assert.AreEqual(TestSelectionMenuOutcome.Open, state.HandleKey(Key(ConsoleKey.Escape, '', control: true)));
                Assert.AreEqual(TestSelectionMenuOutcome.Open, state.HandleKey(Key(ConsoleKey.Enter, '\r', alt: true)));
                Assert.AreEqual(TestSelectionMenuOutcome.Open, state.HandleKey(Key(ConsoleKey.Enter, '\r', control: true)));
                Assert.AreEqual(TestSelectionMenuOutcome.Open, state.HandleKey(Key(ConsoleKey.DownArrow, alt: true)));
                Assert.AreEqual(TestSelectionMenuOutcome.Open, state.HandleKey(Key(ConsoleKey.DownArrow, control: true)));
                Assert.AreEqual(TestSelectionMenuOutcome.Open, state.HandleKey(Key(ConsoleKey.End, control: true)));
                Assert.AreEqual(TestSelectionMenuOutcome.Open, state.HandleKey(Key(ConsoleKey.Backspace, '\b', control: true)));

                Assert.AreEqual("a", state.Filter);
                Assert.AreEqual(1, state.HighlightedTest);
            })
            .Run();
    }

    [Test]
    public async Task Verify_empty_list_and_argument_guards()
    {
        await Scenario()
            .Step("With no tests nothing is highlighted, navigation and typing keep it so, Enter is a no-op and Escape quits", context =>
            {
                var state = new TestSelectionMenuState(Array.Empty<string>()) { VisibleRowCount = 10 };

                Assert.IsEmpty(state.MatchingTests);
                Assert.IsNull(state.HighlightedTest);

                state.MoveDown();
                state.MoveToLast();
                state.PageDown();
                Assert.IsNull(state.HighlightedTest);
                Assert.AreEqual(0, state.FirstVisibleMatch);

                state.AppendToFilter('1');
                Assert.IsNull(state.EnteredTestNumber);
                Assert.IsNull(state.HighlightedTest);

                Assert.AreEqual(TestSelectionMenuOutcome.Open, state.HandleKey(Key(ConsoleKey.Enter, '\r')));
                Assert.AreEqual(TestSelectionMenuOutcome.Quit, state.HandleKey(Key(ConsoleKey.Escape, '')));
            })
            .Step("A single test is the highlighted one and its number entry names it", context =>
            {
                var state = new TestSelectionMenuState(new[] { "Fuzn.Shop.Tests.CartTests.Verify_add_to_cart" });

                Assert.AreEqual(0, state.HighlightedTest);
                state.AppendToFilter('1');
                Assert.AreEqual(1, state.EnteredTestNumber);
                Assert.AreEqual(0, state.HighlightedTest);
            })
            .Step("Null names are rejected", context =>
            {
                Assert.ThrowsExactly<ArgumentNullException>(() => new TestSelectionMenuState(null!));
                Assert.ThrowsExactly<ArgumentNullException>(() => new TestSelectionMenuState(new string[] { null! }));
            })
            .Run();
    }
}
