namespace Fuzn.TestFuzn.Internals.Terminal;

/// <summary>
/// What the dashboard's viewer has chosen, as opposed to what the run has measured: the
/// interaction state <see cref="LiveDashboardLayout.Render"/> lays a frame out for beside the
/// scenarios' snapshots. Immutable — the default value is <see cref="Default"/>, nothing
/// selected, and a key handler derives the next state with a <c>with</c> expression — so a
/// frame is a pure function of the snapshots and one of these, and identical inputs still
/// render an identical frame.
/// </summary>
internal readonly struct LiveDashboardViewState
{
    /// <summary>The state before the viewer has chosen anything: no step selected.</summary>
    public static LiveDashboardViewState Default => default;

    /// <summary>
    /// The highlighted row of the Steps table, as an index into the rows as they are
    /// displayed — the pain-sorted order the layout shows, not the scenario's declaration
    /// order, and only the rows the height budget kept — or null for no selection. The layout
    /// clamps the index into the displayed rows: one past the last row (a row the budget
    /// trimmed away) selects the last row shown and a negative one the first, so a selection
    /// never disappears while there are rows; with no rows displayed nothing is highlighted.
    /// </summary>
    public int? SelectedStepIndex { get; init; }
}
