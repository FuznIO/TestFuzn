using System.Globalization;
using Fuzn.TestFuzn.Internals.Terminal;

namespace Fuzn.TestFuzn.Tests.Terminal;

/// <summary>
/// Pins every binding of the pure <see cref="LiveDashboardKeyHandler"/> on a
/// <see cref="LiveDashboardViewState"/> without a terminal: the view switches (and what
/// <c>2</c> does without steps), the step selection moving over the first scenario's steps
/// and stopping at both ends, the error log scrolling from its top, Enter with and without a
/// selection or steps, Escape closing the help before it leaves a view, the pause toggle, the
/// time window ladder in both directions from every rung, holding at its ends, and from a
/// window off it, the help toggle, the chords and unknown keys that change nothing — the quit
/// key among them — and that the state handed in is never changed.
/// </summary>
[TestClass]
public class LiveDashboardKeyHandlerTests : Test
{
    private static readonly ConsoleKeyInfo Up = Key(ConsoleKey.UpArrow);
    private static readonly ConsoleKeyInfo Down = Key(ConsoleKey.DownArrow);
    private static readonly ConsoleKeyInfo Enter = Key(ConsoleKey.Enter, '\r');
    private static readonly ConsoleKeyInfo Escape = Key(ConsoleKey.Escape, '\x1b');
    private static readonly ConsoleKeyInfo Pause = Typed('p');
    private static readonly ConsoleKeyInfo Widen = Key(ConsoleKey.OemPlus, '+', shift: true);
    private static readonly ConsoleKeyInfo Narrow = Key(ConsoleKey.OemMinus, '-');
    private static readonly ConsoleKeyInfo Help = Key(ConsoleKey.Oem2, '?', shift: true);

    private static ConsoleKeyInfo Key(ConsoleKey key, char keyChar = '\0', bool shift = false, bool alt = false, bool control = false)
    {
        return new ConsoleKeyInfo(keyChar, key, shift, alt, control);
    }

    /// <summary>A letter or digit as typing it produces it: the character with its key, Shift for an upper-case letter.</summary>
    private static ConsoleKeyInfo Typed(char keyChar)
    {
        return new ConsoleKeyInfo(keyChar, (ConsoleKey)char.ToUpperInvariant(keyChar), shift: char.IsAsciiLetterUpper(keyChar), alt: false, control: false);
    }

    /// <summary>
    /// Two scenarios: the first with the given number of steps, the second always with five,
    /// so a binding that counted the wrong scenario's steps would show.
    /// </summary>
    private static LiveMetricsSnapshot[] Snapshots(int stepCount)
    {
        return new[] { new LiveMetricsSnapshot { ScenarioName = "First", Steps = Steps(stepCount) }, new LiveMetricsSnapshot { ScenarioName = "Second", Steps = Steps(5) } };
    }

    private static LiveStepMetrics[] Steps(int count)
    {
        var steps = new LiveStepMetrics[count];
        for (var index = 0; index < count; index++)
            steps[index] = new LiveStepMetrics { Name = "Step " + (index + 1) };

        return steps;
    }

    private static LiveDashboardViewState Apply(LiveDashboardViewState state, ConsoleKeyInfo key, int stepCount = 3)
    {
        return LiveDashboardKeyHandler.Apply(state, key, Snapshots(stepCount));
    }

    private static void AssertUnchanged(LiveDashboardViewState expected, LiveDashboardViewState actual)
    {
        Assert.AreEqual(expected.View, actual.View);
        Assert.AreEqual(expected.SelectedStepIndex, actual.SelectedStepIndex);
        Assert.AreEqual(expected.ErrorLogScroll, actual.ErrorLogScroll);
        Assert.AreEqual(expected.IsPaused, actual.IsPaused);
        Assert.AreEqual(expected.TimeWindow, actual.TimeWindow);
        Assert.AreEqual(expected.ShowHelp, actual.ShowHelp);
    }

