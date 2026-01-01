using System.Reflection;
using System.Text.RegularExpressions;
using Fuzn.TestFuzn.ConsoleOutput;
using Fuzn.TestFuzn.Contracts.Adapters;
using Fuzn.TestFuzn.Contracts.Results.Load;

namespace Fuzn.TestFuzn;

internal class MsTestRunnerAdapter(TestContext testContext) : ITestFrameworkAdapter
{
    private readonly TestContext _testContext = testContext;

    public bool SupportsRealTimeConsoleOutput => false;

    public ConsoleColor ForegroundColor
    {
        get;
        set;
    }
    public ConsoleColor BackgroundColor 
    {
        get;
        set;
    }

    public int WindowWidth
    {
        get;
        set;
    }

    public Task ExecuteTestMethod(ITest featureTest, MethodInfo methodInfo)
    {
        throw new NotImplementedException();
    }

    public CursorPosition GetCursorPosition()
    {
        return new CursorPosition(-1, -1);
    }

    public void SetCursorPosition(int left, int top)
    {
    }

    public void Write(string message, params object?[] args)
    {
        _testContext.Write(StripMarkup(message), args);
    }

    public void WriteTable(TableData table)
    {
        for (var i = 0; i < table.Columns.Count; i++)
            table.Columns[i] = StripMarkup(table.Columns[i]);

        foreach (var row in table.Rows)
        {
            for (var j = 0; j < row.Count; j++)
                row[j] = StripMarkup(row[j]);
        }

        var colWidths = table.Columns
            .Select((c, i) => Math.Max(c.Length, table.Rows.Max(r => r[i].Length)))
            .ToArray();

        var topBorder = "+" + string.Join("+", colWidths.Select(w => new string('-', w + 2))) + "+";
        _testContext.WriteLine(topBorder);

        var header = "| " + string.Join(" | ", table.Columns.Select((c, i) => c.PadRight(colWidths[i]))) + " |";
        _testContext.WriteLine(header);

        var headerSeparator = "+" + string.Join("+", colWidths.Select(w => new string('-', w + 2))) + "+";
        _testContext.WriteLine(headerSeparator);

        foreach (var row in table.Rows)
        {
            var line = "| " + string.Join(" | ", row.Select((cell, i) => cell.PadRight(colWidths[i]))) + " |";
            _testContext.WriteLine(line);
        }

        _testContext.WriteLine(topBorder);
    }

    public void WriteMarkup(string text)
    {
        _testContext.WriteLine(StripMarkup(text));
    }

    public void WritePanel(string[] messages, string header)
    {
        var cleanHeader = StripMarkup(header);
        var cleanMessages = messages.Select(StripMarkup).ToArray();

        var contentWidth = Math.Max(
            cleanHeader.Length,
            cleanMessages.Length > 0 ? cleanMessages.Max(m => m.Length) : 0
        );
        var panelWidth = contentWidth + 4;
        var headerPad = (panelWidth - 2 - cleanHeader.Length) / 2;
        var headerPadRight = panelWidth - 2 - cleanHeader.Length - headerPad;
        var topBorder = "+" + new string('-', headerPad) + cleanHeader + new string('-', headerPadRight) + "+";
        var bottomBorder = "+" + new string('-', panelWidth - 2) + "+";

        _testContext.WriteLine(topBorder);
        foreach (var msg in cleanMessages)
        {
            var messageLine = "| " + msg.PadRight(panelWidth - 4) + " |";
            _testContext.WriteLine(messageLine);
        }
        _testContext.WriteLine(bottomBorder);
    }

    public void WriteAdvancedTable(AdvancedTable table)
    {
        // Calculate max width for each column, considering ColSpan and markup stripping
        int colCount = table.ColumnCount;
        int[] colWidths = new int[colCount];

        // Pass 1: Calculate widths
        foreach (var row in table.Rows)
        {
            if (row.IsDivider) continue;
            int col = 0;
            foreach (var cell in row.Cells)
            {
                int span = cell.ColSpan;
                int contentWidth = cell.GetContentWidth();
                if (span == 1)
                {
                    if (contentWidth > colWidths[col])
                        colWidths[col] = contentWidth;
                }
                else
                {
                    // For spanning cells, distribute width equally (rounded up)
                    int perCol = (contentWidth + span - 1) / span;
                    for (int i = 0; i < span; i++)
                    {
                        if (perCol > colWidths[col + i])
                            colWidths[col + i] = perCol;
                    }
                }
                col += span;
            }
        }

        // Add some padding for aesthetics
        for (int i = 0; i < colWidths.Length; i++)
            colWidths[i] += 4;

        // Helper to render border
        string BorderLine() => "+" + string.Join("-", colWidths.Select(w => new string('-', w))) + "+";
        _testContext.WriteLine(BorderLine());

        foreach (var row in table.Rows)
        {
            if (row.IsDivider)
            {
                _testContext.WriteLine(BorderLine());
                continue;
            }
            var line = "|";
            int col = 0;
            foreach (var cell in row.Cells)
            {
                int span = cell.ColSpan;
                int spanWidth = colWidths.Skip(col).Take(span).Sum() + (span - 1); // 1 for each border
                line += cell.Render(spanWidth) + "|";
                col += span;
            }
            _testContext.WriteLine(line);
        }
        _testContext.WriteLine(BorderLine());
    }

    public void WriteSummary(DateTime testRunStartDateTime, TimeSpan totalRunDuration, Dictionary<Scenario, ScenarioLoadResult> scenarioLoadResults)
    {
        throw new NotImplementedException("Should not happen");
    }

    public string TestResultsDirectory => Directory.GetParent(_testContext.TestRunDirectory).ToString();

    private static string StripMarkup(string input)
    {
        return Regex.Replace(input, @"\[[^\]]+\]", string.Empty);
    }

    public void SetCurrentTestAsSkipped()
    {
        Assert.Inconclusive("Scenario test skipped.");
    }

    public void ThrowTestFuznIsNotInitializedException()
    {
        throw new InvalidOperationException("TestFuzn is not initialized. Please ensure that TestFuznIntegration.Init() has been called from [AssemblyInitialize].");
    }
}
