namespace Fuzn.TestFuzn.Internals.Terminal;

/// <summary>
/// What the dashboard's viewer has chosen, as opposed to what the run has measured: the
/// interaction state <see cref="LiveDashboardLayout.Render"/> lays a frame out for beside the
/// scenarios' snapshots — the view, the step selection, the error log's scroll, whether the
/// frame is paused, the time window the charts show and whether the help is up. Immutable —
/// the default value is <see cref="Default"/>, the overview with nothing chosen, and
/// <see cref="LiveDashboardKeyHandler"/> derives the next state from a key press with a
/// <c>with</c> expression — so a frame is a pure function of the snapshots and one of these, and
/// identical inputs still render an identical frame. Nothing here is validated: the layout and
/// the key handler clamp what they read into what the snapshots allow.
/// </summary>
internal readonly struct LiveDashboardViewState
{
    /// <summary>The state before the viewer has chosen anything: the overview, no step selected, the log at its top, not paused, every sample in the window, no help.</summary>
    public static LiveDashboardViewState Default => default;

    /// <summary>The view the frame lays out; <see cref="LiveDashboardView.Overview"/> until the viewer switches.</summary>
    public LiveDashboardView View { get; init; }

    /// <summary>
    /// The highlighted row of the Steps table, as an index into the rows as they are
    /// displayed — the pain-sorted order the layout shows, not the scenario's declaration
    /// order, and only the rows the height budget kept — or null for no selection. The layout
    /// clamps the index into the displayed rows: one past the last row (a row the budget
    /// trimmed away) selects the last row shown and a negative one the first, so a selection
    /// never disappears while there are rows; with no rows displayed nothing is highlighted.
    /// One selection for the whole frame: it applies to every scenario's section alike, and
    /// the step detail shows the first scenario's selected step — the current rule, since the
    /// key handler moves the selection over the first scenario's steps.
    /// </summary>
    public int? SelectedStepIndex { get; init; }

    /// <summary>
    /// How many entries the error log is scrolled down from its top, never below 0. The key
    /// handler clamps only at 0; the log view clamps the top end into the entries it has, so a
    /// scroll past the last entry lands on it.
    /// </summary>
    public int ErrorLogScroll { get; init; }

    /// <summary>
    /// Whether the viewer has paused the frame: the picture holds the snapshots as they were
    /// when the pause began, under a <c>⏸ paused</c> badge, while the sampling goes on behind
    /// it — so resuming jumps to now. The console manager does the holding; the layout only
    /// shows the badge.
    /// </summary>
    public bool IsPaused { get; init; }

    /// <summary>
    /// How many of the newest samples the charts and the heatmap show, or null for every
    /// sample — the default. In samples, which for the dashboard's 1 Hz series is seconds; the
    /// key handler moves it along <see cref="LiveDashboardKeyHandler.TimeWindowLadder"/>. The
    /// tiles' deltas and trends keep their own fixed windows whatever this says.
    /// </summary>
    public int? TimeWindow { get; init; }

    /// <summary>Whether the help overlay is up. The footer shows how to close it; the overlay itself is drawn by the layout.</summary>
    public bool ShowHelp { get; init; }
}
