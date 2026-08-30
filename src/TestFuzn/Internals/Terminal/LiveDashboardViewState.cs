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
/// the key handler resolve what they read against what the snapshots allow.
/// </summary>
internal readonly struct LiveDashboardViewState
{
    /// <summary>The state before the viewer has chosen anything: the overview, no step selected, the log at its top, not paused, every sample in the window, no help.</summary>
    public static LiveDashboardViewState Default => default;

    /// <summary>The view the frame lays out; <see cref="LiveDashboardView.Overview"/> until the viewer switches.</summary>
    public LiveDashboardView View { get; init; }

    /// <summary>
    /// The selected step, as its declaration index into <see cref="LiveMetricsSnapshot.Steps"/>
    /// — a stable identity: the Steps table sorts its rows by pain and reshuffles them as the
    /// readings move, and the highlight follows the step wherever the sort puts its row — or
    /// null for no selection. The key handler moves it up and down the rows in the order the
    /// table displays them (<see cref="LiveDashboardLayout.DisplayedStepOrder"/>), so ↑ and ↓
    /// walk what the viewer sees, and stores the neighbour's declaration index. A value that
    /// names no step — a negative index, one past the steps, which only a step that has gone
    /// from the snapshot can leave behind — selects nothing: the layout highlights no row and
    /// the step detail shows its notice, and the key handler treats it as no selection. One
    /// selection for the whole frame: it applies to every scenario's section, each resolving it
    /// against its own steps, and the step detail shows the first scenario's selected step —
    /// the current rule, since the key handler moves the selection over the first scenario's
    /// steps.
    /// </summary>
    public int? SelectedStepIndex { get; init; }

    /// <summary>
    /// How many entries the error log is scrolled down from its top — the position of the
    /// first entry on its page, in the log's own order (grouped by step) — never below 0. The
    /// key handler clamps it into the first scenario's entries, 0 to their count less one; the
    /// log view clamps it further, to the last position its page is still full from, so a
    /// scroll past that shows the last full page rather than a short one (the layout
    /// documents both clamps).
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

    /// <summary>Whether the help overlay is up: the layout composes its help panel over the frame of whichever view is up, and the footer shows how to close it.</summary>
    public bool ShowHelp { get; init; }
}
