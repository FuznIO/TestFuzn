using System.Reflection;
using Fuzn.TestFuzn.ConsoleOutput;
using Fuzn.TestFuzn.Contracts.Adapters;
using Fuzn.TestFuzn.Contracts.Results.Load;

namespace Fuzn.TestFuzn.Tests.Terminal;

/// <summary>
/// A standalone-style test framework adapter (real-time output supported) that records what is
/// written through it into a shared event log — the summary as <see cref="SummaryEvent"/>,
/// markup lines prefixed <see cref="MarkupEventPrefix"/>, plain writes prefixed
/// <see cref="WriteEventPrefix"/> — so ordering against other writers logging into the same list
/// (a terminal writer's observer, say) can be asserted, and whose token <see cref="Cancel"/>
/// cancels, as Ctrl+C does on the standalone adapter. Nothing reaches the console.
/// </summary>
internal sealed class FakeTestFrameworkAdapter : ITestFrameworkAdapter
{
    /// <summary>The event recorded for a load summary write.</summary>
    public const string SummaryEvent = "summary";

    /// <summary>The prefix of the event recorded for a markup line, followed by the markup.</summary>
    public const string MarkupEventPrefix = "markup:";

    /// <summary>The prefix of the event recorded for a plain write, followed by the message.</summary>
    public const string WriteEventPrefix = "write:";

    private readonly List<string> _events;
    private readonly CancellationTokenSource _cancellation = new CancellationTokenSource();

    /// <param name="events">The shared event log; every recording locks on it.</param>
    public FakeTestFrameworkAdapter(List<string> events)
    {
        if (events == null)
            throw new ArgumentNullException(nameof(events), "Events cannot be null.");

        _events = events;
    }

    public bool SupportsRealTimeConsoleOutput { get; set; } = true;

    public CancellationToken CancellationToken => _cancellation.Token;

    public ConsoleColor ForegroundColor { get; set; }

    public ConsoleColor BackgroundColor { get; set; }

    public int WindowWidth => 80;

    public string TestResultsDirectory => Path.GetTempPath();

    /// <summary>What Ctrl+C does on the standalone adapter.</summary>
    public void Cancel()
    {
        _cancellation.Cancel();
    }

    public Task ExecuteTestMethod(ITest test, MethodInfo methodInfo)
    {
        return Task.CompletedTask;
    }

    public CursorPosition GetCursorPosition()
    {
        return new CursorPosition(0, 0);
    }

    public void SetCursorPosition(int left, int top)
    {
    }

    public void Write(string message, params object?[] args)
    {
        Record(WriteEventPrefix + message);
    }

    public void WriteTable(TableData table)
    {
        Record("table");
    }

    public void WriteMarkup(string text)
    {
        Record(MarkupEventPrefix + text);
    }

    public void WritePanel(string[] messages, string header)
    {
        Record("panel:" + header);
    }

    public void WriteAdvancedTable(AdvancedTable table)
    {
        Record("advanced-table");
    }

    public void WriteSummary(DateTime testRunStartDateTime, TimeSpan totalRunDuration, Dictionary<Scenario, ScenarioLoadResult> scenarioLoadResults)
    {
        Record(SummaryEvent);
    }

    public void SetCurrentTestAsSkipped()
    {
    }

    public void ThrowTestFuznIsNotInitializedException()
    {
        throw new InvalidOperationException("TestFuzn is not initialized.");
    }

    private void Record(string eventName)
    {
        lock (_events)
            _events.Add(eventName);
    }
}