    [Test]
    public async Task Verify_number_keys_switch_views()
    {
        await Scenario()
            .Step("1, 2 and 3 switch to the overview, the step detail and the error log from any view", context =>
            {
                var state = LiveDashboardViewState.Default;

                state = Apply(state, Typed('3'));
                Assert.AreEqual(LiveDashboardView.ErrorLog, state.View);

                state = Apply(state, Typed('2'));
                Assert.AreEqual(LiveDashboardView.StepDetail, state.View);

                state = Apply(state, Typed('1'));
                Assert.AreEqual(LiveDashboardView.Overview, state.View);

                state = Apply(state, Typed('2'));
                Assert.AreEqual(LiveDashboardView.StepDetail, state.View);

                state = Apply(state, Typed('3'));
                Assert.AreEqual(LiveDashboardView.ErrorLog, state.View);

                state = Apply(state, Typed('1'));
                Assert.AreEqual(LiveDashboardView.Overview, state.View);
            })
            .Step("2 opens the detail on the selected step, selecting the first step when none is and clamping a stale selection into the steps", context =>
            {
                var opened = Apply(LiveDashboardViewState.Default, Typed('2'));
                Assert.AreEqual(LiveDashboardView.StepDetail, opened.View);
                Assert.AreEqual(0, opened.SelectedStepIndex);

                var kept = Apply(LiveDashboardViewState.Default with { SelectedStepIndex = 2 }, Typed('2'));
                Assert.AreEqual(2, kept.SelectedStepIndex);

                var clamped = Apply(LiveDashboardViewState.Default with { SelectedStepIndex = 7 }, Typed('2'));
                Assert.AreEqual(2, clamped.SelectedStepIndex);
            })
            .Step("2 without steps in the first scenario changes nothing — the view stays where it is — while 3 always switches", context =>
            {
                var overview = Apply(LiveDashboardViewState.Default, Typed('2'), stepCount: 0);
                AssertUnchanged(LiveDashboardViewState.Default, overview);

                var log = LiveDashboardViewState.Default with { View = LiveDashboardView.ErrorLog, ErrorLogScroll = 4 };
                AssertUnchanged(log, Apply(log, Typed('2'), stepCount: 0));

                Assert.AreEqual(LiveDashboardView.ErrorLog, Apply(LiveDashboardViewState.Default, Typed('3'), stepCount: 0).View);
                Assert.AreEqual(LiveDashboardView.ErrorLog, LiveDashboardKeyHandler.Apply(LiveDashboardViewState.Default, Typed('3'), Array.Empty<LiveMetricsSnapshot>()).View);
            })
            .Step("Switching views keeps the selection, the scroll, the pause, the window and the help", context =>
            {
                var state = new LiveDashboardViewState { SelectedStepIndex = 1, ErrorLogScroll = 3, IsPaused = true, TimeWindow = 120, ShowHelp = true };

                var log = Apply(state, Typed('3'));
                AssertUnchanged(state with { View = LiveDashboardView.ErrorLog }, log);

                var overview = Apply(log, Typed('1'));
                AssertUnchanged(state, overview);
            })
            .Run();
    }

