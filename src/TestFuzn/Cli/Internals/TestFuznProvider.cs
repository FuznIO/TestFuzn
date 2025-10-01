using Spectre.Console;
using System.Text;
using Fuzn.TestFuzn.ConsoleOutput;
using Fuzn.TestFuzn.Contracts.Adapters;
using Fuzn.TestFuzn.Contracts.Results.Load;

namespace Fuzn.TestFuzn.Cli.Internals;

internal class TestFuznProvider : ITestFrameworkAdapter
{
    public bool SupportsRealTimeConsoleOutput => true;

    public ConsoleColor ForegroundColor
    {
        get => Console.ForegroundColor;
        set => Console.ForegroundColor = value;
    }
    public ConsoleColor BackgroundColor
    {
        get => Console.BackgroundColor;
        set => Console.BackgroundColor = value;
    }

    public int WindowWidth => Console.WindowWidth;

    public void WriteAdvancedTable(AdvancedTable table)
    {
        AnsiConsole.MarkupLine("[red]not implemented[/]");
    }

    public void WriteSummary(DateTime testRunStartDateTime, TimeSpan totalRunDuration, Dictionary<Scenario, ScenarioLoadResult> scenarioLoadResults)
    {
        //AnsiConsole.Clear();
        foreach (var scenario in scenarioLoadResults)
        {
            // ── Header Panel ──────────────────────────────────────────────────────────────
            var headerTable = new Table().Border(TableBorder.Rounded).Expand();
            headerTable.AddColumn(new TableColumn("[yellow]Scenario[/]").Centered());
            headerTable.AddColumn(new TableColumn("[yellow]Execution Time[/]").Centered());
            headerTable.AddColumn(new TableColumn("[yellow]Test Run Time[/]").Centered());
            headerTable.AddColumn(new TableColumn("[yellow]Status[/]").Centered());

            headerTable.AddRow(
                $"{scenario.Key.Name}",
                $"{scenario.Value.TotalExecutionDuration.ToTestFuznFormattedDuration()}",
                $"{(scenario.Value.TestRunTotalDuration()).ToTestFuznFormattedDuration()}",
                $"{(scenario.Value.Status == ScenarioStatus.Passed ? "[green]Passed[/]" : "[red]Failed[/]")}"
            );

            AnsiConsole
                .Write(new Panel(headerTable)
                    .Header("[bold white on blue] Load Test Summary [/]")
                    .Expand());

            // ── Load Simulations ─────────────────────────────────────────────────────────
            var loads = new Table { Border = TableBorder.Rounded, Expand = true };
            loads.AddColumn("Type");
            foreach (var simulations in scenario.Key.SimulationsInternal)
                loads.AddRow($"{simulations.GetDescription()}");

            AnsiConsole
                .Write(new Panel(loads)
                    .Header("[bold white on blue] Load Simulations [/]")
                    .Expand());

            // ── Global Metrics ───────────────────────────────────────────────────────────
            var reqTable = CreateRequestsTable(scenario.Value.RequestCount, scenario.Value.Ok, scenario.Value.Failed);
            var respTable = CreateResponseTimeTable(scenario.Value.Ok, scenario.Value.Failed);

            var summaryGrid = new Grid().AddColumn().AddColumn();
            summaryGrid.AddRow(reqTable, respTable);

            AnsiConsole.Write(new Panel(summaryGrid)
                .Header("[bold]Global Metrics[/]")
                .Border(BoxBorder.Rounded)
                .Expand());

            // ── Per-Step Metrics ────────────────────────────────────────────────────────
            foreach (var step in scenario.Value.Steps)
            {
                reqTable = CreateRequestsTable(step.Value.RequestCount, step.Value.Ok, step.Value.Failed);
                var stepGrid = new Grid().AddColumn().AddColumn();
                var responseTimeTable = CreateResponseTimeTable(step.Value.Ok, step.Value.Failed);

                stepGrid.AddRow(reqTable, responseTimeTable);

                AnsiConsole.Write(new Panel(stepGrid)
                    .Header($"[bold white on darkgreen] Step {step.Key} Details [/]")
                    .Border(BoxBorder.Rounded)
                    .Expand());
            }

            // Errors
            var errorSection = new StringBuilder();
            foreach (var step in scenario.Value.Steps)
            {
                if (step.Value.Errors?.Count > 0)
                {
                    errorSection.AppendLine($"[red]{step.Key}:[/]");
                    foreach (var error in step.Value.Errors)
                        errorSection.AppendLine($"  [red]{error.Key} (Count: {error.Value.Count})[/]");
                }
            }
            if (errorSection.Length > 0)
            {
                AnsiConsole
                    .Write(new Panel(new Markup(errorSection.ToString()))
                        .Header("[bold red] Errors by Step [/]")
                        .Expand());
            }
        }
    }

