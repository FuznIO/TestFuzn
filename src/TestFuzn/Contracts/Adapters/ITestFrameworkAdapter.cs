using Fuzn.TestFuzn.ConsoleOutput;
using Fuzn.TestFuzn.Contracts.Results.Load;
using System.Reflection;

namespace Fuzn.TestFuzn.Contracts.Adapters;

internal interface ITestFrameworkAdapter
{
    Task ExecuteTestMethod(IFeatureTest featureTest, MethodInfo methodInfo);
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
    void SetCurrentTestAsSkipped();
}