    [Test]
    public async Task Verify_arrows_move_the_step_selection_clamped_at_both_ends()
    {
        await Scenario()
            .Step("Without a selection the first press of either arrow selects the first step", context =>
            {
                Assert.AreEqual(0, Apply(LiveDashboardViewState.Default, Down).SelectedStepIndex);
                Assert.AreEqual(0, Apply(LiveDashboardViewState.Default, Up).SelectedStepIndex);
            })
            .Step("Three steps: down walks 0, 1, 2 and stays at 2; up walks back to 0 and stays there — no wrap-around", context =>
            {
                var state = Apply(LiveDashboardViewState.Default, Down);
                state = Apply(state, Down);
                Assert.AreEqual(1, state.SelectedStepIndex);
                state = Apply(state, Down);
                Assert.AreEqual(2, state.SelectedStepIndex);
                state = Apply(state, Down);
                Assert.AreEqual(2, state.SelectedStepIndex);

                state = Apply(state, Up);
                Assert.AreEqual(1, state.SelectedStepIndex);
                state = Apply(state, Up);
                Assert.AreEqual(0, state.SelectedStepIndex);
                state = Apply(state, Up);
                Assert.AreEqual(0, state.SelectedStepIndex);
                Assert.AreEqual(LiveDashboardView.Overview, state.View);
            })
            .Step("One step: the selection lands on it and neither arrow leaves it", context =>
            {
                var state = Apply(LiveDashboardViewState.Default, Down, stepCount: 1);
                Assert.AreEqual(0, state.SelectedStepIndex);
                Assert.AreEqual(0, Apply(state, Down, stepCount: 1).SelectedStepIndex);
                Assert.AreEqual(0, Apply(state, Up, stepCount: 1).SelectedStepIndex);
            })
            .Step("No steps: the selection stays null, and a stale selection past the steps is pulled back into them", context =>
            {
                Assert.IsNull(Apply(LiveDashboardViewState.Default, Down, stepCount: 0).SelectedStepIndex);
                Assert.IsNull(Apply(LiveDashboardViewState.Default, Up, stepCount: 0).SelectedStepIndex);
                Assert.IsNull(LiveDashboardKeyHandler.Apply(LiveDashboardViewState.Default, Down, Array.Empty<LiveMetricsSnapshot>()).SelectedStepIndex);

                var stale = LiveDashboardViewState.Default with { SelectedStepIndex = 7 };
                Assert.AreEqual(2, Apply(stale, Down).SelectedStepIndex);
                Assert.AreEqual(2, Apply(stale, Up).SelectedStepIndex);
            })
            .Step("The steps counted are the first scenario's, not the second's five", context =>
            {
                var state = Apply(LiveDashboardViewState.Default, Down, stepCount: 1);
                state = Apply(state, Down, stepCount: 1);
                state = Apply(state, Down, stepCount: 1);
                Assert.AreEqual(0, state.SelectedStepIndex);
            })
            .Step("In the step detail the arrows move the selection the same way", context =>
            {
                var detail = LiveDashboardViewState.Default with { View = LiveDashboardView.StepDetail, SelectedStepIndex = 1 };
                var down = Apply(detail, Down);
                Assert.AreEqual(2, down.SelectedStepIndex);
                Assert.AreEqual(LiveDashboardView.StepDetail, down.View);
                Assert.AreEqual(0, Apply(detail, Up).SelectedStepIndex);
            })
            .Run();
    }

    [Test]
    public async Task Verify_arrows_scroll_the_error_log()
    {
        await Scenario()
            .Step("In the error log down scrolls one entry per press without an upper limit, up scrolls back and stops at the top, and the step selection stays put", context =>
            {
                var state = LiveDashboardViewState.Default with { View = LiveDashboardView.ErrorLog, SelectedStepIndex = 1 };

                state = Apply(state, Down);
                Assert.AreEqual(1, state.ErrorLogScroll);
                state = Apply(state, Down);
                Assert.AreEqual(2, state.ErrorLogScroll);
                Assert.AreEqual(1, state.SelectedStepIndex);

                state = Apply(state, Up);
                Assert.AreEqual(1, state.ErrorLogScroll);
                state = Apply(state, Up);
                Assert.AreEqual(0, state.ErrorLogScroll);
                state = Apply(state, Up);
                Assert.AreEqual(0, state.ErrorLogScroll);
                Assert.AreEqual(LiveDashboardView.ErrorLog, state.View);
                Assert.AreEqual(1, state.SelectedStepIndex);

                // The top end is the log view's to clamp: the handler scrolls on regardless
                // of how many errors there are (none here).
                var deep = Apply(LiveDashboardViewState.Default with { View = LiveDashboardView.ErrorLog, ErrorLogScroll = 10 }, Down, stepCount: 0);
                Assert.AreEqual(11, deep.ErrorLogScroll);
            })
            .Run();
    }

