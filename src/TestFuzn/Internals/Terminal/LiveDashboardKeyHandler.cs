namespace Fuzn.TestFuzn.Internals.Terminal;

/// <summary>
/// The live dashboard's key bindings as a pure function: <see cref="Apply"/> takes the viewer's
/// <see cref="LiveDashboardViewState"/>, one key press and the scenarios' current snapshots and
/// returns the next state, touching nothing — no console, no clock — so every binding is
/// testable without a terminal, and the console manager's loop, which reads the keys, keeps the
/// one state it hands the layout. The bindings:
/// <list type="bullet">
/// <item><description><c>1</c>, <c>2</c>, <c>3</c> switch to the overview, the step detail and the error log. <c>2</c> is <c>Enter</c> under another name: it opens the selected step, selects the first displayed step when none is, and does nothing at all — the view stays — while the first scenario has no steps, since the detail would have no subject; <c>1</c> and <c>3</c> always switch.</description></item>
/// <item><description><c>↑</c> and <c>↓</c> move the step selection in the overview and the step detail — over the first scenario's steps in the order the Steps table displays them (<see cref="LiveDashboardLayout.DisplayedStepOrder"/>, the pain order), the selection being the step's declaration index (<see cref="LiveDashboardViewState.SelectedStepIndex"/>): the first press selects the top row's step from no selection, later ones move one row up or down and stop at the ends (no wrap-around), and without steps the selection stays as it is — and scroll the error log in the log view, one entry per press, clamped into the first scenario's entries (<see cref="LiveDashboardViewState.ErrorLogScroll"/>: never above the top, never past the last entry, and 0 without an entry); the log view clamps the offset further, to the last one its page is still full from, since only it knows the page. A selection that names no step of the first scenario (stale: the step has gone from the snapshot) counts as none, so the next press selects the top row's step.</description></item>
/// <item><description><c>Page Up</c> and <c>Page Down</c> scroll the error log by <see cref="ErrorLogPageSize"/> entries in the log view, clamped the same way, and do nothing in the other views — the Steps table has no page to turn.</description></item>
/// <item><description><c>Enter</c> opens the step detail on the selected step, selecting the top row's step when none is selected (or the selection names no step), and does nothing while the first scenario has no steps.</description></item>
/// <item><description><c>Esc</c> closes the help when it is up, else returns to the overview (the selection and the other choices stay).</description></item>
/// <item><description><c>p</c> (either case, like the quit key) toggles the pause.</description></item>
/// <item><description><c>+</c> and <c>-</c> move the time window one rung along the ladder 60 → 120 → 300 → every sample (<see cref="TimeWindowLadder"/>, then null), which does not wrap: <c>+</c> widens to the narrowest rung wider than the window, and to every sample when there is none — so every sample, the default, stays where it is; <c>-</c> narrows to the widest rung narrower than the window (every sample counts as wider than every rung), and to the narrowest rung when there is none — so 60 stays where it is. The same rule snaps a window that is not on the ladder to the nearest rung in the pressed direction, and to the ladder's end past its last rung: 200 goes to 300 on <c>+</c> and to 120 on <c>-</c>, 400 to every sample on <c>+</c> and to 300 on <c>-</c>, 45 to 60 either way.</description></item>
/// <item><description><c>?</c> toggles the help. The help is not modal: every other binding still applies while it is up.</description></item>
/// </list>
/// The quit key is not here: the console manager acts on it before consulting the handler, and
/// the handler leaves the state alone for it as for any key it has no binding for. A chord with
/// Alt or Control is never a binding — on a pty Alt+p arrives as ESC p and reads as p with the
/// Alt modifier — while Shift is what makes <c>?</c>, <c>+</c> and an upper-case letter, so it
/// is not. The letters and symbols are matched by the typed character (so a keypad <c>+</c>
/// counts) and the arrows, the page keys, Enter and Escape by their key. Stateless and
/// thread-safe.
/// </summary>
internal static class LiveDashboardKeyHandler
{
    /// <summary>The rungs of the ladder <c>+</c> and <c>-</c> move the time window along, in samples, narrowest first; every sample (null) is the top of the ladder above the widest rung, and nothing lies below the narrowest.</summary>
    public static readonly IReadOnlyList<int> TimeWindowLadder = new[] { 60, 120, 300 };

