using System.Reflection;
using Fuzn.TestFuzn.ConsoleOutput;
using Fuzn.TestFuzn.Contracts.Adapters;
using Fuzn.TestFuzn.Contracts.Results.Load;

namespace Fuzn.TestFuzn.Tests.Terminal;

internal sealed class FakeTestFrameworkAdapter : ITestFrameworkAdapter
{
    public const string SummaryEvent = "summary";

    public const string MarkupEventPrefix = "markup:";

    public const string WriteEventPrefix = "write:";

    private readonly List<string> _events;
    private readonly CancellationTokenSource _cancellation = new CancellationTokenSource();

    public FakeTestFrameworkAdapter(List<string> events)
    {
        if (events == null)
            throw new ArgumentNullException(nameof(events), "Events cannot be null.");

        _events = events;
    }

    public bool SupportsRealTimeConsoleOutput { get; set; } = true;

    public CancellationToken CancellationToken => _cancellation.Token;

    public string TestResultsDirectory => Path.GetTempPath();

    public void Cancel()
    {
        _cancellation.Cancel();
    }

    public Task ExecuteTestMethod(ITest test, MethodInfo methodInfo)
    {
        return Task.CompletedTask;
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

    public void WriteSummary(DateTime testRunStartDateTime, TimeSpan totalRunDuration, Dictionary<Scenario, ScenarioLoadResult> scenarioLoadResults, string reportPath, string executionEnvironment, string targetEnvironment)
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