    [Test]
    public async Task Verify_enter_opens_the_step_detail()
    {
        await Scenario()
            .Step("Enter with a selection opens the detail on it; without one it selects the first step and opens it", context =>
            {
                var selected = Apply(LiveDashboardViewState.Default with { SelectedStepIndex = 2 }, Enter);
                Assert.AreEqual(LiveDashboardView.StepDetail, selected.View);
                Assert.AreEqual(2, selected.SelectedStepIndex);

                var first = Apply(LiveDashboardViewState.Default, Enter);
                Assert.AreEqual(LiveDashboardView.StepDetail, first.View);
                Assert.AreEqual(0, first.SelectedStepIndex);
            })
            .Step("Enter without steps changes nothing, and from the error log it opens the detail like anywhere else", context =>
            {
                AssertUnchanged(LiveDashboardViewState.Default, Apply(LiveDashboardViewState.Default, Enter, stepCount: 0));

                var log = LiveDashboardViewState.Default with { View = LiveDashboardView.ErrorLog, ErrorLogScroll = 2 };
                var opened = Apply(log, Enter);
                Assert.AreEqual(LiveDashboardView.StepDetail, opened.View);
                Assert.AreEqual(0, opened.SelectedStepIndex);
                Assert.AreEqual(2, opened.ErrorLogScroll);
            })
            .Run();
    }

    [Test]
    public async Task Verify_escape_closes_the_help_before_it_returns_to_the_overview()
    {
        await Scenario()
            .Step("With the help up Escape closes it and leaves the view; the next Escape returns to the overview with the selection kept; in the overview it changes nothing", context =>
            {
                var state = new LiveDashboardViewState { View = LiveDashboardView.StepDetail, SelectedStepIndex = 1, ShowHelp = true, TimeWindow = 60 };

                state = Apply(state, Escape);
                Assert.IsFalse(state.ShowHelp);
                Assert.AreEqual(LiveDashboardView.StepDetail, state.View);

                state = Apply(state, Escape);
                Assert.AreEqual(LiveDashboardView.Overview, state.View);
                Assert.AreEqual(1, state.SelectedStepIndex);
                Assert.AreEqual(60, state.TimeWindow);

                AssertUnchanged(state, Apply(state, Escape));

                var log = LiveDashboardViewState.Default with { View = LiveDashboardView.ErrorLog, ErrorLogScroll = 5 };
                var back = Apply(log, Escape);
                Assert.AreEqual(LiveDashboardView.Overview, back.View);
                Assert.AreEqual(5, back.ErrorLogScroll);
            })
            .Run();
    }

    [Test]
    public async Task Verify_p_toggles_the_pause_and_question_mark_toggles_the_help()
    {
        await Scenario()
            .Step("p pauses, p again resumes, and P with Shift held does the same", context =>
            {
                var paused = Apply(LiveDashboardViewState.Default, Pause);
                Assert.IsTrue(paused.IsPaused);
                AssertUnchanged(LiveDashboardViewState.Default with { IsPaused = true }, paused);

                var resumed = Apply(paused, Pause);
                Assert.IsFalse(resumed.IsPaused);

                Assert.IsTrue(Apply(LiveDashboardViewState.Default, Typed('P')).IsPaused);
            })
            .Step("? shows the help, ? again hides it, and the other bindings still apply while it is up", context =>
            {
                var help = Apply(LiveDashboardViewState.Default, Help);
                Assert.IsTrue(help.ShowHelp);
                AssertUnchanged(LiveDashboardViewState.Default with { ShowHelp = true }, help);

                Assert.IsFalse(Apply(help, Help).ShowHelp);

                var log = Apply(help, Typed('3'));
                Assert.AreEqual(LiveDashboardView.ErrorLog, log.View);
                Assert.IsTrue(log.ShowHelp);

                var paused = Apply(help, Pause);
                Assert.IsTrue(paused.IsPaused);
                Assert.IsTrue(paused.ShowHelp);
            })
            .Run();
    }