    /// <summary>The entries Page Up and Page Down scroll the error log by — a fixed page, since the handler never sees the window's height.</summary>
    public const int ErrorLogPageSize = 10;

    /// <summary>The key that toggles the pause, in either case.</summary>
    public const char PauseKey = 'p';

    /// <summary>The key that toggles the help.</summary>
    public const char HelpKey = '?';

    /// <summary>The key that widens the time window.</summary>
    public const char WidenTimeWindowKey = '+';

    /// <summary>The key that narrows the time window.</summary>
    public const char NarrowTimeWindowKey = '-';

    /// <summary>
    /// The state after one key press, per the class summary; the given state is never changed
    /// and comes back as it is for a key without a binding, a chord with Alt or Control, and a
    /// binding with nothing to do.
    /// </summary>
    public static LiveDashboardViewState Apply(LiveDashboardViewState state, ConsoleKeyInfo key, IReadOnlyList<LiveMetricsSnapshot> snapshots)
    {
        if (snapshots == null)
            throw new ArgumentNullException(nameof(snapshots), "Snapshots cannot be null.");

        if ((key.Modifiers & (ConsoleModifiers.Alt | ConsoleModifiers.Control)) != 0)
            return state;

        var stepOrder = DisplayedStepOrder(snapshots);

        switch (key.Key)
        {
            case ConsoleKey.UpArrow:
                return MoveUp(state, stepOrder, snapshots);
            case ConsoleKey.DownArrow:
                return MoveDown(state, stepOrder, snapshots);
            case ConsoleKey.PageUp:
                return PageErrorLog(state, -ErrorLogPageSize, snapshots);
            case ConsoleKey.PageDown:
                return PageErrorLog(state, ErrorLogPageSize, snapshots);
            case ConsoleKey.Enter:
                return OpenStepDetail(state, stepOrder);
            case ConsoleKey.Escape:
                if (state.ShowHelp)
                    return state with { ShowHelp = false };

                return state with { View = LiveDashboardView.Overview };
        }

        switch (char.ToLowerInvariant(key.KeyChar))
        {
            case '1':
                return state with { View = LiveDashboardView.Overview };
            case '2':
                return OpenStepDetail(state, stepOrder);
            case '3':
                return state with { View = LiveDashboardView.ErrorLog };
            case PauseKey:
                return state with { IsPaused = !state.IsPaused };
            case WidenTimeWindowKey:
                return state with { TimeWindow = WiderTimeWindow(state.TimeWindow) };
            case NarrowTimeWindowKey:
                return state with { TimeWindow = NarrowerTimeWindow(state.TimeWindow) };
            case HelpKey:
                return state with { ShowHelp = !state.ShowHelp };
        }

        return state;
    }

    // The steps the selection moves over, as declaration indices in the order the Steps table
    // displays them: the first scenario's, none without a scenario.
    private static IReadOnlyList<int> DisplayedStepOrder(IReadOnlyList<LiveMetricsSnapshot> snapshots)
    {
        if (snapshots.Count == 0)
            return Array.Empty<int>();

        return LiveDashboardLayout.DisplayedStepOrder(snapshots[0]);
    }

    private static LiveDashboardViewState MoveUp(LiveDashboardViewState state, IReadOnlyList<int> stepOrder, IReadOnlyList<LiveMetricsSnapshot> snapshots)
    {
        if (state.View == LiveDashboardView.ErrorLog)
            return ScrollErrorLog(state, -1, snapshots);

        return MoveSelection(state, -1, stepOrder);
    }