    private Table CreateResponseTimeTable(string min, string mean, string max, string stdDev, string median, string p75, string p95, string p99)
    {
        var table = new Table { Border = TableBorder.Minimal };
        table.Title("[blue]Response Times[/]");
        table.AddColumn("[u]Min[/]");
        table.AddColumn("[u]Mean[/]");
        table.AddColumn("[u]Max[/]");
        table.AddColumn("[u]StdDev[/]");
        table.AddColumn("[u]Median[/]");
        table.AddColumn("[u]P75[/]");
        table.AddColumn("[u]P95[/]");
        table.AddColumn("[u]P99[/]");
        table.AddRow(min, mean, max, stdDev, median, p75, p95, p99);
        return table;
    }

    private Table CreateResponseTimeTable(Stats ok, Stats failed)
    {
        var table = new Table { Border = TableBorder.Minimal };
        table.Title("[blue]Response Times[/]");
        table.AddColumn("[u]Metric[/]");
        table.AddColumn("[u]Min[/]");
        table.AddColumn("[u]Mean[/]");
        table.AddColumn("[u]Max[/]");
        table.AddColumn("[u]StdDev[/]");
        table.AddColumn("[u]Median[/]");
        table.AddColumn("[u]P75[/]");
        table.AddColumn("[u]P95[/]");
        table.AddColumn("[u]P99[/]");
        table.AddRow(
            $"[green]Ok[/]",
            $"[green]{ok.ResponseTimeMin.ToTestFuznResponseTime()}[/]",
            $"[green]{ok.ResponseTimeMean.ToTestFuznResponseTime()}[/]",
            $"[green]{ok.ResponseTimeMax.ToTestFuznResponseTime()}[/]",
            $"[green]{ok.ResponseTimeStandardDeviation.ToTestFuznResponseTime()}[/]",
            $"[green]{ok.ResponseTimeMedian.ToTestFuznResponseTime()}[/]",
            $"[green]{ok.ResponseTimePercentile75.ToTestFuznResponseTime()}[/]",
            $"[green]{ok.ResponseTimePercentile95.ToTestFuznResponseTime()}[/]",
            $"[green]{ok.ResponseTimePercentile99.ToTestFuznResponseTime()}[/]"
        );
        table.AddRow(
            $"[red]Failed[/]",
            $"[red]{failed.ResponseTimeMin.ToTestFuznResponseTime()}[/]",
            $"[red]{failed.ResponseTimeMean.ToTestFuznResponseTime()}[/]",
            $"[red]{failed.ResponseTimeMax.ToTestFuznResponseTime()}[/]",
            $"[red]{failed.ResponseTimeStandardDeviation.ToTestFuznResponseTime()}[/]",
            $"[red]{failed.ResponseTimeMedian.ToTestFuznResponseTime()}[/]",
            $"[red]{failed.ResponseTimePercentile75.ToTestFuznResponseTime()}[/]",
            $"[red]{failed.ResponseTimePercentile95.ToTestFuznResponseTime()}[/]",
            $"[red]{failed.ResponseTimePercentile99.ToTestFuznResponseTime()}[/]"
        );
        return table;
    }

    private Table CreateRequestsTable(int totalRequests, Stats ok, Stats failed)
    {
        var table = new Table { Border = TableBorder.Minimal };
        table.Title("[green]Requests[/]");
        table.AddColumn("[u]Metric[/]");
        table.AddColumn("[u]Count[/]");
        table.AddColumn("[u]RPS[/]");
        table.AddRow("Total", $"{totalRequests}", "");
        table.AddRow("[green]OK[/]", $"{ok.RequestCount}", $"{ok.RequestsPerSecond}");
        table.AddRow("[red]Failed[/]", $"{failed.RequestCount}", $"{failed.RequestsPerSecond}");
        return table;
    }

    public string TestResultsDirectory
    {
        get
        {
            var assemblyLocation = System.Reflection.Assembly.GetEntryAssembly().Location;
            var directory = Path.Combine(Path.GetDirectoryName(assemblyLocation)!, "TestResults");
            return directory;
        }
    }

    public CursorPosition GetCursorPosition()
    {
        var cursorPosition = Console.GetCursorPosition();

        return new CursorPosition(cursorPosition.Left, cursorPosition.Top);
    }

    public void SetCursorPosition(int left, int top)
    {
        Console.SetCursorPosition(left, top);
    }

    public void Write(string message, params object?[] args)
    {
        Console.WriteLine(message, args);
    }

    public void WriteTable(TableData table)
    {
        var t = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.SeaGreen1);
        foreach (var col in table.Columns)
            t.AddColumn(new TableColumn(col));
        foreach (var row in table.Rows)
            t.AddRow(row.ToArray());
        AnsiConsole.Write(t);
    }

    public void WriteMarkup(string text)
    {
        AnsiConsole.MarkupLine(text);
    }

    public void WritePanel(string[] messages, string header)
    {
        AnsiConsole.Write(new Panel(string.Join(Environment.NewLine, messages))
            .Border(BoxBorder.Rounded)
            .Header(header, Justify.Center));
    }
}