    [Test]
    public async Task Verify_plus_and_minus_move_the_time_window_along_the_ladder_without_wrapping()
    {
        await Scenario()
            .Step("The ladder's rungs are 60, 120 and 300, with every sample — the default — at the top", context =>
            {
                CollectionAssert.AreEqual(new[] { 60, 120, 300 }, LiveDashboardKeyHandler.TimeWindowLadder.ToList());
                Assert.IsNull(LiveDashboardViewState.Default.TimeWindow);
            })
            .Step("+ widens one rung at a time and holds at the top: 60 → 120 → 300 → every sample → every sample, so the first + on a fresh dashboard changes nothing", context =>
            {
                var state = LiveDashboardViewState.Default with { TimeWindow = 60 };

                state = Apply(state, Widen);
                Assert.AreEqual(120, state.TimeWindow);
                state = Apply(state, Widen);
                Assert.AreEqual(300, state.TimeWindow);
                state = Apply(state, Widen);
                Assert.IsNull(state.TimeWindow);
                state = Apply(state, Widen);
                Assert.IsNull(state.TimeWindow);

                AssertUnchanged(LiveDashboardViewState.Default, Apply(LiveDashboardViewState.Default, Widen));
            })
            .Step("- narrows one rung at a time and holds at the bottom: every sample → 300 → 120 → 60 → 60", context =>
            {
                var state = LiveDashboardViewState.Default;

                state = Apply(state, Narrow);
                Assert.AreEqual(300, state.TimeWindow);
                state = Apply(state, Narrow);
                Assert.AreEqual(120, state.TimeWindow);
                state = Apply(state, Narrow);
                Assert.AreEqual(60, state.TimeWindow);
                state = Apply(state, Narrow);
                Assert.AreEqual(60, state.TimeWindow);
            })
            .Step("From every rung and the top, each key moves exactly one rung its way, and the ends hold", context =>
            {
                var transitions = new (int? From, int? Wider, int? Narrower)[]
                {
                    (null, null, 300),
                    (300, null, 120),
                    (120, 300, 60),
                    (60, 120, 60)
                };

                foreach (var transition in transitions)
                {
                    var from = transition.From == null ? "every sample" : transition.From.Value.ToString(CultureInfo.InvariantCulture);
                    var state = LiveDashboardViewState.Default with { TimeWindow = transition.From };

                    Assert.AreEqual(transition.Wider, Apply(state, Widen).TimeWindow, "+ from " + from);
                    Assert.AreEqual(transition.Narrower, Apply(state, Narrow).TimeWindow, "- from " + from);
                }
            })
            .Step("A window off the ladder snaps to the nearest rung in the pressed direction, and to the ladder's end when there is none that way", context =>
            {
                var betweenRungs = LiveDashboardViewState.Default with { TimeWindow = 200 };
                Assert.AreEqual(300, Apply(betweenRungs, Widen).TimeWindow);
                Assert.AreEqual(120, Apply(betweenRungs, Narrow).TimeWindow);

                var belowTheBottom = LiveDashboardViewState.Default with { TimeWindow = 45 };
                Assert.AreEqual(60, Apply(belowTheBottom, Widen).TimeWindow);
                Assert.AreEqual(60, Apply(belowTheBottom, Narrow).TimeWindow);

                var aboveTheTopRung = LiveDashboardViewState.Default with { TimeWindow = 400 };
                Assert.IsNull(Apply(aboveTheTopRung, Widen).TimeWindow);
                Assert.AreEqual(300, Apply(aboveTheTopRung, Narrow).TimeWindow);

                // A window below 1 reads as every sample to the widgets; to the keys it is a
                // window below the bottom rung like any other.
                var belowOne = LiveDashboardViewState.Default with { TimeWindow = 0 };
                Assert.AreEqual(60, Apply(belowOne, Widen).TimeWindow);
                Assert.AreEqual(60, Apply(belowOne, Narrow).TimeWindow);
            })
            .Step("The window keys change nothing else, and a keypad + or - counts by its character", context =>
            {
                var state = new LiveDashboardViewState { View = LiveDashboardView.StepDetail, SelectedStepIndex = 2, IsPaused = true, TimeWindow = 120 };
                AssertUnchanged(state with { TimeWindow = 300 }, Apply(state, Widen));
                AssertUnchanged(state with { TimeWindow = 300 }, Apply(state, Key(ConsoleKey.Add, '+')));
                AssertUnchanged(state with { TimeWindow = 60 }, Apply(state, Key(ConsoleKey.Subtract, '-')));
            })
            .Run();
    }

