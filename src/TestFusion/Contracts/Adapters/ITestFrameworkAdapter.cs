using TestFusion.ConsoleOutput;
using TestFusion.Results.Load;

namespace TestFusion.Contracts.Adapters;

public interface ITestFrameworkAdapter
{
    bool SupportsRealTimeConsoleOutput { get; }
    ConsoleColor ForegroundColor { get; set; }
    ConsoleColor BackgroundColor { get; set; }
    int WindowWidth { get; }
    CursorPosition GetCursorPosition();
    void SetCursorPosition(int left, int top);
    void Write(string message, params object?[] args);
    void WriteTable(TableData table);
    void WriteMarkup(string text);
    void WritePanel(string[] messages, string header);
    public void WriteAdvancedTable(AdvancedTable table);
    public void WriteSummary(DateTime testRunStartDateTime, TimeSpan totalRunDuration, Dictionary<Scenario, ScenarioLoadResult> scenarioLoadResults);
    string TestResultsDirectory { get; }
}
