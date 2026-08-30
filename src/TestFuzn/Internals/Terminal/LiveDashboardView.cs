namespace Fuzn.TestFuzn.Internals.Terminal;

/// <summary>
/// Which of the live dashboard's views a frame lays out — the viewer's choice, carried on
/// <see cref="LiveDashboardViewState.View"/> and switched by <see cref="LiveDashboardKeyHandler"/>:
/// the overview of every scenario (the default), the detail of the selected step, or the
/// error log.
/// </summary>
internal enum LiveDashboardView
{
    /// <summary>Every scenario's section — tiles, timeline, charts, heatmap, requests, steps and the error ticker.</summary>
    Overview,

    /// <summary>The selected step on its own (<see cref="LiveDashboardViewState.SelectedStepIndex"/>).</summary>
    StepDetail,

    /// <summary>The scenarios' errors as a scrollable log (<see cref="LiveDashboardViewState.ErrorLogScroll"/>).</summary>
    ErrorLog
}