    [Test]
    public async Task Verify_chords_and_unknown_keys_change_nothing()
    {
        await Scenario()
            .Step("A chord with Alt or Control is never a binding, whatever the key", context =>
            {
                var state = new LiveDashboardViewState { View = LiveDashboardView.StepDetail, SelectedStepIndex = 1, ErrorLogScroll = 2, TimeWindow = 120 };

                AssertUnchanged(state, Apply(state, Key(ConsoleKey.D1, '1', alt: true)));
                AssertUnchanged(state, Apply(state, Key(ConsoleKey.D3, '3', control: true)));
                AssertUnchanged(state, Apply(state, Key(ConsoleKey.P, 'p', alt: true)));
                AssertUnchanged(state, Apply(state, Key(ConsoleKey.P, '\x10', control: true)));
                AssertUnchanged(state, Apply(state, Key(ConsoleKey.DownArrow, control: true)));
                AssertUnchanged(state, Apply(state, Key(ConsoleKey.UpArrow, alt: true)));
                AssertUnchanged(state, Apply(state, Key(ConsoleKey.Enter, '\r', alt: true)));
                AssertUnchanged(state, Apply(state, Key(ConsoleKey.Escape, '\x1b', control: true)));
                AssertUnchanged(state, Apply(state, Key(ConsoleKey.OemPlus, '+', shift: true, alt: true)));
                AssertUnchanged(state, Apply(state, Key(ConsoleKey.Oem2, '?', shift: true, control: true)));
            })
            .Step("Keys without a binding change nothing: letters, space, Tab, function keys, Home, End, the reserved Page Up and Page Down, and the quit key in either case", context =>
            {
                var state = new LiveDashboardViewState { View = LiveDashboardView.ErrorLog, SelectedStepIndex = 1, ErrorLogScroll = 2, IsPaused = true };

                AssertUnchanged(state, Apply(state, Typed('x')));
                AssertUnchanged(state, Apply(state, Typed('4')));
                AssertUnchanged(state, Apply(state, Key(ConsoleKey.Spacebar, ' ')));
                AssertUnchanged(state, Apply(state, Key(ConsoleKey.Tab, '\t')));
                AssertUnchanged(state, Apply(state, Key(ConsoleKey.F1)));
                AssertUnchanged(state, Apply(state, Key(ConsoleKey.Home)));
                AssertUnchanged(state, Apply(state, Key(ConsoleKey.End)));
                AssertUnchanged(state, Apply(state, Key(ConsoleKey.PageUp)));
                AssertUnchanged(state, Apply(state, Key(ConsoleKey.PageDown)));
                AssertUnchanged(state, Apply(state, Key(ConsoleKey.Backspace, '\b')));
                AssertUnchanged(state, Apply(state, Typed(LiveDashboardLayout.QuitKey)));
                AssertUnchanged(state, Apply(state, Typed('Q')));
            })
            .Step("The state handed in is never changed: the handler returns a new one", context =>
            {
                var original = new LiveDashboardViewState { SelectedStepIndex = 1, TimeWindow = 60 };

                var next = Apply(original, Down);
                Assert.AreEqual(2, next.SelectedStepIndex);
                Assert.AreEqual(1, original.SelectedStepIndex);

                var paused = Apply(original, Pause);
                Assert.IsTrue(paused.IsPaused);
                Assert.IsFalse(original.IsPaused);
                Assert.AreEqual(60, original.TimeWindow);
            })
            .Step("Null snapshots are rejected", context =>
            {
                Assert.ThrowsExactly<ArgumentNullException>(() => LiveDashboardKeyHandler.Apply(LiveDashboardViewState.Default, Down, null!));
            })
            .Run();
    }
}