    private static LiveDashboardViewState MoveDown(LiveDashboardViewState state, IReadOnlyList<int> stepOrder, IReadOnlyList<LiveMetricsSnapshot> snapshots)
    {
        if (state.View == LiveDashboardView.ErrorLog)
            return ScrollErrorLog(state, 1, snapshots);

        return MoveSelection(state, 1, stepOrder);
    }

    // A page key: the log scrolled by a page in the log view, nothing elsewhere.
    private static LiveDashboardViewState PageErrorLog(LiveDashboardViewState state, int delta, IReadOnlyList<LiveMetricsSnapshot> snapshots)
    {
        if (state.View != LiveDashboardView.ErrorLog)
            return state;

        return ScrollErrorLog(state, delta, snapshots);
    }

    // The error log's scroll moved by delta entries and clamped into the first scenario's
    // entries: never below 0, never past the last entry, and 0 without an entry or a scenario
    // — so a stale offset past the entries comes back onto them with the next press.
    private static LiveDashboardViewState ScrollErrorLog(LiveDashboardViewState state, int delta, IReadOnlyList<LiveMetricsSnapshot> snapshots)
    {
        var lastEntry = 0;
        if (snapshots.Count > 0)
            lastEntry = Math.Max(0, snapshots[0].Errors.Count - 1);

        return state with { ErrorLogScroll = (int)Math.Clamp((long)state.ErrorLogScroll + delta, 0, lastEntry) };
    }

    // The selection one displayed row in the given direction, clamped into the rows: the top
    // row's step from no selection (or one that names no step) whichever the direction, and
    // no change without steps. The row is the layout's DisplayedPosition, the one the Steps
    // table marks.
    private static LiveDashboardViewState MoveSelection(LiveDashboardViewState state, int direction, IReadOnlyList<int> stepOrder)
    {
        if (stepOrder.Count == 0)
            return state;

        var position = LiveDashboardLayout.DisplayedPosition(stepOrder, state.SelectedStepIndex);
        if (position < 0)
            return state with { SelectedStepIndex = stepOrder[0] };

        return state with { SelectedStepIndex = stepOrder[Math.Clamp(position + direction, 0, stepOrder.Count - 1)] };
    }

    // The step detail on the selected step, the top row's step when none is selected (or the
    // selection names no step); nothing without steps.
    private static LiveDashboardViewState OpenStepDetail(LiveDashboardViewState state, IReadOnlyList<int> stepOrder)
    {
        if (stepOrder.Count == 0)
            return state;

        var selected = state.SelectedStepIndex;
        if (LiveDashboardLayout.DisplayedPosition(stepOrder, selected) < 0)
            selected = stepOrder[0];

        return state with { View = LiveDashboardView.StepDetail, SelectedStepIndex = selected };
    }

    // One rung up the ladder: the narrowest rung wider than the window, and every sample when
    // there is none — so every sample stays, and a window off the ladder lands on the rung
    // above it.
    private static int? WiderTimeWindow(int? timeWindow)
    {
        if (timeWindow == null)
            return null;

        for (var index = 0; index < TimeWindowLadder.Count; index++)
        {
            if (TimeWindowLadder[index] > timeWindow.Value)
                return TimeWindowLadder[index];
        }

        return null;
    }

    // One rung down the ladder: the widest rung narrower than the window — every sample is
    // wider than every rung — and the narrowest rung when there is none, so the narrowest
    // stays, and a window off the ladder lands on the rung below it or on the narrowest.
    private static int? NarrowerTimeWindow(int? timeWindow)
    {
        if (timeWindow == null)
            return TimeWindowLadder[TimeWindowLadder.Count - 1];

        for (var index = TimeWindowLadder.Count - 1; index >= 0; index--)
        {
            if (TimeWindowLadder[index] < timeWindow.Value)
                return TimeWindowLadder[index];
        }

        return TimeWindowLadder[0];
    }
}
